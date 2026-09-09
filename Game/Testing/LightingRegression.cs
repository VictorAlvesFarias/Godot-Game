using Godot;
using Jogo25D.Light;
using System;
using System.Diagnostics;

namespace Jogo25D.Testing
{
    // Run: godot --path Game res://Testing/LightingRegression.tscn
    // No saves, player state, or production scenes are modified.
    public partial class LightingRegression : Node2D
    {
        private LogicalLightWorld _world;
        private static void Check(bool valid, string message) { if (!valid) throw new Exception(message); }
        private static void Settle(LightingField field) { while (!field.Settled) field.Process(1000000, 10000); }

        public override async void _Ready()
        {
            try
            {
                var remote = new LogicalLightWorld(0, "test", 1, false);
                remote.SetTerrain(0, -10000, 0);
                Check(remote.Sky(0, 0) == 0 && remote.Sun(0.5, 0.5, 0, -1) == 0, "Remote roof must block sky and sun");
                Check(remote.Sun(0.5, 0.5, 0, -1, 64) == 1, "An obstacle beyond a finite source must not block its light");
                remote.SetTerrain(0, -10000, -1);
                Check(remote.Sky(0, 0) == 255 && remote.Sun(0.5, 0.5, 0, -1) == 1, "Removing remote roof must restore sky");
                remote.SetTerrain(-2, -2, 7);
                Check(remote.Opacity(-2, -2) == 255 && remote.Sky(-2, 0) == 0
                    && remote.Sun(-1.5, 0.5, 0, -1) == 0, "Leaves must block light like every other solid block");
                remote.SetTerrain(100, -100, 0);
                Check(remote.Sun(0.5, 0.5, 1, -1) == 0, "Oblique distant roof must block sun");
                var restored = new LogicalLightWorld(0, "test", 1, false);
                foreach (var edit in remote.Edits()) restored.SetTerrain(edit.X, edit.Y, edit.Terrain);
                Check(restored.Sun(0.5, 0.5, 1, -1) == remote.Sun(0.5, 0.5, 1, -1), "Restored edits changed shadow");

                _world = new LogicalLightWorld(0, "fixture", 1, false);
                for (int x = 0; x < 64; x++) for (int y = 28; y < 36; y++) _world.SetTerrain(x, y, 0);
                Room(3, 18); Room(24, 39); Room(45, 60);
                _world.SetTerrain(28, 17, -1); _world.SetTerrain(29, 17, -1); // Window in roof.
                for (int y = 8; y < 17; y++) _world.SetTerrain(52, y, 6);
                for (int y = 4; y < 10; y++) for (int x = 48; x < 57; x++)
                    if (Math.Abs(x - 52) + Math.Abs(y - 7) < 7) _world.SetTerrain(x, y, 7);
                var watch = Stopwatch.StartNew();
                _world.Field.SetRegion(0, 0, 64, 36); Settle(_world.Field);
                Check(_world.Field.Get(10, 23).Sky == 0, "Sealed room lit");
                Check(_world.Field.Get(30, 23).Sky > 0, "Open room dark");
                _world.Field.SetSource(1, new(52, 23), new(0, 255, 130, 35)); Settle(_world.Field);
                Check(_world.Field.Get(50, 23).R > 0, "Emitter missing");
                _world.SetTerrain(28, 17, 0); _world.SetTerrain(29, 17, 0); Settle(_world.Field);
                Check(_world.Field.Get(30, 23).Sky == 0, "Closing window left stale light");
                _world.SetTerrain(28, 17, -1); _world.SetTerrain(29, 17, -1); Settle(_world.Field);
                GD.Print($"LIGHTING field: {watch.ElapsedMilliseconds}ms, {_world.Field.ProcessedCells} cells, {_world.Field.ResidentChunks} chunks");
                watch.Restart();
                var raster = new LightMapComputer();
                raster.Begin(_world, Vector2I.Zero, new(64, 36), -32, 0.5f);
                while (!raster.Complete) raster.Process(10000);
                GD.Print($"LIGHTING raster: {watch.ElapsedMilliseconds}ms");
                var light = ImageTexture.CreateFromImage(raster.LightImage());
                var emission = ImageTexture.CreateFromImage(raster.EmissionImage());
                var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/Shaders/light_map.gdshader") };
                material.SetShaderParameter("light_data", light);
                material.SetShaderParameter("map_size", new Vector2(128, 72));
                material.SetShaderParameter("sun_angle", Mathf.DegToRad(-32));
                material.SetShaderParameter("shadow_geometry", ImageTexture.CreateFromImage(raster.ShadowGeometryImage()));
                var pass = new SubViewport { Size = new(64 * LightMapComputer.ShadowSubdivisions, 36 * LightMapComputer.ShadowSubdivisions), Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Once };
                AddChild(pass);
                pass.AddChild(new ColorRect { Size = pass.Size, Material = material, Color = Colors.White });
                var present = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/Shaders/light_map_present.gdshader") };
                present.SetShaderParameter("light_data", light);
                present.SetShaderParameter("emission_data", emission);
                present.SetShaderParameter("map_size", new Vector2(128, 72));
                present.SetShaderParameter("shadow_geometry", ImageTexture.CreateFromImage(raster.ShadowGeometryImage()));
                present.SetShaderParameter("sun_angle", Mathf.DegToRad(-32));
                present.SetShaderParameter("geometry_ready", true);
                AddChild(new Sprite2D { Texture = pass.GetTexture(), Material = present, Centered = false, Scale = new(16f / LightMapComputer.ShadowSubdivisions, 16f / LightMapComputer.ShadowSubdivisions), ZIndex = 900, TextureFilter = TextureFilterEnum.Linear });
                var filteredPass = new SubViewport { Size = pass.Size, Disable3D = true,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                AddChild(filteredPass);
                filteredPass.AddChild(new ColorRect { Size = filteredPass.Size, Color = Colors.White });
                filteredPass.AddChild(new Sprite2D { Texture = pass.GetTexture(), Material = present,
                    Centered = false, TextureFilter = TextureFilterEnum.Linear });
                QueueRedraw();
                for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (DisplayServer.GetName() != "headless")
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    string folder = ProjectSettings.GlobalizePath("res://../.images");
                    DirAccess.MakeDirRecursiveAbsolute(folder);
                    using var composed = GetViewport().GetTexture().GetImage();
                    composed.SavePng(folder + "/lighting-regression.png");
                    // Check the final filter at the air/roof boundary, not only the raw sun map.
                    using var filtered = filteredPass.GetTexture().GetImage();
                    for (int x = 4 * LightMapComputer.ShadowSubdivisions; x < 18 * LightMapComputer.ShadowSubdivisions; x++)
                        Check(filtered.GetPixel(x, 18 * LightMapComputer.ShadowSubdivisions).R < 0.004,
                            "Presentation filtering leaked light across the sealed roof");
                    using var shadow = pass.GetTexture().GetImage();
                    for (int y = 19; y < 27; y++) for (int x = 5; x < 17; x++)
                        Check(shadow.GetPixel(x * LightMapComputer.ShadowSubdivisions + 1, y * LightMapComputer.ShadowSubdivisions + 1).R < 0.004, "GPU sun leaked into sealed room");
                    // A ceiling ten thousand tiles above the GPU window must still block it.
                    for (int x = 0; x < 64; x++) _world.SetTerrain(x, -10000, 0);
                    Settle(_world.Field);
                    raster.Begin(_world, Vector2I.Zero, new(64, 36), 0, 1);
                    while (!raster.Complete) raster.Process(10000);
                    material.SetShaderParameter("light_data", ImageTexture.CreateFromImage(raster.LightImage()));
                    material.SetShaderParameter("shadow_geometry", ImageTexture.CreateFromImage(raster.ShadowGeometryImage()));
                    material.SetShaderParameter("sun_angle", 0f);
                    material.SetShaderParameter("penumbra", 1f);
                    pass.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
                    for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var distantShadow = pass.GetTexture().GetImage();
                    Check(distantShadow.GetPixel(32 * LightMapComputer.ShadowSubdivisions, 10 * LightMapComputer.ShadowSubdivisions).R < 0.004, "GPU ignored remote logical ceiling");
                    // Measure cone geometry and symmetry, not just edge softness.
                    var blocker = new LogicalLightWorld(0, "penumbra", 1, false);
                    for (int y = 6; y < 14; y++) for (int x = 24; x < 32; x++) blocker.SetTerrain(x, y, 0);
                    blocker.Field.SetRegion(0, 0, 64, 36); Settle(blocker.Field);
                    int nearbySourceWidth = 0;
                    foreach (float distance in new[] { 0f, 0.5f, 1f })
                    {
                        raster.Begin(blocker, Vector2I.Zero, new(64, 36), 0, distance);
                        while (!raster.Complete) raster.Process(10000);
                        material.SetShaderParameter("light_data", ImageTexture.CreateFromImage(raster.LightImage()));
                        material.SetShaderParameter("shadow_geometry", ImageTexture.CreateFromImage(raster.ShadowGeometryImage()));
                        material.SetShaderParameter("penumbra", distance);
                        pass.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
                        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        using var result = pass.GetTexture().GetImage();
                        int near = ShadowWidth(result, 15), far = ShadowWidth(result, 33);
                        int scale = LightMapComputer.ShadowSubdivisions;
                        // Mirrored pixels about the block center must agree at every aperture.
                        for (int x = 16 * scale; x < 28 * scale; x++)
                            Check(Math.Abs(result.GetPixel(x, 33 * scale).R - result.GetPixel(56 * scale - 1 - x, 33 * scale).R) < 0.015,
                                "Penumbra shifted the shadow away from the SunAngle axis");
                        if (distance == 0)
                        {
                            Check(far > near + 10, "Open cone must expand the whole shadow");
                            nearbySourceWidth = far;
                            result.SavePng(folder + "/lighting-cone-open.png");
                        }
                        if (distance == 1)
                        {
                            Check(Math.Abs(far - near) <= 1, "Infinite source must cast parallel shadows");
                            Check(nearbySourceWidth > far + 10, "Penumbra must change projection width");
                            for (int x = 25; x < 31; x++)
                                Check(result.GetPixel(x * LightMapComputer.ShadowSubdivisions + 1, 6 * LightMapComputer.ShadowSubdivisions + 1).R > 0.9,
                                    "Lit solid top face has a dark internal seam");
                            result.SavePng(folder + "/lighting-cone-parallel.png");
                        }
                        GD.Print($"CONE {distance}: shadow near={near}px far={far}px");
                    }
                    // Put the blocker outside the GPU window. Its logical boundary rays
                    // must preserve the expanding projection in the same world coordinates.
                    raster.Begin(blocker, new(0, 16), new(64, 36), 0, 0);
                    while (!raster.Complete) raster.Process(10000);
                    material.SetShaderParameter("light_data", ImageTexture.CreateFromImage(raster.LightImage()));
                    material.SetShaderParameter("shadow_geometry", ImageTexture.CreateFromImage(raster.ShadowGeometryImage()));
                    material.SetShaderParameter("penumbra", 0f);
                    pass.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
                    for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var offscreenShadow = pass.GetTexture().GetImage();
                    Check(Math.Abs(ShadowWidth(offscreenShadow, 17) - nearbySourceWidth) <= 2,
                        "Moving the window changed the cone geometry");
                    // At 8x presentation zoom a hard diagonal must still cover only
                    // one or two display pixels, rather than magnifying solar texels.
                    raster.Begin(blocker, Vector2I.Zero, new(64, 36), -11.5f, 1);
                    while (!raster.Complete) raster.Process(10000);
                    present.SetShaderParameter("light_data", ImageTexture.CreateFromImage(raster.LightImage()));
                    present.SetShaderParameter("shadow_geometry", ImageTexture.CreateFromImage(raster.ShadowGeometryImage()));
                    present.SetShaderParameter("sun_angle", Mathf.DegToRad(-11.5f));
                    present.SetShaderParameter("penumbra", 1f);
                    present.SetShaderParameter("ambient_energy", 0f);
                    present.SetShaderParameter("sun_energy", 1f);
                    present.SetShaderParameter("sun_color", Colors.White);
                    filteredPass.Size = new(2048, 1152);
                    filteredPass.GetChild<ColorRect>(0).Size = filteredPass.Size;
                    filteredPass.GetChild<Sprite2D>(1).Scale = new(8, 8);
                    for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var zoomed = filteredPass.GetTexture().GetImage();
                    int smoothRows = 0;
                    for (int y = 20 * 32; y < 30 * 32; y++)
                    {
                        int transition = 0;
                        for (int x = 32 * 32; x < 42 * 32; x++)
                        {
                            float value = zoomed.GetPixel(x, y).R;
                            if (value > 0.05 && value < 0.95) transition++;
                        }
                        Check(transition <= 2, "Zoom magnified the cached shadow edge");
                        if (transition > 0) smoothRows++;
                    }
                    Check(smoothRows > 250, "Zoomed diagonal lacks subpixel coverage");
                    zoomed.SavePng(folder + "/lighting-edge-zoom.png");
                    GD.Print($"DISPLAY AA PASS: {smoothRows}/320 rows have subpixel coverage at 8x zoom");
                    raster.Begin(blocker, Vector2I.Zero, new(64, 36), -11.5f, 0);
                    while (!raster.Complete) raster.Process(10000);
                    present.SetShaderParameter("shadow_geometry", ImageTexture.CreateFromImage(raster.ShadowGeometryImage()));
                    present.SetShaderParameter("penumbra", 0f);
                    for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var soft = filteredPass.GetTexture().GetImage();
                    int nearTransition = SoftTransition(soft, 16 * 32);
                    int farTransition = SoftTransition(soft, 30 * 32);
                    Check(nearTransition > 8 && farTransition > nearTransition + 15,
                        "Penumbra must contain a real gradient that widens with distance");
                    soft.SavePng(folder + "/lighting-edge-soft.png");
                    GD.Print($"SOFT TRANSITION PASS: near={nearTransition}px far={farTransition}px");
                }
                GD.Print("LIGHTING REGRESSION PASS");
                GetTree().Quit();
            }
            catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        }

