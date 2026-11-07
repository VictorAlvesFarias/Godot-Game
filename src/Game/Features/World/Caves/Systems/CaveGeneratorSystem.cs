using Godot;
using Jogo25D.Chunks;
using System.Collections.Generic;

namespace Jogo25D.Caves
{
    public class CaveGeneratorSystem
    {
        #region Constructors

        public CaveGeneratorSystem(long worldSeed, string dimensionId, int worldScale)
        {
            WorldSeed = worldSeed;
            DimensionId = dimensionId;
            WorldScale = worldScale;
        }

        #endregion

        #region Dinamic properties

        private const float WarpChannelOffset = 7919f;

        public long WorldSeed { get; }
        public string DimensionId { get; }
        public int WorldScale { get; }

        private readonly Dictionary<(string CaveId, string Tag, float Frequency, int Octaves), FastNoiseLite> _noises = new();

        #endregion

        #region Core - Campo de cavernas

        // A celula fica aberta se qualquer um dos tipos de caverna do bioma a abrir.
        public bool IsOpen(int x, int y, int depth, IReadOnlyList<string> caveIds)
        {
            if (depth < 0 || caveIds == null)
            {
                return false;
            }

            foreach (var id in caveIds)
            {
                if (CaveDB.Get(id)?.IsOpen(this, x, y, depth) == true)
                {
                    return true;
                }
            }

            return false;
        }

        public Vector2 Warp(CaveDefinition cave, int x, int y)
        {
            var warp = Noise(cave.Id, "warp", cave.WarpFrequency, 2);
            var amplitude = cave.WarpAmplitude * WorldScale;

            return new Vector2(
                x + warp.GetNoise2D(x, y) * amplitude,
                y + warp.GetNoise2D(x + WarpChannelOffset, y - WarpChannelOffset) * amplitude);
        }

        // Ruido do tipo de caverna, semeado pelo Id: dois tipos com a mesma forma geram padroes diferentes.
        // octaves = 0: sem fractal.
        public FastNoiseLite Noise(string caveId, string tag, float frequency, int octaves)
        {
            var key = (caveId, tag, frequency, octaves);

            if (_noises.TryGetValue(key, out var noise))
            {
                return noise;
            }

            unchecked
            {
                var hash = WorldSeed;

                hash = hash * 397 ^ WorldRandom.StableStringHash(DimensionId);
                hash = hash * 397 ^ WorldRandom.StableStringHash(caveId);
                hash = hash * 397 ^ WorldRandom.StableStringHash(tag);

                noise = new FastNoiseLite
                {
                    Seed = (int)hash,
                    NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
                    Frequency = frequency / WorldScale,
                    FractalType = octaves > 0 ? FastNoiseLite.FractalTypeEnum.Fbm : FastNoiseLite.FractalTypeEnum.None,
                    FractalOctaves = Mathf.Max(1, octaves),
                };
            }

            _noises[key] = noise;

            return noise;
        }

        #endregion
    }
}
