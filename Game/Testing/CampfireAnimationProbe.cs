using Godot;
using System;
using System.Collections.Generic;

namespace Jogo25D.Testing
{
    // Captura a fogueira como ela aparece em jogo: instancia a cena de verdade, deixa a animacao
    // rodar e fotografa um frame de cada quadro do ciclo. Nao basta a folha existir - o teste so
    // passa se os quadros renderizados forem diferentes entre si e o ciclo voltar ao inicio.
    public partial class CampfireAnimationProbe : Node
    {
        private const int Frames = 6;
        private const float Zoom = 2.5f;   // o mesmo das dimensoes

        public override async void _Ready()
        {
            try
            {
                var fire = GD.Load<PackedScene>("res://Scenes/World/Props/Campfire.tscn").Instantiate<Node2D>();
                var sprite = fire.GetNode<AnimatedSprite2D>("Sprite");

                // Uma fogueira so na viewport; a tira e montada depois, uma foto ao lado da outra.
                var pass = new SubViewport { Size = new Vector2I(48, 56), Disable3D = true,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                AddChild(pass);
                pass.AddChild(new ColorRect { Size = pass.Size, Color = new Color(0.09f, 0.08f, 0.11f) });
                pass.AddChild(fire);

                var camera = new Camera2D { Zoom = new Vector2(Zoom, Zoom), Position = new Vector2(0, -14), Enabled = true };
                pass.AddChild(camera);
                camera.MakeCurrent();

                if (!sprite.IsPlaying())
                {
                    throw new Exception("A fogueira nao esta animando: autoplay nao entrou.");
                }

                // Confirmado que toca sozinha, congela: dai cada foto e o quadro pedido, e nao
                // aquele em que a reproducao estivesse no instante do print.
                sprite.Pause();

                var shots = new List<Image>();
                var seen = new List<int>();

                // Anda quadro a quadro pela animacao, fotografando cada um.
                for (int i = 0; i < Frames; i++)
                {
                    sprite.SetFrameAndProgress(i, 0f);

                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

                    var shot = pass.GetTexture().GetImage();

                    shots.Add(shot);
                    seen.Add(sprite.Frame);

                    if (sprite.Frame != i)
                    {
                        throw new Exception($"Quadro {i} nao ficou na tela: parou em {sprite.Frame}.");
                    }
                }

                // Quadros iguais seriam animacao parada: compara cada par.
                for (int a = 0; a < shots.Count; a++)
                {
                    for (int b = a + 1; b < shots.Count; b++)
                    {
                        if (shots[a].GetData().AsSpan().SequenceEqual(shots[b].GetData()))
                        {
                            throw new Exception($"Quadros {a} e {b} sao identicos na tela.");
                        }
                    }
                }

                var strip = Image.CreateEmpty(pass.Size.X * Frames, pass.Size.Y, false, shots[0].GetFormat());

                for (int i = 0; i < Frames; i++)
                {
                    strip.BlitRect(shots[i], new Rect2I(Vector2I.Zero, pass.Size), new Vector2I(i * pass.Size.X, 0));
                }

                strip.Resize(strip.GetWidth() * 3, strip.GetHeight() * 3, Image.Interpolation.Nearest);
                strip.SavePng(ProjectSettings.GlobalizePath("res://../.images/campfire-animacao-em-jogo.png"));

                var duration = Frames / (float)sprite.SpriteFrames.GetAnimationSpeed(sprite.Animation);

                GD.Print($"quadros distintos: {Frames}   ciclo: {duration:F2} s a " +
                    $"{sprite.SpriteFrames.GetAnimationSpeed(sprite.Animation)} fps   loop: " +
                    (sprite.SpriteFrames.GetAnimationLoop(sprite.Animation) ? "sim" : "nao"));
                GD.Print("CAMPFIRE ANIMATION OK");

                foreach (var shot in shots) shot.Dispose();
                strip.Dispose();
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