        private static int SoftTransition(Image image, int y)
        {
            int count = 0;
            for (int x = 32 * 32; x < 48 * 32; x++)
            {
                float value = image.GetPixel(x, y).R;
                if (value > 0.05 && value < 0.95) count++;
            }
            return count;
        }

        private static int ShadowWidth(Image image, int y)
        {
            int count = 0, scale = LightMapComputer.ShadowSubdivisions;
            for (int x = 16 * scale; x < 40 * scale; x++)
            {
                float value = image.GetPixel(x, y * scale).R;
                if (value < 0.5f) count++;
            }
            return count;
        }

        private void Room(int left, int right)
        {
            for (int x = left; x <= right; x++) _world.SetTerrain(x, 17, 0);
            for (int y = 17; y < 28; y++) { _world.SetTerrain(left, y, 0); _world.SetTerrain(right, y, 0); }
        }

        public override void _Draw()
        {
            DrawRect(new(0, 0, 1024, 576), new Color(0.52f, 0.65f, 0.75f));
            if (_world == null) return;
            for (int y = 0; y < 36; y++) for (int x = 0; x < 64; x++)
            {
                int terrain = _world.Terrain(x, y);
                if (terrain < 0) continue;
                var color = terrain == 7 ? new Color(0.42f, 0.68f, 0.2f) : terrain == 6 ? new Color(0.55f, 0.3f, 0.15f) : new Color(0.65f, 0.58f, 0.44f);
                DrawRect(new(x * 16, y * 16, 16, 16), color);
                DrawRect(new(x * 16 + 1, y * 16 + 1, 14, 14), color.Lightened(0.1f));
            }
        }
    }
}
