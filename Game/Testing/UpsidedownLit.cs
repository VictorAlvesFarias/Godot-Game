using Godot;
using Jogo25D.Light;
using System;
using System.Collections.Generic;

namespace Jogo25D.Testing
{
    // Renderiza a cena real com a sombra real: monta o mundo logico a partir da MESMA camada que o
    // LightMap2D usa (o export Layers) e aplica projected_shadow.gdshader por cima dos tiles.
    public partial class UpsidedownLit : Node
    {
        public override async void _Ready()
        {
            try
            {
                var packed = GD.Load<PackedScene>("res://Scenes/World/Levels/Upsidedown.tscn");
                var root = packed.Instantiate<Node2D>(PackedScene.GenEditState.Disabled);
                var lightMap = root.GetNode("LightMap");
                var caminhos = lightMap.Get("Layers").As<Godot.Collections.Array<NodePath>>();

                var pass = new SubViewport { Size = new(1400, 700), Disable3D = true,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                AddChild(pass);
                pass.AddChild(new ColorRect { Size = pass.Size, Color = new Color(0.62f, 0.78f, 0.93f) });

                var geometria = new List<TileMapLayer>();
                foreach (var caminho in caminhos)
                    if (lightMap.GetNodeOrNull<TileMapLayer>(caminho) is { } l) geometria.Add(l);

                var mundo = new LogicalLightWorld(0, "lit", 1, false);
                foreach (var layer in geometria)
                    foreach (var c in layer.GetUsedCells())
                        mundo.SetTerrain(c.X, c.Y, LogicalLightWorld.TileTerrain(layer, c));

                foreach (var nome in new[] { "BackgroundWalls", "Base", "Compose" })
                {
                    var layer = root.GetNode<TileMapLayer>(nome);
                    root.RemoveChild(layer);
                    pass.AddChild(layer);
                }

                var geometry = new AnalyticShadowGeometry();
                geometry.Begin(mundo, new Vector2I(-120, -40), new Vector2I(240, 90), -10, 0);
                while (!geometry.Complete) geometry.Process(10000);

                // Campo de luz, que e o que o layered_light desenha - e de onde vem o preto do miolo.
                mundo.Field.SetRegion(-120, -40, 240, 90);
                for (int i = 0; i < 20000 && !mundo.Field.Settled; i++) mundo.Field.Process(10000);
                float profundidade = (float)(GD.Load<Resource>("res://Assets/Data/LightMap.tres")
                    .Get("TerrainLightDepthTiles").AsDouble());
                var computer = new LightMapComputer();
                computer.Begin(mundo, new Vector2I(-120, -40), new Vector2I(240, 90), 0, 1, profundidade, buildShadow: false);
                while (!computer.Complete) computer.Process(10000);

                var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/Shaders/layered_light.gdshader") };
                material.SetShaderParameter("light_data", ImageTexture.CreateFromImage(computer.LightImage()));
                material.SetShaderParameter("emission_data", ImageTexture.CreateFromImage(computer.EmissionImage()));
                material.SetShaderParameter("shadow_geometry", ImageTexture.CreateFromImage(geometry.Image()));
                material.SetShaderParameter("map_size", new Vector2(240, 90) * 2);
                material.SetShaderParameter("sun_angle", Mathf.DegToRad(-10));
                material.SetShaderParameter("penumbra", 0f);
                material.SetShaderParameter("geometry_ready", true);
                GD.Print("profundidade usada = " + profundidade);
                var overlay = new Sprite2D
                {
                    Name = "Sombra", Centered = false, Material = material, ZIndex = 900,
                    Texture = ImageTexture.CreateFromImage(Image.CreateEmpty(1, 1, false, Image.Format.Rgb8)),
                    Position = new Vector2(-120 * 16, -40 * 16), Scale = new Vector2(240 * 16, 90 * 16),
                };
                pass.AddChild(overlay);

                var camera = new Camera2D { Zoom = new Vector2(1.4f, 1.4f), Position = new Vector2(-260, -300), Enabled = true };
                pass.AddChild(camera);
                camera.MakeCurrent();
                for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var frame = pass.GetTexture().GetImage();
                frame.SavePng(ProjectSettings.GlobalizePath("res://../.images/upsidedown-com-sombra.png"));
                // Campo de luz cru, em cinza, do mesmo recorte: defeito aparece sem a textura por cima.
                using (var campo = computer.LightImage())
                {
                    int w = campo.GetWidth(), h = campo.GetHeight();
                    var cinza = Image.CreateEmpty(w, h, false, Image.Format.Rgb8);
                    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                    {
                        float v = campo.GetPixel(x, y).R;
                        cinza.SetPixel(x, y, new Color(v, v, v));
                    }
                    cinza.SavePng(ProjectSettings.GlobalizePath("res://../.images/upsidedown-campo-de-luz.png"));
                    GD.Print("campo de luz " + w + "x" + h + " salvo");
                }
                GD.Print("LIT OK: camadas de geometria = " + geometria.Count
                    + (geometria.Count > 0 ? " (" + geometria[0].Name + ")" : ""));
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
