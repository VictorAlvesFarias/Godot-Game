using Godot;

namespace Jogo25D.Biomes
{
    public static class BiomeResolver
    {
        private const float BiomeNoiseFrequency = 0.004f;

        private const float MinBiomeBandWidth = 64f;
        private const int BiomeSmoothingSampleCount = 5;

        private const float WarpNoiseFrequency = 0.04f;
        private const float WarpAmplitude = 48f;
        private const int WarpFractalOctaves = 2;
        private const float WarpFractalLacunarity = 2.3f;
        private const float WarpFractalGain = 0.55f;

        private const float FadeRange = 0.2f;

        public static BiomeType Resolve(long worldSeed, string dimensionId, int worldX, int worldY)
        {
            var baseValue = GetSmoothedBaseNoiseValue(worldSeed, dimensionId, worldX);
            var proximity = Mathf.Clamp(1f - Mathf.Abs(baseValue) / FadeRange, 0f, 1f);

            if (proximity <= 0f)
            {
                return baseValue < 0f ? BiomeType.LimeGround : BiomeType.OliveGround;
            }

            var warpNoise = new FastNoiseLite
            {
                Seed = (int)CombineBiomeSeed(worldSeed, dimensionId, "biome_warp"),
                Frequency = WarpNoiseFrequency,
                FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
                FractalOctaves = WarpFractalOctaves,
                FractalLacunarity = WarpFractalLacunarity,
                FractalGain = WarpFractalGain,
            };

            var warpOffset = Mathf.RoundToInt(warpNoise.GetNoise1D(worldY) * WarpAmplitude * proximity);
            var shiftedValue = GetSmoothedBaseNoiseValue(worldSeed, dimensionId, worldX + warpOffset);

            return shiftedValue < 0f ? BiomeType.LimeGround : BiomeType.OliveGround;
        }

        private static float GetSmoothedBaseNoiseValue(long worldSeed, string dimensionId, int worldX)
        {
            var half = BiomeSmoothingSampleCount / 2;
            var step = MinBiomeBandWidth / BiomeSmoothingSampleCount;
            var sum = 0f;

            for (int i = -half; i <= half; i++)
            {
                sum += GetBaseNoiseValue(worldSeed, dimensionId, worldX + Mathf.RoundToInt(i * step));
            }

            return sum / BiomeSmoothingSampleCount;
        }

        private static float GetBaseNoiseValue(long worldSeed, string dimensionId, int worldX)
        {
            var noise = new FastNoiseLite
            {
                Seed = (int)CombineBiomeSeed(worldSeed, dimensionId, "biome"),
                Frequency = BiomeNoiseFrequency,
            };

            return noise.GetNoise1D(worldX);
        }

        private static long CombineBiomeSeed(long worldSeed, string dimensionId, string tag)
        {
            unchecked
            {
                long hash = worldSeed;

                hash = hash * 397 ^ StableStringHash(dimensionId);
                hash = hash * 397 ^ StableStringHash(tag);

                return hash;
            }
        }

        private static long StableStringHash(string value)
        {
            unchecked
            {
                long hash = 1469598103934665603;

                foreach (var c in value)
                {
                    hash ^= c;
                    hash *= 1099511628211;
                }

                return hash;
            }
        }
    }
}
