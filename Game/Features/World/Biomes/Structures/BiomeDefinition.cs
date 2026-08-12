using Godot;
using System.Collections.Generic;

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
        public int BorderCapTerrainSet { get; init; }

        // Ids de StructureDefinition (StructureDB) que podem nascer sozinhas nesse bioma - cada
        // uma rola sua PROPRIA chance (StructureDefinition.Chance) por coluna elegivel. Lista
        // vazia/null = bioma sem decoracao procedural nenhuma.
        public List<string> StructureIds { get; init; } = new();

        #endregion
    }
}
