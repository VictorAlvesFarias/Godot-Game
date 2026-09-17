using Godot;
using Jogo25D.Biomes;
using Jogo25D.Constants;
using Jogo25D.Light;
using System;

namespace Jogo25D.Testing
{
    // Renderiza a Upsidedown com a luz da copia aplicada do mesmo jeito que o LightingEditorPreview
    // faz: grid de luz 1px por tile com filtro linear, mascara de cobertura em resolucao de pixel e
    // o TerrainLightOverlay por cima, multiplicativo. Serve pra comparar com o print do original.
    public partial class LightPortRender : Node
    {
        public override async void _Ready()
        {
            try
            {
                bool usarGpu = OS.GetCmdlineUserArgs().Length > 0 && OS.GetCmdlineUserArgs()[0] == "--gpu";
                var packed = GD.Load<PackedScene>("res://Scenes/World/Levels/Upsidedown.tscn");
                var root = packed.Instantiate<Node2D>(PackedScene.GenEditState.Disabled);
                var compose = root.GetNode<TerrainLayer>("Compose");
                var baseLayer = root.GetNode<TerrainLayer>("Base");
                var walls = root.GetNode<TileMapLayer>("BackgroundWalls");

                var pass = new SubViewport
                {
                    Size = new Vector2I(1600, 900),
                    Disable3D = true,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                };
                AddChild(pass);
                pass.AddChild(new ColorRect { Size = pass.Size, Color = new Color(0.55f, 0.72f, 0.9f) });

                var mundo = new Node2D { Name = "Mundo" };
                pass.AddChild(mundo);
                foreach (var layer in new TileMapLayer[] { walls, baseLayer, compose })
                {
                    root.RemoveChild(layer);
                    mundo.AddChild(layer);
                }

                var used = compose.GetUsedRect().Merge(baseLayer.GetUsedRect());
                int padding = LightingConstants.CHUNK_PADDING;
                var region = new Rect2I(used.Position - new Vector2I(padding, padding),
                    used.Size + new Vector2I(padding * 2, padding * 2));

                bool IsSolid(Vector2I cell) =>
                    compose.GetCellSourceId(cell) != -1 || baseLayer.GetCellSourceId(cell) != -1;

                var index = LightSourceScanner.BuildLightEmittingBlockIndex();
                var sources = LightSourceScanner.CollectSources(compose, baseLayer, region, index, IsSolid);

                Color[,] grid;
                if (usarGpu)
                {
                    using var gpu = new LightPropagationGpu();
                    grid = gpu.Compute(region, IsSolid, sources);
                }
                else
                {
                    grid = LightPropagationSystem.Compute(region, IsSolid, sources);
                }

                var image = Image.CreateEmpty(region.Size.X, region.Size.Y, false, Image.Format.Rgba8);
                for (int x = 0; x < region.Size.X; x++)
                for (int y = 0; y < region.Size.Y; y++)
                {
                    var v = grid[x, y];
                    image.SetPixel(x, y, new Color(
                        Mathf.Max(v.R, LightingConstants.AMBIENT_MIN),
                        Mathf.Max(v.G, LightingConstants.AMBIENT_MIN),
                        Mathf.Max(v.B, LightingConstants.AMBIENT_MIN)));
                }

                int tileSize = compose.TileSet.TileSize.X;
                var sprite = new Sprite2D
                {
                    Name = "PreviewSprite",
                    Centered = false,
                    TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                    Material = new ShaderMaterial
                    {
                        Shader = GD.Load<Shader>("res://Features/World/Light/Resources/TerrainLightOverlay.gdshader"),
                    },
                    Position = new Vector2(region.Position.X * tileSize, region.Position.Y * tileSize),
                    Scale = new Vector2(tileSize, tileSize),
                    Texture = ImageTexture.CreateFromImage(image),
                };
                mundo.AddChild(sprite);

                ImageTexture mascara = null;
                using (var mask = TerrainLightMask.Build(
                    new Rect2I(region.Position * tileSize, region.Size * tileSize), mundo, baseLayer, compose, walls))
                {
                    TerrainLightMask.UpdateMaterial((ShaderMaterial)sprite.Material, ref mascara, mask);
                }

                var camera = new Camera2D { Zoom = new Vector2(1.4f, 1.4f), Enabled = true };
                mundo.AddChild(camera);
                camera.MakeCurrent();

                string prefixo = usarGpu ? "porte-gpu" : "porte-cpu";
                foreach (var alvo in new (string Nome, Vector2 Onde)[]
                {
                    ("geral", new Vector2(0, -22 * tileSize)),
                    ("arvores", new Vector2(10 * tileSize, -24 * tileSize)),
                    ("torre", new Vector2(-16 * tileSize, -26 * tileSize)),
                })
                {
                    camera.Position = alvo.Onde;
                    camera.Zoom = alvo.Nome == "geral" ? new Vector2(0.7f, 0.7f) : new Vector2(2.0f, 2.0f);
                    for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var frame = pass.GetTexture().GetImage();
                    frame.SavePng(ProjectSettings.GlobalizePath(
                        "res://../.images/" + prefixo + "-" + alvo.Nome + ".png"));
                    GD.Print("GRAVADO " + prefixo + "-" + alvo.Nome + ".png");
                }

                GD.Print("RENDER OK");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
