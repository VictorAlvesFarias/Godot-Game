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
                var depthWorld = new LogicalLightWorld(0, "depth", 1, false) { DepthLightEnabled = true };
                for (int y = 0; y <= 12; y++) for (int x = 0; x <= 12; x++)
                    if (x == 0 || y == 0 || x == 12 || y == 12) depthWorld.SetTerrain(x,y,0);
                depthWorld.Field.SetRegion(0,0,64,36); Settle(depthWorld.Field);
                Check(depthWorld.Field.Get(6,6).Sky == 255, "Closed outline without background must receive depth light");
                for (int y = 1; y < 12; y++) for (int x = 1; x < 12; x++) depthWorld.SetBackground(x,y,true);
                Settle(depthWorld.Field);
                Check(depthWorld.Field.Get(6,6).Sky == 0, "Closed background retained depth light");
                depthWorld.SetBackground(6,6,false); Settle(depthWorld.Field);
                Check(depthWorld.Field.Get(6,6).Sky == 255 && depthWorld.Field.Get(8,6).Sky > 0,
                    "Depth opening failed to propagate sideways");
                depthWorld.SetBackground(6,6,true); Settle(depthWorld.Field);
                Check(depthWorld.Field.Get(8,6).Sky == 0, "Closing depth opening left stale indirect light");
                depthWorld.Field.SetRegion(1000,1000,32,32); Settle(depthWorld.Field);
                depthWorld.Field.SetRegion(0,0,64,36); Settle(depthWorld.Field);
                Check(depthWorld.Field.Get(6,6).Sky == 0, "Cache reconstruction lost logical background");
                GD.Print("DEPTH LIGHT PASS: open back, closed back, incremental propagation/removal, cache reconstruction");
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
                    var solid = new LogicalLightWorld(0, "terrain-depth", 1, false);
                    for (int x = 0; x < 64; x++) for (int y = 10; y < 36; y++) solid.SetTerrain(x, y, 0);
                    solid.Field.SetRegion(0, 0, 64, 36); Settle(solid.Field);
                    foreach (float transition in new[] { 1f, 6f })
                    {
                        raster.Begin(solid, Vector2I.Zero, new(64, 36), 0, 1, transition);
                        while (!raster.Complete) raster.Process(10000);
                        using var fieldImage = raster.LightImage();
                        float ambientAtDepth = fieldImage.GetPixel(32 * 2, 13 * 2).R;
                        Check(fieldImage.GetPixel(32 * 2, 10 * 2).R < 1,
                            "Terrain transition has an unattenuated plateau after the surface");
                        Check(transition == 1 ? ambientAtDepth == 0 : ambientAtDepth > 0.1,
                            "Terrain transition did not update ambient surface reception");
                        Check(fieldImage.GetPixel(32 * 2, 17 * 2).R == 0, "Ambient exceeds requested terrain depth");
                        material.SetShaderParameter("light_data", ImageTexture.CreateFromImage(fieldImage));
                        material.SetShaderParameter("shadow_geometry", ImageTexture.CreateFromImage(raster.ShadowGeometryImage()));
                        material.SetShaderParameter("sun_angle", 0f);
                        material.SetShaderParameter("penumbra", 1f);
                        material.SetShaderParameter("terrain_transition_tiles", transition);
                        pass.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
                        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        using var surface = pass.GetTexture().GetImage();
                        float sunAtDepth = surface.GetPixel(32 * LightMapComputer.ShadowSubdivisions, 13 * LightMapComputer.ShadowSubdivisions).R;
                        Check(transition == 1 ? sunAtDepth == 0 : sunAtDepth > 0.1, "Terrain transition did not update solar reception");
                        Check(surface.GetPixel(32 * LightMapComputer.ShadowSubdivisions, 17 * LightMapComputer.ShadowSubdivisions).R == 0,
                            "Sun exceeds requested terrain depth");
                    }
                    GD.Print("TERRAIN TRANSITION PASS: 1 and 6 tiles, ambient and sun");
                    // Identical opaque silhouette, different rectangle decomposition.
                    // The fixed response must not reveal the internal checkerboard.
                    Image unifiedShadow = null;
                    for (int variant = 0; variant < 2; variant++)
                    {
                        if (variant == 1)
                            for (int x = 24; x < 32; x++) for (int y = 6; y < 14; y++)
                                blocker.SetTerrain(x, y, (x + y) % 2 == 0 ? 0 : 6);
                        Settle(blocker.Field);
                        raster.Begin(blocker, Vector2I.Zero, new(64, 36), 0, 0, 3, 3);
                        while (!raster.Complete) raster.Process(10000);
                        material.SetShaderParameter("light_data", ImageTexture.CreateFromImage(raster.LightImage()));
                        material.SetShaderParameter("shadow_geometry", ImageTexture.CreateFromImage(raster.ShadowGeometryImage()));
                        material.SetShaderParameter("penumbra", 0f);
                        material.SetShaderParameter("terrain_transition_tiles", 3f);
                        pass.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
                        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        var variantImage = pass.GetTexture().GetImage();
                        if (variant == 0) unifiedShadow = variantImage;
                        else
                        {
                            int scale = LightMapComputer.ShadowSubdivisions;
                            for (int x = 16 * scale; x < 42 * scale; x++) for (int y = 15 * scale; y < 34 * scale; y++)
                                Check(Math.Abs(variantImage.GetPixel(x, y).R - unifiedShadow.GetPixel(x, y).R) < 0.008,
                                    "Rectangle decomposition created internal penumbra seams");
                            variantImage.Dispose(); unifiedShadow.Dispose();
                        }
                    }
                    GD.Print("SILHOUETTE UNION PASS: solid rectangle matches checkerboard decomposition with fixed response");
                    using (var baseline = pass.GetTexture().GetImage())
                    {
                        int middleBand = 0, fullBand = 0;
                        for (int x = 28 * 4; x < 42 * 4; x++)
                        {
                            float v = baseline.GetPixel(x, 25 * 4).R;
                            if (v > 0.05 && v < 0.95) fullBand++;
                            if (v > 0.48 && v < 0.52) middleBand++;
                        }
                        Check(middleBand <= Math.Max(3, fullBand / 6),
                            "Full-width penumbra flattened around its midpoint");

                        foreach (string edge in new[] { "shadow", "ambient" })
                        foreach (float setting in new[] { 0f, 0.25f, 0.5f })
                        {
                            material.SetShaderParameter("penumbra_" + edge + "_transition", setting);
                            pass.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
                            for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                            using var adjusted = pass.GetTexture().GetImage();
                            int changed = 0, nonOverlayPixels = 0;
                            for (int x = 28 * 4; x < 42 * 4; x++)
                            {
                                float before = baseline.GetPixel(x, 25 * 4).R;
                                float after = adjusted.GetPixel(x, 25 * 4).R;
                                Check(float.IsFinite(after), "Invalid transition intensity");
                                // Image is 8-bit: values close to an endpoint can quantize to it.
                                // Check the exterior away from that quantization band.
                                if ((before == 0f || before == 1f)
                                    && baseline.GetPixel(x - 2, 25 * 4).R == before
                                    && baseline.GetPixel(x + 2, 25 * 4).R == before)
                                    Check(Math.Abs(after - before) < 0.008,
                                        "Intensity control changed the exterior of the penumbra");
                                if (setting > 0f && before > 0.01f && before < 0.49f)
                                    Check(after > 0f && after < 0.5f, "Shadow gradient collapsed into a plateau");
                                if (setting > 0f && before > 0.51f && before < 0.99f)
                                    Check(after > 0.5f && after < 1f, "Ambient gradient collapsed into a plateau");
                                bool target = edge == "shadow" ? before < 0.5f : before > 0.5f;
                                if (!target) Check(Math.Abs(before - after) < 0.008, "Edge control changed the opposite half");
                                else if (Math.Abs(before - after) > 0.02) changed++;
                                if (target && setting == 0.5f && before > 0.1f && before < 0.9f)
                                {
                                    // The rejected overlay was halfway between the old smooth
                                    // profile and the uniform half-lit region at this setting.
                                    float overlay = 0.5f * before + 0.25f;
                                    if (Math.Abs(after - overlay) > 0.008f) nonOverlayPixels++;
                                }
                            }
                            Check(changed > 2, "Edge control did not shape its transition");
                            if (setting == 0.5f) Check(nonOverlayPixels > 0,
                                "Transition is still an opacity blend instead of a variable-hardness curve");
                            material.SetShaderParameter("penumbra_" + edge + "_transition", 1f);
                        }
                    }
                    material.SetShaderParameter("penumbra_shadow_transition", 0f);
                    material.SetShaderParameter("penumbra_ambient_transition", 0f);
                    pass.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
                    for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using (var hard = pass.GetTexture().GetImage())
                    {
                        int ramp = 0, plateau = 0;
                        for (int x = 28 * 4; x < 42 * 4; x++)
                        {
                            float v = hard.GetPixel(x, 25 * 4).R;
                            if (Math.Abs(v - 0.5f) < 0.008) plateau++;
                            else if (v > 0.008 && v < 0.992) ramp++;
                        }
                        Check(plateau > 2 && ramp <= 4, "Zero controls still produce a broad gradient");
                    }
                    material.SetShaderParameter("penumbra_shadow_transition", 1f);
                    material.SetShaderParameter("penumbra_ambient_transition", 1f);
                    GD.Print("INDEPENDENT PENUMBRA EDGES PASS");
                    raster.Begin(solid, Vector2I.Zero, new(64, 36), 0, 1, 6);
                    while (!raster.Complete) raster.Process(10000);
                    present.SetShaderParameter("light_data", ImageTexture.CreateFromImage(raster.LightImage()));
                    present.SetShaderParameter("emission_data", ImageTexture.CreateFromImage(raster.EmissionImage()));
                    present.SetShaderParameter("shadow_geometry", ImageTexture.CreateFromImage(raster.ShadowGeometryImage()));
                    present.SetShaderParameter("sun_angle", 0f);
                    present.SetShaderParameter("penumbra", 1f);
                    present.SetShaderParameter("terrain_transition_tiles", 6f);
                    present.SetShaderParameter("ambient_energy", 0.5f);
                    present.SetShaderParameter("sun_energy", 1.03f);
                    present.SetShaderParameter("sky_color", Colors.White);
                    for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var daylight = filteredPass.GetTexture().GetImage();
                    float shallow = daylight.GetPixel(32 * 32, 11 * 32).R;
                    float middle = daylight.GetPixel(32 * 32, 13 * 32).R;
                    Check(shallow < 0.99 && shallow > middle + 0.15, "Daylight saturation erased the terrain gradient");
                    Check(daylight.GetPixel(32 * 32, 17 * 32).R < 0.004, "Daylight normalization lifted complete darkness");
                    foreach (bool opening in new[] { false, true, false })
                    {
                        depthWorld.SetBackground(6,6,!opening); Settle(depthWorld.Field);
                        raster.Begin(depthWorld,Vector2I.Zero,new(64,36),0,1);
                        while (!raster.Complete) raster.Process(10000);
                        present.SetShaderParameter("light_data",ImageTexture.CreateFromImage(raster.LightImage()));
                        present.SetShaderParameter("emission_data",ImageTexture.CreateFromImage(raster.EmissionImage()));
                        present.SetShaderParameter("shadow_geometry",ImageTexture.CreateFromImage(raster.ShadowGeometryImage()));
                        present.SetShaderParameter("ambient_energy",1f);
                        present.SetShaderParameter("sun_energy",0f);
                        for (int i = 0; i < 3; i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        using var depthFrame = filteredPass.GetTexture().GetImage();
                        float brightness = depthFrame.GetPixel(6*32+16,6*32+16).R;
                        Check(opening ? brightness > 0.8f : brightness < 0.008f,
                            "Final GPU composition does not reflect depth opening");
                    }
                    present.SetShaderParameter("depth_beam_enabled",true);
                    present.SetShaderParameter("ambient_energy",0f);
                    present.SetShaderParameter("sun_energy",1f);
                    present.SetShaderParameter("sun_angle",0f);
                    present.SetShaderParameter("penumbra",1f);
                    foreach (int state in new[] { 0, 1, 2, 3 })
                    {
                        depthWorld.SetBackground(6,4,state == 1);
                        depthWorld.SetTerrain(6,7,state == 2 ? 0 : -1);
                        Settle(depthWorld.Field);
                        raster.Begin(depthWorld,Vector2I.Zero,new(64,36),0,1);
                        while (!raster.Complete) raster.Process(10000);
                        present.SetShaderParameter("depth_beam_data",ImageTexture.CreateFromImage(raster.DepthBeamImage()));
                        present.SetShaderParameter("light_data",ImageTexture.CreateFromImage(raster.LightImage()));
                        present.SetShaderParameter("shadow_geometry",ImageTexture.CreateFromImage(raster.ShadowGeometryImage()));
                        present.SetShaderParameter("sun_angle",state == 3 ? Mathf.DegToRad(35) : 0f);
                        for (int i = 0; i < 3; i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                        using var beamFrame = filteredPass.GetTexture().GetImage();
                        float inside = beamFrame.GetPixel(6*32+16,9*32+16).R;
                        float outside = beamFrame.GetPixel(3*32+16,9*32+16).R;
                        Check(state == 0 ? inside > 0.3f : inside < 0.01f,
                            "Window beam failed projection, closure, obstacle or angle check");
                        if (state == 0) Check(outside < 0.01f,"Window beam illuminates off-axis receiver");
                        if (state == 0) beamFrame.SavePng(folder + "/lighting-window-beam.png");
                    }
                    present.SetShaderParameter("shadow_air",0.35f);
                    present.SetShaderParameter("volumetric_reach",24f);
                    present.SetShaderParameter("sun_angle",0f);
                    var volume = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/Shaders/window_volume.gdshader") };
                    foreach (bool opening in new[] { false, true, false })
                    {
                        depthWorld.SetBackground(6,4,!opening);
                        depthWorld.SetTerrain(6,7,-1);
                        depthWorld.SetTerrain(20,0,0);
                        Settle(depthWorld.Field);
                        raster.Begin(depthWorld,Vector2I.Zero,new(64,36),0,1);
                        while (!raster.Complete) raster.Process(10000);
                        present.SetShaderParameter("depth_beam_data",ImageTexture.CreateFromImage(raster.DepthBeamImage()));
                        present.SetShaderParameter("light_data",ImageTexture.CreateFromImage(raster.LightImage()));
                        present.SetShaderParameter("shadow_geometry",ImageTexture.CreateFromImage(raster.ShadowGeometryImage()));
                        var surface = filteredPass.GetChild<Sprite2D>(1);
                        var backdrop = filteredPass.GetChild<ColorRect>(0);
                        surface.Material = present;
                        backdrop.Color = Colors.White;
                        for (int i = 0; i < 3; i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                        using (var frame = filteredPass.GetTexture().GetImage())
                        {
                            float room = frame.GetPixel(6*32+16,9*32+16).R;
                            Check(opening ? room > 0.3f : room < 0.01f,"Layered composition leaked through a distant closed roof");
                            // Ceu aberto agora recebe a sombra projetada com peso, nao zero: o que
                            // se pede e forca reduzida, nao ausencia. Lit sky continua intacto.
                            float shaded = frame.GetPixel(20*32+16,9*32+16).R;
                            Check(Math.Abs(shaded - 0.65f) < 0.06f,
                                $"Open-air projected shadow ignored its weight (got {shaded:0.000}, expected ~0.65)");
                            Check(frame.GetPixel(30*32+16,9*32+16).R > 0.99f,"Open-air weight darkened lit sky");
                        }
                        foreach (var uniform in present.Shader.GetShaderUniformList())
                        {
                            string name = uniform.AsGodotDictionary()["name"].AsString();
                            volume.SetShaderParameter(name,present.GetShaderParameter(name));
                        }
                        volume.SetShaderParameter("volume_density",0.5f);
                        surface.Material = volume;
                        backdrop.Color = Colors.Black;
                        for (int i = 0; i < 3; i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                        using var fog = filteredPass.GetTexture().GetImage();
                        float shaft = fog.GetPixel(6*32+16,9*32+16).R;
                        Check(opening ? shaft > 0.1f : shaft < 0.01f,"Window volume did not follow aperture closure");
                        Check(fog.GetPixel(20*32+16,9*32+16).R < 0.01f,"Volume contaminated open sky");
                    }
                    GD.Print("LAYERED GPU PASS: weighted open-air shadow, lit sky unchanged, closed room dark, additive volume follows opening");
                    GD.Print("WINDOW BEAM GPU PASS: projected shaft, off-axis darkness, closure, obstacle and sun angle");
                    GD.Print("DEPTH GPU PASS: background close/open/close reaches final composition");
                    GD.Print("FINAL GRADIENT PASS: softness preserves midpoint; 0.5 ambient + 1.03 sun has no white plateau");
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
                // Measure the outer projection (5% shadow), independent of its inner tone.
                if (value < 0.995f) count++; // Detect geometry beyond the high-contrast core.
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
