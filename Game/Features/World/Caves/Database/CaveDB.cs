using System.Collections.Generic;

namespace Jogo25D.Caves
{
    public static class CaveDB
    {
        public const string TunnelsId = "tunnels";
        public const string HallsId = "halls";

        private static readonly Dictionary<string, CaveDefinition> _caves = new()
        {
            [TunnelsId] = new SpaghettiCaveDefinition
            {
                Id = TunnelsId,
                Frequency = 0.011f,
                Width = 0.1f,
                MinDepth = 6,
                EntranceFrequency = 0.008f,
                EntranceThreshold = -0.05f,
                EntranceFade = 0.1f,
                WarpFrequency = 0.03f,
                WarpAmplitude = 6f,
            },
            [HallsId] = new CheeseCaveDefinition
            {
                Id = HallsId,
                Frequency = 0.016f,
                FractalOctaves = 3,
                ThresholdShallow = 0.6f,
                ThresholdDeep = 0.12f,
                MinDepth = 14,
                FullDepth = 320,
                WarpFrequency = 0.03f,
                WarpAmplitude = 6f,
            },
        };

        public static CaveDefinition Get(string id)
        {
            return id != null && _caves.TryGetValue(id, out var definition) ? definition : null;
        }
    }
}
