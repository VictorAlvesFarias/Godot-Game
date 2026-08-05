using Godot;
using System.Collections.Generic;

namespace Jogo25D.Biomes
{
    public static class BiomeTerrainConnector
    {
        private static readonly Vector2I[] NeighborOffsets = new Vector2I[]
        {
            new Vector2I(-1, -1), new Vector2I(0, -1), new Vector2I(1, -1),
            new Vector2I(-1, 0), new Vector2I(1, 0),
            new Vector2I(-1, 1), new Vector2I(0, 1), new Vector2I(1, 1),
        };

        // Nome da Custom Data Layer (bool) criada no TileSet - marcada manualmente, por tile,
        // no editor do Godot. Por padrao NENHUM tile conecta com um tileset diferente (fronteira
        // crua); so os tiles com essa flag marcada sao tratados como "parte do mesmo conjunto"
        // de um bioma vizinho pra fins de autotile.
        private const string ConnectionCustomDataLayer = "connection";

        // Conecta "cells" com o autotile do proprio bioma. Vizinhos solidos de OUTRO bioma so
        // sao temporariamente disfarcados de chao solido deste bioma (tile interior) SE o tile
        // do vizinho tiver a flag "connection" marcada - senao a fronteira fica crua de
        // proposito. Depois do calculo, os vizinhos disfarcados voltam ao tile real deles.
        public static void Connect(TileMapLayer layer, IReadOnlyCollection<Vector2I> cells, BiomeDefinition biomeDef)
        {
            if (cells.Count == 0)
            {
                return;
            }

            var cellSet = new HashSet<Vector2I>(cells);
            var foreignNeighbors = CollectForeignNeighbors(layer, cellSet, biomeDef.TerrainSet);
            var disguisedNeighbors = new List<ForeignCellInfo>();

            foreach (var entry in foreignNeighbors)
            {
                if (!HasConnectionFlag(layer, entry.Cell))
                {
                    continue;
                }

                disguisedNeighbors.Add(entry);
                layer.SetCell(entry.Cell, biomeDef.InteriorSourceId, biomeDef.InteriorAtlasCoord);
            }

            var cellsArray = new Godot.Collections.Array<Vector2I>();

            foreach (var cell in cells)
            {
                cellsArray.Add(cell);
            }

            layer.SetCellsTerrainConnect(cellsArray, biomeDef.TerrainSet, 0, false);

            foreach (var entry in disguisedNeighbors)
            {
                layer.SetCell(entry.Cell, entry.SourceId, entry.AtlasCoord, entry.AlternativeId);
            }
        }

        private static bool HasConnectionFlag(TileMapLayer layer, Vector2I cell)
        {
            var tileData = layer.GetCellTileData(cell);

            return tileData != null && tileData.GetCustomData(ConnectionCustomDataLayer).AsBool();
        }

        // Espelha o Connect() do outro lado da divisa: reconecta as celulas estrangeiras que
        // encostam em "cells", cada uma com o proprio bioma dela, tratando "cells" como chao
        // solido temporario. Assim os dois lados da fronteira ficam com a borda correta.
        public static void ReconnectForeignBorder(TileMapLayer layer, IReadOnlyCollection<Vector2I> cells, BiomeDefinition biomeDef)
        {
            var cellSet = new HashSet<Vector2I>(cells);
            var foreignNeighbors = CollectForeignNeighbors(layer, cellSet, biomeDef.TerrainSet);
            var groups = new Dictionary<int, List<Vector2I>>();

            foreach (var entry in foreignNeighbors)
            {
                if (!groups.TryGetValue(entry.TerrainSet, out var group))
                {
                    group = new List<Vector2I>();
                    groups[entry.TerrainSet] = group;
                }

                group.Add(entry.Cell);
            }

            foreach (var group in groups)
            {
                var foreignBiomeDef = BiomeDB.GetByTerrainSet(group.Key);

                if (foreignBiomeDef == null)
                {
                    continue;
                }

                Connect(layer, group.Value, foreignBiomeDef);
            }
        }

        // Reconecta cada celula com o bioma que ela JA tem (le o TerrainSet do tile atual),
        // agrupando por bioma antes de conectar - assim celulas de biomas diferentes nunca sao
        // repintadas com o terrain de outra.
        public static void ReconnectExistingCells(TileMapLayer layer, IEnumerable<Vector2I> cells)
        {
            var groups = new Dictionary<int, List<Vector2I>>();

            foreach (var cell in cells)
            {
                if (layer.GetCellSourceId(cell) == -1)
                {
                    continue;
                }

                var tileData = layer.GetCellTileData(cell);

                if (tileData == null)
                {
                    continue;
                }

                if (!groups.TryGetValue(tileData.TerrainSet, out var group))
                {
                    group = new List<Vector2I>();
                    groups[tileData.TerrainSet] = group;
                }

                group.Add(cell);
            }

            foreach (var group in groups)
            {
                var biomeDef = BiomeDB.GetByTerrainSet(group.Key);

                if (biomeDef == null)
                {
                    continue;
                }

                Connect(layer, group.Value, biomeDef);
            }
        }

