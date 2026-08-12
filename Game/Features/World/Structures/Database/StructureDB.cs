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
                // ERA 0.97 (arvore pequena antiga) -> 0.25 (hibrida grande + espacamento por
                // caixa completa da copa = quase nada spawnava). Com arvore calibrada 1:1 no
                // Python e espacamento por coluna-ancora do tronco, 0.65 da floresta visivel
                    // sem grudar tronco em tronco. Sobe um pouco mais pra compensar a filtragem de
                    // volume livre acima do chao e voltar a encher o mapa.
                    Chance = 0.82f,
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
