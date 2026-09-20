using Godot;
using Jogo25D.Constants;
using System;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    public static class LightPropagationSystem
    {
        #region Dinamic properties

        private const float DiagonalDistance = 1.41421356f;

        private static readonly (Vector2I Offset, float Distance)[] Neighbors =
        {
            (new Vector2I(1, 0), 1f),
            (new Vector2I(-1, 0), 1f),
            (new Vector2I(0, 1), 1f),
            (new Vector2I(0, -1), 1f),
            (new Vector2I(1, 1), DiagonalDistance),
            (new Vector2I(1, -1), DiagonalDistance),
            (new Vector2I(-1, 1), DiagonalDistance),
            (new Vector2I(-1, -1), DiagonalDistance),
        };

        #endregion

        #region Core - Propagacao

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

            foreach (var source in sources)
            {
                Seed(grid, queue, region, source.Cell, source.Color);
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

                    var propagated = Propagate(currentColor, distance, isSolid(neighbor + region.Position));

                    if (propagated.R < LightingConstants.MIN_LIGHT_THRESHOLD
                        && propagated.G < LightingConstants.MIN_LIGHT_THRESHOLD
                        && propagated.B < LightingConstants.MIN_LIGHT_THRESHOLD)
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

        #endregion

        #region Utils

        private static void Seed(Color[,] grid, Queue<Vector2I> queue, Rect2I region, Vector2I worldCell, Color color)
        {
            var local = worldCell - region.Position;

            if (local.X < 0 || local.X >= region.Size.X || local.Y < 0 || local.Y >= region.Size.Y)
            {
                return;
            }

            var existing = grid[local.X, local.Y];

            if (color.R <= existing.R && color.G <= existing.G && color.B <= existing.B)
            {
                return;
            }

            grid[local.X, local.Y] = MaxColor(existing, color);

            queue.Enqueue(local);
        }

        private static Color Propagate(Color color, float distance, bool solid)
        {
            if (solid)
            {
                return color * Mathf.Pow(LightingConstants.SOLID_FALLOFF, distance);
            }

            var loss = LightingConstants.AIR_LIGHT_LOSS * distance;

            return new Color(
                Mathf.Max(0, color.R - loss),
                Mathf.Max(0, color.G - loss),
                Mathf.Max(0, color.B - loss));
        }

        private static Color MaxColor(Color a, Color b)
        {
            return new Color(Mathf.Max(a.R, b.R), Mathf.Max(a.G, b.G), Mathf.Max(a.B, b.B));
        }

        #endregion
    }
}
