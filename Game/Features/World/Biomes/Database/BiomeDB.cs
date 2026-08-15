using Godot;
using System.Collections.Generic;

namespace Jogo25D.Biomes
{
    public static class BiomeDB
    {
        public const string LimeGroundId = "lime_ground";
        public const string OliveGroundId = "olive_ground";

        private static readonly Dictionary<string, BiomeDefinition> _biomes = new()
        {
            [LimeGroundId] = new BiomeDefinition
            {
                Id = LimeGroundId,
                TerrainSet = 0,
                NoiseFrequency = 0.05f,
                HeightAmplitude = 4f,
                HeightOffset = 0,
                InteriorSourceId = 0,
                InteriorAtlasCoord = new Vector2I(1, 1),
                BorderCapTerrainSet = 2,
                StructureIds = new List<string> { "tree" },
            },
            [OliveGroundId] = new BiomeDefinition
            {
                Id = OliveGroundId,
                TerrainSet = 1,
                NoiseFrequency = 0.08f,
                HeightAmplitude = 8f,
                HeightOffset = 0,
                InteriorSourceId = 1,
                InteriorAtlasCoord = new Vector2I(1, 1),
                BorderCapTerrainSet = 3,
            },
        };

        // Ordem usada pelo ChunkGeneratorSystem pra dividir o eixo de ruido em faixas de bioma.
        // Adicionar um novo bioma aqui automaticamente ganha uma faixa no mundo, sem precisar
        // mexer no algoritmo de geracao.
        public static readonly IReadOnlyList<string> OrderedIds = new List<string>
        {
            LimeGroundId,
            OliveGroundId,
        };

        public static BiomeDefinition Get(string id)
        {
            return _biomes.TryGetValue(id, out var definition) ? definition : _biomes[LimeGroundId];
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
