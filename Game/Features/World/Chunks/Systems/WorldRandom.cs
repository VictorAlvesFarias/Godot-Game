namespace Jogo25D.Chunks
{
    public static class WorldRandom
    {
        public static float Random01(long worldSeed, string ns, int worldX, int salt)
        {
            unchecked
            {
                long hash = worldSeed;

                hash = hash * 397 ^ StableStringHash(ns);
                hash = hash * 397 ^ worldX;
                hash = hash * 397 ^ salt;
                hash = hash * 397 ^ 0x5EED5EEDL;

                return (hash & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        public static int RandomInt(long worldSeed, string ns, int worldX, int salt, (int Min, int Max) range)
        {
            var span = range.Max - range.Min + 1;

            return range.Min + System.Math.Min(span - 1, (int)(Random01(worldSeed, ns, worldX, salt) * span));
        }

        public static float StructureRandom01(long worldSeed, string dimensionId, string structureId, int worldX, int salt)
        {
            return Random01(worldSeed, dimensionId + "|" + structureId, worldX, salt);
        }

        public static int StructureRandomInt(long worldSeed, string dimensionId, string structureId, int worldX, int salt, (int Min, int Max) range)
        {
            return RandomInt(worldSeed, dimensionId + "|" + structureId, worldX, salt, range);
        }

        public static long StableStringHash(string value)
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
