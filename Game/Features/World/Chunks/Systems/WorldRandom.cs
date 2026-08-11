namespace Jogo25D.Chunks
{
    // Aleatoriedade determinada por (worldSeed, "namespace", worldX, salt) - mesma entrada sempre
    // da o mesmo resultado, entao o mundo fica estavel entre recarregamentos sem precisar
    // persistir nada. Extraido do ChunkGenerator pra poder ser reusado por StructureDefinition
    // (cada estrutura rola sua propria aleatoriedade, sem depender do ChunkGenerator).
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

        // Cada estrutura precisa do seu PROPRIO fluxo de aleatoriedade, independente das outras -
        // sem isso, duas estruturas diferentes usando o mesmo salt na mesma coluna sempre dariam
        // o MESMO resultado (ex: as duas rolando "spawna" ou "nao spawna" sempre juntas), ficando
        // artificialmente correlacionadas. Junta o id da estrutura no "namespace" do hash em vez
        // de so o dimensionId.
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
