using Godot;
using System;

namespace Jogo25D.Testing
{
    // Renderiza apenas as tres camadas de tile da Upsidedown, sem subir o jogo.
    public partial class UpsidedownPreview : Node
    {
        public override async void _Ready()
        {
            try
            {
                var packed = GD.Load<PackedScene>(System.Environment.GetEnvironmentVariable("CENA") ?? "res://Scenes/World/Levels/Upsidedown.tscn");
                var root = packed.Instantiate<Node2D>(PackedScene.GenEditState.Disabled);
                var pass = new SubViewport { Size = new(1920, 720), Disable3D = true, TransparentBg = false,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                AddChild(pass);
                pass.AddChild(new ColorRect { Size = pass.Size, Color = new Color(0.55f, 0.72f, 0.9f) });
                foreach (var nome in new[] { "BackgroundWalls", "Base", "Compose" })
                {
                    var layer = root.GetNode<TileMapLayer>(nome);
                    root.RemoveChild(layer);
                    pass.AddChild(layer);
                }
                var camera = new Camera2D { Zoom = new Vector2(0.55f, 0.55f), Position = new Vector2(0, -300), Enabled = true };
                pass.AddChild(camera);
                camera.MakeCurrent();
                for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var frame = pass.GetTexture().GetImage();
                frame.SavePng(ProjectSettings.GlobalizePath("res://../.images/" + (System.Environment.GetEnvironmentVariable("PREFIXO") ?? "upsidedown") + "-geral.png"));

                camera.Zoom = new Vector2(3.0f, 3.0f);
                foreach (var alvo in new (string Nome, Vector2 Onde)[] {
                    ("torres", new Vector2(-16 * 16 + 100, -22 * 16 - 60)),
                    ("arvores", new Vector2(10 * 16, -22 * 16 - 40)),
                    ("barranco", new Vector2(-62 * 16, -12 * 16)) })
                {
                    camera.Position = alvo.Onde;
                    for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var recorte = pass.GetTexture().GetImage();
                    recorte.SavePng(ProjectSettings.GlobalizePath("res://../.images/"
                        + (System.Environment.GetEnvironmentVariable("PREFIXO") ?? "upsidedown") + "-" + alvo.Nome + ".png"));
                }
                GD.Print("PREVIA OK");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
