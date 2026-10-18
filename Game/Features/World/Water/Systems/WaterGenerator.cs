using Godot;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using Jogo25D.Dimensions;
using Jogo25D.Utils.Coordinates;
using System;

namespace Jogo25D.Water
{
    // Agua gerada pela seed, so pela geracao do terreno (sem edicoes), entao a mesma seed da a mesma agua:
    // - lagos: um ruido por coluna marca regioes da superficie; em cada uma, a agua enche o ar entre a superficie e a
    //   menor das duas bordas;
    // - pocas: no fundo de cavernas abaixo de uma profundidade, um ruido escolhe chaos que recebem 2 a 4 celulas de agua.
    public sealed class WaterGenerator : IDisposable
    {
        #region Constructors

        public WaterGenerator(TerrainCache terrain)
        {
            _terrain = terrain;

            var scale = terrain.WorldScale;
            var seed = unchecked((int)(terrain.Seed * 31 ^ WorldRandom.StableStringHash(terrain.DimensionId + "|water")));

            _lakes = new FastNoiseLite { Seed = seed, Frequency = LakeFrequency / scale, FractalType = FastNoiseLite.FractalTypeEnum.None };
            _pools = new FastNoiseLite { Seed = seed ^ 0x5A5A5A, Frequency = PoolFrequency / scale, FractalType = FastNoiseLite.FractalTypeEnum.None };
            _maxLakeWidth = MaxLakeWidth * scale;
            _minPoolDepth = MinPoolDepth * scale;
        }

        #endregion

        #region Dinamic properties

        private const float LakeFrequency = 0.012f;
        private const float LakeThreshold = 0.2f;
        private const int MaxLakeWidth = 64;
        private const float PoolFrequency = 0.06f;
        private const float PoolThreshold = 0.3f;
        private const int MinPoolDepth = 30;
        private const int MinPoolCells = 2;
        private const int MaxPoolCells = 4;
        private const int MaxPoolHalfWidth = 10;

        private readonly TerrainCache _terrain;
        private readonly FastNoiseLite _lakes;
        private readonly FastNoiseLite _pools;
        private readonly int _maxLakeWidth;
        private readonly int _minPoolDepth;

        #endregion

        #region Core - Geracao

        // Niveis do chunk (byte[1024]), ou nulo quando o chunk nao tem agua gerada.
        public byte[] Generate(Vector2I chunk)
        {
            if (!_terrain.Procedural)
            {
                return null;
            }

            var size = ChunkStreamingConstants.CHUNK_SIZE;
            var origin = CoordinateUtilities.ChunkToCell(chunk);
            byte[] levels = null;

            for (var localX = 0; localX < size; localX++)
            {
                var x = origin.X + localX;
                var ground = _terrain.GroundHeight(x);
                var lakeTop = LakeTop(x);

                for (var localY = 0; localY < size; localY++)
                {
                    var y = origin.Y + localY;

                    if (_terrain.GeneratedCollides(x, y))
                    {
                        continue;
                    }

                    var lake = lakeTop is int top && y >= top && y < ground;

                    if (lake || IsPool(x, y, ground))
                    {
                        levels ??= new byte[WaterField.ChunkCells];
                        levels[localY * size + localX] = WaterField.Full;
                    }
                }
            }

            return levels;
        }

        // Linha d'agua (y) do lago que cobre a coluna, ou nulo: a menor (y maior) das superficies das duas bordas da
        // regiao do ruido. Regiao mais larga que o limite nao vira lago.
        private int? LakeTop(int x)
        {
            if (!InLake(x))
            {
                return null;
            }

            var left = x;
            var right = x;

            while (InLake(left - 1))
            {
                if (x - --left >= _maxLakeWidth)
                {
                    return null;
                }
            }

            while (InLake(right + 1))
            {
                if (++right - left >= _maxLakeWidth)
                {
                    return null;
                }
            }

            return Math.Max(_terrain.GroundHeight(left), _terrain.GroundHeight(right));
        }

        private bool InLake(int x)
        {
            return _lakes.GetNoise1D(x) > LakeThreshold;
        }

        // Ar a ate 2-4 celulas acima de um chao de caverna escolhido pelo ruido (mais forte = mais fundo), abaixo da
        // profundidade minima, e so numa bacia: a linha da celula fecha em paredes dos dois lados, com chao embaixo de
        // cada celula no caminho. Sem isso a agua gerada escorreria pela encosta.
        private bool IsPool(int x, int y, int ground)
        {
            if (y - ground < _minPoolDepth)
            {
                return false;
            }

            for (var distance = 1; distance <= MaxPoolCells; distance++)
            {
                if (!_terrain.GeneratedCollides(x, y + distance))
                {
                    continue;
                }

                var noise = _pools.GetNoise2D(x, y + distance);

                if (noise <= PoolThreshold)
                {
                    return false;
                }

                var strength = (noise - PoolThreshold) / (1f - PoolThreshold);
                var cells = MinPoolCells + Mathf.RoundToInt(strength * 2f * (MaxPoolCells - MinPoolCells));

                return distance <= Math.Min(cells, MaxPoolCells) && IsBasinRow(x, y, -1) && IsBasinRow(x, y, 1);
            }

            return false;
        }

        // Andando pela linha y para um lado, acha parede antes de MaxPoolHalfWidth, e toda celula de ar no caminho tem
        // chao a ate MaxPoolCells abaixo.
        private bool IsBasinRow(int x, int y, int direction)
        {
            for (var step = 1; step <= MaxPoolHalfWidth; step++)
            {
                var column = x + direction * step;

                if (_terrain.GeneratedCollides(column, y))
                {
                    return true;
                }

                var supported = false;

                for (var below = 1; below <= MaxPoolCells && !supported; below++)
                {
                    supported = _terrain.GeneratedCollides(column, y + below);
                }

                if (!supported)
                {
                    return false;
                }
            }

            return false;
        }

        public void Dispose()
        {
            _lakes.Dispose();
            _pools.Dispose();
        }

        #endregion
    }
}
