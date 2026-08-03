using Godot;

namespace Jogo25D.Biomes
{
    public static class BiomeResolver
    {
        private const float BiomeNoiseFrequency = 0.004f;

        public static BiomeType Resolve(long worldSeed, string dimensionId, int worldX)
        {
            var noise = new FastNoiseLite
            {
                Seed = (int)CombineBiomeSeed(worldSeed, dimensionId),
                Frequency = BiomeNoiseFrequency,
            };

            var value = noise.GetNoise1D(worldX);

            return value < 0f ? BiomeType.LimeGround : BiomeType.OliveGround;
        }

        private static long CombineBiomeSeed(long worldSeed, string dimensionId)
        {
            unchecked
            {
                long hash = worldSeed;

                hash = hash * 397 ^ StableStringHash(dimensionId);
                hash = hash * 397 ^ StableStringHash("biome");

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
