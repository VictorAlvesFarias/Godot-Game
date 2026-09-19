using Godot;
using Jogo25D.Light;
using System;
using System.Diagnostics;

namespace Jogo25D.Testing
{
    // Mede o que roda quando a regiao da camera se desloca: a reconstrucao do campo de acesso ao ceu
    // e a da geometria de sombra. Sao as duas coisas nao orcadas por frame no caminho do sol.
    public partial class RegionShiftProbe : Node
    {
        public override void _Ready()
        {
            try
            {
                var world = new LogicalLightWorld(0, "shift", 1, false) { DepthLightEnabled = true };
                // Terreno parecido com o da Upsidedown: superficie ondulada e macico abaixo.
                var rng = new RandomNumberGenerator { Seed = 7 };
                for (int x = -120; x < 200; x++)
                {
                    int topo = -20 + (int)(6 * Mathf.Sin(x * 0.11f)) + rng.RandiRange(0, 2);
                    for (int y = topo; y < 60; y++) world.SetTerrain(x, y, 0);
                    for (int y = topo + 3; y < 60; y++) world.SetBackground(x, y, true);
                }

                // Regiao do runtime: view em tiles arredondada para chunk, com 3 chunks de folga.
                var size = new Vector2I(((120 + 31) / 32 + 3) * 32, ((68 + 31) / 32 + 3) * 32);
                GD.Print("REGIAO " + size + " = " + (size.X * size.Y) + " celulas");

                var geometry = new AnalyticShadowGeometry();
                for (int passo = 0; passo < 5; passo++)
                {
                    var origin = new Vector2I(-64 + passo * 32, -64);

                    var relogio = Stopwatch.StartNew();
                    var bytes = SkyAccessField.Build(world, origin, size, 3);
                    relogio.Stop();
                    double campo = relogio.Elapsed.TotalMilliseconds;

                    relogio.Restart();
                    geometry.Begin(world, origin, size, -10, 0.5f);
                    int fatias = 0;
                    while (!geometry.Complete) { geometry.Process(3); fatias++; }
                    relogio.Stop();

                    GD.Print($"  origem {origin,-14} campo de ceu = {campo,6:F2} ms" +
                        $"   geometria = {relogio.Elapsed.TotalMilliseconds,6:F2} ms em {fatias} fatias de 3 ms" +
                        $"   ({bytes.Length / 4} texels)");
                }
                GD.Print("SHIFT OK");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
