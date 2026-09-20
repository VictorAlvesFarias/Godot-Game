using Godot;
using Jogo25D.Constants;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    internal static class SkyAccessField
    {
        #region Dinamic properties

        private const float DiagonalDistance = 1.41421356f;
        private const float PriorityEpsilon = .00001f;
        private const float SolidDepthScale = 3f;
        private const float MinDepth = .25f;

        #endregion

        #region Core - Construcao do campo

        public static byte[] Build(LogicalLightWorld world, Vector2I origin, Vector2I size, float depth)
        {
            int width = size.X;
            int height = size.Y;
            int count = width * height;

            var data = new byte[count * 4];
            var light = new float[count];
            var queue = new PriorityQueue<int, float>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;

                    data[i * 4] = world.Opacity(origin.X + x, origin.Y + y);
                    data[i * 4 + 1] = world.HasBackground(origin.X + x, origin.Y + y) ? (byte)255 : (byte)0;
                    data[i * 4 + 3] = 255;

                    if (data[i * 4] == 0 && data[i * 4 + 1] == 0)
                    {
                        light[i] = 1;

                        queue.Enqueue(i, -1);
                    }
                }
            }

            Spread(false, data, light, queue, width, height, depth);

            for (int i = 0; i < count; i++)
            {
                if (data[i * 4] == 0 && light[i] > 0)
                {
                    queue.Enqueue(i, -light[i]);
                }
            }

            Spread(true, data, light, queue, width, height, depth);

            for (int i = 0; i < count; i++)
            {
                data[i * 4 + 2] = (byte)Mathf.RoundToInt(Mathf.Clamp(light[i], 0, 1) * 255);
            }

            return data;
        }

        #endregion

        #region Core - Propagacao

        private static void Spread(bool solids, byte[] data, float[] light, PriorityQueue<int, float> queue, int width, int height, float depth)
        {
            while (queue.TryDequeue(out int i, out float priority))
            {
                if (-priority < light[i] - PriorityEpsilon)
                {
                    continue;
                }

                int x = i % width;
                int y = i / width;

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0)
                        {
                            continue;
                        }

                        int nx = x + dx;
                        int ny = y + dy;

                        if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        {
                            continue;
                        }

                        int j = ny * width + nx;

                        if (data[j * 4] > 0 != solids)
                        {
                            continue;
                        }

                        bool diagonal = dx != 0 && dy != 0;

                        if (!solids && diagonal && (data[(y * width + nx) * 4] > 0 || data[(ny * width + x) * 4] > 0))
                        {
                            continue;
                        }

                        float distance = diagonal ? DiagonalDistance : 1f;
                        float next = solids
                            ? light[i] * Mathf.Pow(LightingConstants.SOLID_FALLOFF, distance * SolidDepthScale / Mathf.Max(MinDepth, depth))
                            : light[i] - LightingConstants.AIR_LIGHT_LOSS * distance;

                        if (next < LightingConstants.MIN_LIGHT_THRESHOLD || next <= light[j])
                        {
                            continue;
                        }

                        light[j] = next;

                        queue.Enqueue(j, -next);
                    }
                }
            }
        }

        #endregion
    }
}
