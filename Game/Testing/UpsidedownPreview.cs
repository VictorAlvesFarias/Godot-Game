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
                var packed = GD.Load<PackedScene>("res://Scenes/World/Levels/Upsidedown.tscn");
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
                frame.SavePng(ProjectSettings.GlobalizePath("res://../.images/upsidedown-geral.png"));

                camera.Zoom = new Vector2(2.2f, 2.2f);
                foreach (var alvo in new (string Nome, Vector2 Onde)[] {
                    ("casa-fechada", new Vector2(-72 * 16 + 130, -21 * 16 - 40)),
                    ("casa-porta",   new Vector2(-14 * 16 + 130, -21 * 16 - 40)),
                    ("casa-janela",  new Vector2( 48 * 16 + 130, -21 * 16 - 40)),
                    ("caverna",      new Vector2(-40 * 16, -4 * 16)) })
                {
                    camera.Position = alvo.Onde;
                    for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var recorte = pass.GetTexture().GetImage();
                    recorte.SavePng(ProjectSettings.GlobalizePath($"res://../.images/upsidedown-{alvo.Nome}.png"));
                }
                GD.Print("PREVIA OK");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
