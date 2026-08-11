using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Structures
{
    public static class StructureDB
    {
        private static readonly Dictionary<string, StructureDefinition> _structures = new()
        {
            ["tree"] = new TreeStructureDefinition
            {
                Id = "tree",
                // ERA 0.97 - calibrado pro sistema antigo de arvore PEQUENA (raio fixo ate ~5
                // tiles). A arvore hibrida atual e bem maior (raio de copa 6-12 tiles reais) e,
                // com a trava de "caber dentro do proprio chunk" removida, quase toda coluna
                // elegivel conseguia nascer - virava floresta grudada/infinita. Bem mais baixo
                // agora que arvore grande realmente nasce a maior parte das vezes que rola.
                Chance = 0.25f,
            },
        };

        // Uniao de todos os terrain_sets de TODA estrutura registrada - usado pelo ChunkGenerator
        // pra excluir decoracao (tronco/copa de arvore, e qualquer estrutura futura) da deteccao
        // de "vizinho estrangeiro do bioma" nas 3 camadas, sem precisar listar cada estrutura na
        // mao. Calculado uma vez, na primeira leitura.
        private static HashSet<int> _allTerrainSets;

        public static HashSet<int> AllTerrainSets =>
            _allTerrainSets ??= _structures.Values.SelectMany(s => s.TerrainSets).ToHashSet();

        public static StructureDefinition Get(string id)
        {
            return _structures.TryGetValue(id, out var definition) ? definition : null;
        }
    }
}
