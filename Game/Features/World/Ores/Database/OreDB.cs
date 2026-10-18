using System.Collections.Generic;

namespace Jogo25D.Ores
{
    public static class OreDB
    {
        #region Dinamic properties

        // Do mais raro para o mais comum: numa celula onde mais de um passaria, fica o mais raro.
        private static readonly List<OreDefinition> _ores = new()
        {
            new OreDefinition
            {
                Id = "gold",
                BlockId = "gold_ore",
                MinDepth = 90,
                Frequency = 0.06f,
                Threshold = 0.9f,
                Tag = "ore_gold",
            },
            new OreDefinition
            {
                Id = "iron",
                BlockId = "iron_ore",
                MinDepth = 40,
                Frequency = 0.065f,
                Threshold = 0.86f,
                Tag = "ore_iron",
            },
            new OreDefinition
            {
                Id = "copper",
                BlockId = "copper_ore",
                MinDepth = 6,
                Frequency = 0.07f,
                Threshold = 0.79f,
                Tag = "ore_copper",
            },
        };

        #endregion

        #region Core - Consulta

        public static IReadOnlyList<OreDefinition> All => _ores;

        #endregion
    }
}
