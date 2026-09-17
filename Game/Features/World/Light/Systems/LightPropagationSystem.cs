using Godot;
using Jogo25D.Constants;
using System;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    public readonly struct LightSource
    {
        public readonly Vector2I Cell;
        public readonly Color Color;

        public LightSource(Vector2I cell, Color color)
        {
            Cell = cell;
            Color = color;
        }
    }

    // Flood-fill de luz por tile (mesmo principio usado em jogos tipo Terraria/Starbound): cada
    // fonte propaga para os vizinhos com atenuacao multiplicativa, ate o valor cair abaixo do
    // limiar. Nao precisa de limite manual de profundidade - o falloff geometrico ja garante que
    // a busca termina sozinha.
    public static class LightPropagationSystem
    {
        // 8 direcoes (nao so 4): com falloff so nos 4 vizinhos ortogonais, cada fonte pontual
        // espalha em losango (BFS de distancia Manhattan) em vez de circulo, o que fica visivel
        // como raios/picos saindo de cada fonte. Incluir as diagonais com atenuacao ajustada pela
        // distancia real (sqrt(2) num passo diagonal) deixa o brilho redondo, tipo Terraria.
        private static readonly (Vector2I Offset, float Distance)[] Neighbors =
        {
            (new Vector2I(1, 0), 1f),
            (new Vector2I(-1, 0), 1f),
            (new Vector2I(0, 1), 1f),
            (new Vector2I(0, -1), 1f),
            (new Vector2I(1, 1), 1.41421356f),
            (new Vector2I(1, -1), 1.41421356f),
            (new Vector2I(-1, 1), 1.41421356f),
            (new Vector2I(-1, -1), 1.41421356f),
        };

        public static Color[,] Compute(Rect2I region, Func<Vector2I, bool> isSolid, IReadOnlyList<LightSource> sources)
        {
            var width = region.Size.X;
            var height = region.Size.Y;
            var grid = new Color[width, height];
            var queue = new Queue<Vector2I>();

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    grid[x, y] = Colors.Black;
                }
            }

            void Seed(Vector2I worldCell, Color color)
            {
                var local = worldCell - region.Position;

                if (local.X < 0 || local.X >= width || local.Y < 0 || local.Y >= height)
                {
                    return;
                }

                if (color.R <= grid[local.X, local.Y].R && color.G <= grid[local.X, local.Y].G && color.B <= grid[local.X, local.Y].B)
                {
                    return;
                }

                grid[local.X, local.Y] = MaxColor(grid[local.X, local.Y], color);
                queue.Enqueue(local);
            }

            foreach (var source in sources)
            {
                Seed(source.Cell, source.Color);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentColor = grid[current.X, current.Y];

                foreach (var (offset, distance) in Neighbors)
                {
                    var neighbor = current + offset;

                    if (neighbor.X < 0 || neighbor.X >= width || neighbor.Y < 0 || neighbor.Y >= height)
                    {
                        continue;
                    }

                    var worldNeighbor = neighbor + region.Position;
                    var falloff = isSolid(worldNeighbor) ? LightingConstants.SOLID_FALLOFF : LightingConstants.AIR_FALLOFF;
                    var propagated = currentColor * Mathf.Pow(falloff, distance);

                    if (propagated.R < LightingConstants.MIN_LIGHT_THRESHOLD && propagated.G < LightingConstants.MIN_LIGHT_THRESHOLD && propagated.B < LightingConstants.MIN_LIGHT_THRESHOLD)
                    {
                        continue;
                    }

                    var existing = grid[neighbor.X, neighbor.Y];

                    if (propagated.R <= existing.R && propagated.G <= existing.G && propagated.B <= existing.B)
                    {
                        continue;
                    }

                    grid[neighbor.X, neighbor.Y] = MaxColor(existing, propagated);
                    queue.Enqueue(neighbor);
                }
            }

            return grid;
        }

        private static Color MaxColor(Color a, Color b)
        {
            return new Color(Mathf.Max(a.R, b.R), Mathf.Max(a.G, b.G), Mathf.Max(a.B, b.B));
        }
    }
}
