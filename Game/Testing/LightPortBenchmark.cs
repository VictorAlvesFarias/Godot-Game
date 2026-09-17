using Godot;
using Jogo25D.Biomes;
using Jogo25D.Constants;
using Jogo25D.Light;
using System;

namespace Jogo25D.Testing
{
    // Mede o custo real por chunk, do jeito que o LightingManager trabalha: regiao de chunk com
    // padding, flood-fill, e a mascara de pixel. Serve pra saber o que vale a pena mover pra GPU.
    public partial class LightPortBenchmark : Node
    {
        public override void _Ready()
        {
            try
            {
                var packed = GD.Load<PackedScene>("res://Scenes/World/Levels/Upsidedown.tscn");
                var root = packed.Instantiate<Node2D>(PackedScene.GenEditState.Disabled);
                AddChild(root);
                var compose = root.GetNode<TerrainLayer>("Compose");
                var baseLayer = root.GetNode<TerrainLayer>("Base");

                int chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
                int padding = LightingConstants.CHUNK_PADDING;
                int tileSize = compose.TileSet.TileSize.X;
                var used = compose.GetUsedRect().Merge(baseLayer.GetUsedRect());
                var primeiroChunk = new Vector2I(
                    Mathf.FloorToInt(used.Position.X / (float)chunkSize) + 1,
                    Mathf.FloorToInt(used.Position.Y / (float)chunkSize) + 1);

                bool IsSolid(Vector2I cell) =>
                    compose.GetCellSourceId(cell) != -1 || baseLayer.GetCellSourceId(cell) != -1;

                var index = LightSourceScanner.BuildLightEmittingBlockIndex();
                using var gpu = new LightPropagationGpu();

                double totalScan = 0, totalCpu = 0, totalGpu = 0, totalMascara = 0;
                int chunks = 0;
                for (int cx = 0; cx < 4; cx++)
                for (int cy = 0; cy < 3; cy++)
                {
                    var chunkCoord = primeiroChunk + new Vector2I(cx, cy);
                    var origem = chunkCoord * chunkSize;
                    var region = new Rect2I(origem - new Vector2I(padding, padding),
                        new Vector2I(chunkSize + padding * 2, chunkSize + padding * 2));

                    ulong m = Time.GetTicksUsec();
                    var sources = LightSourceScanner.CollectSources(compose, baseLayer, region, index, IsSolid);
                    totalScan += (Time.GetTicksUsec() - m) / 1000.0;

                    m = Time.GetTicksUsec();
                    LightPropagationSystem.Compute(region, IsSolid, sources);
                    totalCpu += (Time.GetTicksUsec() - m) / 1000.0;

                    m = Time.GetTicksUsec();
                    gpu.Compute(region, IsSolid, sources);
                    totalGpu += (Time.GetTicksUsec() - m) / 1000.0;

                    m = Time.GetTicksUsec();
                    using var mask = TerrainLightMask.Build(
                        new Rect2I(origem * tileSize, new Vector2I(chunkSize, chunkSize) * tileSize),
                        root, baseLayer, compose);
                    totalMascara += (Time.GetTicksUsec() - m) / 1000.0;

                    chunks++;
                }

                GD.Print("CHUNKS=" + chunks + "  regiao=" + (chunkSize + padding * 2) + "x" + (chunkSize + padding * 2)
                    + "  mascara=" + (chunkSize * tileSize) + "x" + (chunkSize * tileSize) + " px");
                GD.Print("POR CHUNK (media, ms):");
                GD.Print("   varredura de fontes : " + (totalScan / chunks).ToString("F2"));
                GD.Print("   flood-fill CPU      : " + (totalCpu / chunks).ToString("F2"));
                GD.Print("   flood-fill GPU      : " + (totalGpu / chunks).ToString("F2"));
                GD.Print("   mascara de pixel    : " + (totalMascara / chunks).ToString("F2"));
                GD.Print("BENCHMARK OK");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
