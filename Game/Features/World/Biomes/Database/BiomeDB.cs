using Godot;
using System.Collections.Generic;

namespace Jogo25D.Biomes
{
    public static class BiomeDB
    {
        private static readonly Dictionary<BiomeType, BiomeDefinition> _biomes = new()
        {
            [BiomeType.LimeGround] = new BiomeDefinition
            {
                Type = BiomeType.LimeGround,
                TerrainSet = 0,
                NoiseFrequency = 0.05f,
                HeightAmplitude = 4f,
                HeightOffset = 0,
                InteriorSourceId = 7,
                InteriorAtlasCoord = new Vector2I(1, 1),
                BorderCapSourceId = 9,
            },
            [BiomeType.OliveGround] = new BiomeDefinition
            {
                Type = BiomeType.OliveGround,
                TerrainSet = 1,
                NoiseFrequency = 0.08f,
                HeightAmplitude = 8f,
                HeightOffset = 0,
                InteriorSourceId = 1,
                InteriorAtlasCoord = new Vector2I(1, 1),
                BorderCapSourceId = 10,
            },
        };

        public static BiomeDefinition Get(BiomeType type)
        {
            return _biomes.TryGetValue(type, out var definition) ? definition : _biomes[BiomeType.LimeGround];
        }

        public static BiomeDefinition GetByTerrainSet(int terrainSet)
        {
            foreach (var definition in _biomes.Values)
            {
                if (definition.TerrainSet == terrainSet)
                {
                    return definition;
                }
            }

            return null;
        }
    }
}
