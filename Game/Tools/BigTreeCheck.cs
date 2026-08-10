using Godot;
using Jogo25D.Biomes;
using Jogo25D.Chunks;
using System.Collections.Generic;

namespace Jogo25D.Biomes
{
    public partial class BigTreeCheck : Node
    {
        public override void _Ready()
        {
            var scene = GD.Load<PackedScene>("res://Scenes/World/Levels/Upsidedown.tscn").Instantiate<Node2D>();
            AddChild(scene);

            var texture = scene.GetNode<TerrainLayer>("Texture");
            var bordercap = scene.GetNode<TerrainLayer>("Bordercap");
            var baseLayer = scene.GetNode<TerrainLayer>("Base");

            long seed = 42;
            int chunkSize = 32;

            for (int cx = -10; cx <= 10; cx++)
                for (int cy = -3; cy <= 0; cy++)
                    ChunkGenerator.Paint(texture, bordercap, baseLayer, seed, "upsidedown", new Vector2I(cx, cy), chunkSize);

            var seenX = new HashSet<int>();
            var found = 0;
            for (int x = -10 * chunkSize; x < 11 * chunkSize && found < 6; x++)
            {
                bool hasTree = false;
                for (int y = -3 * chunkSize; y < chunkSize; y++)
                {
                    var d = baseLayer.GetCellTileData(new Vector2I(x, y));
                    if (d != null && (d.TerrainSet == 6 || d.TerrainSet == 7)) { hasTree = true; break; }
                }

                if (!hasTree || seenX.Contains(x)) continue;

                // pula colunas vizinhas da mesma arvore (raio ate 3)
                bool tooClose = false;
                foreach (var sx in seenX) if (Mathf.Abs(sx - x) <= 3) { tooClose = true; break; }
                if (tooClose) continue;

                seenX.Add(x);
                found++;

                GD.Print($"--- coluna x={x} ---");
                for (int y = -25; y <= 2; y++)
                {
                    var c = new Vector2I(x, y);
                    var cd = baseLayer.GetCellTileData(c);
                    var mark = " ";
                    if (cd != null && cd.TerrainSet == 6) mark = "W";
                    else if (cd != null && cd.TerrainSet == 7) mark = "L";
                    else if (texture.GetCellSourceId(c) != -1) mark = ".";
                    if (mark != " ") GD.Print($"y={y} [{mark}]");
                }
            }

            GD.Print("total colunas com arvore encontradas (amostra) = " + found);

            GetTree().Quit();
        }
    }
}
