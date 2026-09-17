using Godot;
using Jogo25D.Biomes;
using Jogo25D.Constants;
using Jogo25D.Light;
using System;

namespace Jogo25D.Testing
{
    // Roda os dois caminhos sobre a mesma cena e compara celula a celula. O porte para GPU so vale
    // se o grid bater com o da CPU dentro da precisao de half float.
    public partial class LightPortGpuCheck : Node
    {
        public override void _Ready()
        {
            try
            {
                var packed = GD.Load<PackedScene>("res://Scenes/World/Levels/Upsidedown.tscn");
                var root = packed.Instantiate<Node2D>(PackedScene.GenEditState.Disabled);
                var compose = root.GetNode<TerrainLayer>("Compose");
                var baseLayer = root.GetNode<TerrainLayer>("Base");

                var used = compose.GetUsedRect().Merge(baseLayer.GetUsedRect());
                int padding = LightingConstants.CHUNK_PADDING;
                var region = new Rect2I(used.Position - new Vector2I(padding, padding),
                    used.Size + new Vector2I(padding * 2, padding * 2));

                bool IsSolid(Vector2I cell) =>
                    compose.GetCellSourceId(cell) != -1 || baseLayer.GetCellSourceId(cell) != -1;

                var index = LightSourceScanner.BuildLightEmittingBlockIndex();
                var sources = LightSourceScanner.CollectSources(compose, baseLayer, region, index, IsSolid);

                ulong marca = Time.GetTicksUsec();
                var cpu = LightPropagationSystem.Compute(region, IsSolid, sources);
                double msCpu = (Time.GetTicksUsec() - marca) / 1000.0;

                using var gpu = new LightPropagationGpu();
                marca = Time.GetTicksUsec();
                var primeiro = gpu.Compute(region, IsSolid, sources);
                double msPrimeiro = (Time.GetTicksUsec() - marca) / 1000.0;
                marca = Time.GetTicksUsec();
                var grid = gpu.Compute(region, IsSolid, sources);
                double msGpu = (Time.GetTicksUsec() - marca) / 1000.0;

                GD.Print("REGIAO " + region.Size + "  fontes=" + sources.Count + "  iteracoes=" + LightPropagationGpu.Iterations);
                GD.Print("CPU " + msCpu.ToString("F1") + " ms   GPU " + msGpu.ToString("F1")
                    + " ms (primeira " + msPrimeiro.ToString("F1") + " ms)");

                float pior = 0;
                Vector2I onde = default;
                int acima001 = 0;
                double somaCpu = 0, somaGpu = 0;
                for (int x = 0; x < region.Size.X; x++)
                for (int y = 0; y < region.Size.Y; y++)
                {
                    var a = cpu[x, y];
                    var b = grid[x, y];
                    somaCpu += a.R; somaGpu += b.R;
                    float d = Mathf.Max(Mathf.Abs(a.R - b.R), Mathf.Max(Mathf.Abs(a.G - b.G), Mathf.Abs(a.B - b.B)));
                    if (d > 0.001f) acima001++;
                    if (d > pior) { pior = d; onde = new Vector2I(x, y); }
                }

                GD.Print("DIFERENCA maxima=" + pior.ToString("F5") + " em " + onde
                    + "   celulas acima de 0.001 = " + acima001 + " de " + (region.Size.X * region.Size.Y));
                GD.Print("SOMA_R cpu=" + somaCpu.ToString("F3") + "  gpu=" + somaGpu.ToString("F3"));
                GD.Print(pior <= 0.002f ? "IGUAL" : "DIVERGENTE");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