        // Ordem fixa usada em qualquer lugar que trabalhe com as 4 camadas de preenchimento
        // direcional: Direita, Esquerda, Cima, Baixo (cima = Y menor, mesma convencao da UV do
        // shader tile_edge_fill.gdshader).
        public static readonly Vector2I[] EdgeFillDirectionOffsets = new Vector2I[]
        {
            new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, -1), new Vector2I(0, 1),
        };

        // Copia, em CADA UMA das 4 camadas de preenchimento direcional, o MESMO tile que ja
        // esta na camada de chao - mas SO na camada correspondente ao lado que realmente
        // encosta em outro terrain_set (ex: se o vizinho estrangeiro esta a direita, so a
        // camada "Right" recebe a copia dessa celula). Cada camada carrega um material do
        // shader tile_edge_fill.gdshader configurado pra so preencher aquela metade do tile.
        // "fillLayers" deve seguir a mesma ordem de EdgeFillDirectionOffsets (Right, Left, Top,
        // Bottom); qualquer posicao pode ser null se a camada nao existir.
        public static void PaintEdgeFillOverlay(TileMapLayer groundLayer, TileMapLayer[] fillLayers, IEnumerable<Vector2I> cells)
        {
            foreach (var cell in cells)
            {
                var tileData = groundLayer.GetCellTileData(cell);

                if (tileData == null)
                {
                    foreach (var fillLayer in fillLayers)
                    {
                        fillLayer?.SetCell(cell, -1);
                    }

                    continue;
                }

                for (int i = 0; i < EdgeFillDirectionOffsets.Length; i++)
                {
                    var fillLayer = fillLayers[i];

                    if (fillLayer == null)
                    {
                        continue;
                    }

                    var neighbor = cell + EdgeFillDirectionOffsets[i];
                    var touchesForeignTileset = false;

                    if (groundLayer.GetCellSourceId(neighbor) != -1)
                    {
                        var neighborTileData = groundLayer.GetCellTileData(neighbor);

                        touchesForeignTileset = neighborTileData != null && neighborTileData.TerrainSet != tileData.TerrainSet;
                    }

                    if (touchesForeignTileset)
                    {
                        fillLayer.SetCell(cell, groundLayer.GetCellSourceId(cell), groundLayer.GetCellAtlasCoords(cell), groundLayer.GetCellAlternativeTile(cell));
                    }
                    else
                    {
                        fillLayer.SetCell(cell, -1);
                    }
                }
            }
        }

