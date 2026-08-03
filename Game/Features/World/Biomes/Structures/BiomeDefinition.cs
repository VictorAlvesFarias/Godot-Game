using Godot;

namespace Jogo25D.Biomes
{
    public class BiomeDefinition
    {
        #region Dinamic properties

        public BiomeType Type { get; init; }
        public int TerrainSet { get; init; }
        public float NoiseFrequency { get; init; }
        public float HeightAmplitude { get; init; }
        public int HeightOffset { get; init; }
        public int InteriorSourceId { get; init; }
        public Vector2I InteriorAtlasCoord { get; init; }

        #endregion
    }
}
