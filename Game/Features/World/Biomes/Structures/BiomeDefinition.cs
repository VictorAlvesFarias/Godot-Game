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
        public int BorderCapSourceId { get; init; }
        public int BorderCapTerrainSet { get; init; }
        public int BaseSourceId { get; init; }
        public int BaseTerrainSet { get; init; }
        public float TreeChance { get; init; }

        #endregion
    }
}