        // Pinta a variante BorderCap numa camada SEPARADA (por cima do chao, nunca substitui a
        // celula original) - so nas celulas de "cells" que encostam (8 direcoes) em algum tile
        // de OUTRO bioma no chao. A variante usa um terrain_set PROPRIO e exclusivo (nao o mesmo
        // terrain_set do chao), entao o autotile dela so conecta com OUTRAS celulas BorderCap do
        // MESMO bioma que tambem estejam na linha de juncao - nunca com o bioma oposto, e nunca
        // "rouba" celulas normais de interior (que ficam noutro terrain_set inteiramente).
        public static void PaintBorderCap(TileMapLayer overlayLayer, TileMapLayer groundLayer, IReadOnlyCollection<Vector2I> cells, BiomeDefinition biomeDef)
        {
            var junctionCells = new List<Vector2I>();
            var junctionSet = new HashSet<Vector2I>();

            foreach (var cell in cells)
            {
                overlayLayer.SetCell(cell, -1);

                if (TouchesForeignBiome(groundLayer, cell, biomeDef.TerrainSet))
                {
                    junctionCells.Add(cell);
                    junctionSet.Add(cell);
                }
            }

            if (junctionCells.Count == 0)
            {
                return;
            }

            // O calculo de conexao (abaixo) so enxerga o que ja esta pintado NESSA camada de
            // overlay - como so as celulas da linha de juncao sao pintadas ali, o lado que vira
            // pro INTERIOR do proprio bioma pareceria "vazio" (sem conexao), mesmo o chao la
            // embaixo sendo solido do mesmo bioma. Pra evitar isso, disfarca temporariamente
            // esses vizinhos interiores (mesmo bioma, fora da linha de juncao) de tile BorderCap
            // interior nessa mesma camada - exatamente o mesmo truque que Connect() ja usa pro
            // lado estrangeiro, so que aqui e pro proprio bioma. Depois do calculo, os vizinhos
            // disfarcados voltam a ficar vazios (o overlay so mostra a linha de fato).
            var disguisedCells = new HashSet<Vector2I>();

            foreach (var cell in junctionCells)
            {
                foreach (var offset in NeighborOffsets)
                {
                    var neighbor = cell + offset;

                    if (junctionSet.Contains(neighbor) || !disguisedCells.Add(neighbor))
                    {
                        continue;
                    }

                    // Se ja existe um BorderCap DE VERDADE aqui (pintado por outro chunk/celula
                    // vizinha antes dessa chamada), NAO disfarca por cima - senao ao desfazer o
                    // disfarce no final a gente apaga um tile que ja estava certo, criando um
                    // buraco na linha (era exatamente o "alguns pontos falhando" reportado).
                    if (overlayLayer.GetCellSourceId(neighbor) != -1)
                    {
                        disguisedCells.Remove(neighbor);
                        continue;
                    }

                    if (groundLayer.GetCellSourceId(neighbor) == -1)
                    {
                        continue;
                    }

                    var neighborTileData = groundLayer.GetCellTileData(neighbor);

                    if (neighborTileData == null || neighborTileData.TerrainSet != biomeDef.TerrainSet)
                    {
                        disguisedCells.Remove(neighbor);
                        continue;
                    }

                    overlayLayer.SetCell(neighbor, biomeDef.BorderCapSourceId, biomeDef.InteriorAtlasCoord);
                }
            }

            var junctionCellsArray = new Godot.Collections.Array<Vector2I>();

            foreach (var cell in junctionCells)
            {
                junctionCellsArray.Add(cell);
            }

            overlayLayer.SetCellsTerrainConnect(junctionCellsArray, biomeDef.BorderCapTerrainSet, 0, false);

            foreach (var cell in disguisedCells)
            {
                overlayLayer.SetCell(cell, -1);
            }
        }

        private static bool TouchesForeignBiome(TileMapLayer layer, Vector2I cell, int terrainSet)
        {
            foreach (var offset in NeighborOffsets)
            {
                var neighbor = cell + offset;

                if (layer.GetCellSourceId(neighbor) == -1)
                {
                    continue;
                }

                var neighborTileData = layer.GetCellTileData(neighbor);

                if (neighborTileData != null && neighborTileData.TerrainSet != terrainSet)
                {
                    return true;
                }
            }

            return false;
        }

        // Devolve as celulas vizinhas de OUTRO bioma que encostam em "cells" - usado pra tambem
        // atualizar a camada de preenchimento do OUTRO lado da fronteira (o vizinho ja pintado).
        public static List<Vector2I> GetForeignNeighborCells(TileMapLayer layer, IReadOnlyCollection<Vector2I> cells, int terrainSet)
        {
            var cellSet = new HashSet<Vector2I>(cells);
            var foreign = CollectForeignNeighbors(layer, cellSet, terrainSet);
            var result = new List<Vector2I>();

            foreach (var entry in foreign)
            {
                result.Add(entry.Cell);
            }

            return result;
        }

        private struct ForeignCellInfo
        {
            public Vector2I Cell;
            public int SourceId;
            public Vector2I AtlasCoord;
            public int AlternativeId;
            public int TerrainSet;
        }

        private static List<ForeignCellInfo> CollectForeignNeighbors(TileMapLayer layer, HashSet<Vector2I> cellSet, int terrainSet)
        {
            var result = new List<ForeignCellInfo>();
            var seen = new HashSet<Vector2I>();

            foreach (var cell in cellSet)
            {
                foreach (var offset in NeighborOffsets)
                {
                    var neighbor = cell + offset;

                    if (cellSet.Contains(neighbor) || !seen.Add(neighbor))
                    {
                        continue;
                    }

                    if (layer.GetCellSourceId(neighbor) == -1)
                    {
                        continue;
                    }

                    var tileData = layer.GetCellTileData(neighbor);

                    if (tileData == null || tileData.TerrainSet == terrainSet)
                    {
                        continue;
                    }

                    result.Add(new ForeignCellInfo
                    {
                        Cell = neighbor,
                        SourceId = layer.GetCellSourceId(neighbor),
                        AtlasCoord = layer.GetCellAtlasCoords(neighbor),
                        AlternativeId = layer.GetCellAlternativeTile(neighbor),
                        TerrainSet = tileData.TerrainSet,
                    });
                }
            }

            return result;
        }
    }
}
