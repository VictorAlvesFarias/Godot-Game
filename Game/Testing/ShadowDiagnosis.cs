using Godot;
using Jogo25D.Light;
using System;
namespace Jogo25D.Testing
{
    // Documenta o comportamento com TODAS as regras removidas: a sombra projeta tudo sobre tudo.
    //   A) parede alta ao lado de chao plano projeta igual no ar e no solido.
    //   C) escada descendente volta a se auto-sombrear - esperado agora, era o preco das regras.
    public partial class ShadowDiagnosis : Node
    {
        public override async void _Ready()
        {
            try
            {
                const int PX = 16;
                var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/Shaders/projected_shadow_debug.gdshader") };
                var pass = new SubViewport { Size = new(512, 512), Disable3D = true,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                AddChild(pass);
                pass.AddChild(new ColorRect { Size = pass.Size, Color = Colors.White });
                pass.AddChild(new ColorRect { Size = pass.Size, Material = material });
                material.SetShaderParameter("map_size", new Vector2(64, 64));
                material.SetShaderParameter("geometry_ready", true);
                var geometry = new AnalyticShadowGeometry();

                var plain = new LogicalLightWorld(0, "wall", 1, false);
                for (int y = 20; y < 32; y++) for (int x = 0; x < 32; x++) plain.SetTerrain(x, y, 0);
                for (int y = 4; y < 26; y++) for (int x = 12; x < 15; x++) plain.SetTerrain(x, y, 0);
                foreach (float aperture in new[] { 0f, 1f })
                {
                    geometry.Begin(plain, Vector2I.Zero, new(32, 32), -30, aperture);
                    while (!geometry.Complete) geometry.Process(10000);
                    material.SetShaderParameter("shadow_geometry", ImageTexture.CreateFromImage(geometry.Image()));
                    material.SetShaderParameter("sun_angle", Mathf.DegToRad(-30));
                    material.SetShaderParameter("penumbra", aperture);
                    for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var frame = pass.GetTexture().GetImage();
                    frame.SavePng(ProjectSettings.GlobalizePath($"res://../.images/sem-regras-parede-{aperture}.png"));
                    int ar = 0, solido = 0, total = 0;
                    for (int tx = 16; tx < 26; tx++)
                    {
                        total++;
                        if (frame.GetPixel(tx * PX + PX / 2, 19 * PX + PX / 2).R < 0.5f) ar++;
                        if (frame.GetPixel(tx * PX + PX / 2, 21 * PX + PX / 2).R < 0.5f) solido++;
                    }
                    GD.Print($"A abertura={aperture}: de {total} colunas, ar sombreado={ar}, solido sombreado={solido}");
                }

                var stairs = new LogicalLightWorld(0, "stairs", 1, false);
                for (int x = 8; x < 24; x++) for (int y = 6 + x - 8; y < 30; y++) stairs.SetTerrain(x, y, 0);
                foreach (float aperture in new[] { 0f, 1f })
                {
                    geometry.Begin(stairs, Vector2I.Zero, new(32, 32), -10, aperture);
                    while (!geometry.Complete) geometry.Process(10000);
                    material.SetShaderParameter("shadow_geometry", ImageTexture.CreateFromImage(geometry.Image()));
                    material.SetShaderParameter("sun_angle", Mathf.DegToRad(-10));
                    material.SetShaderParameter("penumbra", aperture);
                    for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var frame = pass.GetTexture().GetImage();
                    frame.SavePng(ProjectSettings.GlobalizePath($"res://../.images/sem-regras-escada-{aperture}.png"));
                    int escuros = 0, total = 0;
                    for (int x = 8 * PX; x < 24 * PX; x++)
                        for (int y = (6 + x / PX - 8) * PX; y < 30 * PX; y++)
                        { total++; if (frame.GetPixel(x, y).R < 0.99f) escuros++; }
                    GD.Print($"C abertura={aperture}: auto-sombra na rampa={escuros} de {total} pixels");
                }
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
