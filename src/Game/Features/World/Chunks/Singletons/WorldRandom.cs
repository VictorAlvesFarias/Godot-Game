namespace Jogo25D.Chunks
{
    public static class WorldRandom
    {
        #region Core - Random generation

        public static float Random(long worldSeed, string context, int worldX, int salt)
        {
            unchecked
            {
                long hash = worldSeed;

                hash = hash * 397 ^ StableStringHash(context);
                hash = hash * 397 ^ worldX;
                hash = hash * 397 ^ salt;
                hash = hash * 397 ^ 0x5EED5EEDL;

                return (hash & 0xFFFFFF) / (float)0xFFFFFF;
            }
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

        public static int Random(long worldSeed, string context, int worldX, int salt, (int Min, int Max) range)
        {
            var span = range.Max - range.Min + 1;

            return range.Min + System.Math.Min(span - 1, (int)(Random(worldSeed, context, worldX, salt) * span));
        }

        public static float StructureRandom(long worldSeed, string dimensionId, string structureId, int worldX, int salt)
        {
            return Random(worldSeed, dimensionId + "|" + structureId, worldX, salt);
        }

        public static int StructureRandom(long worldSeed, string dimensionId, string structureId, int worldX, int salt, (int Min, int Max) range)
        {
            return Random(worldSeed, dimensionId + "|" + structureId, worldX, salt, range);
        }

        #endregion
    }
}
