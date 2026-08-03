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

        private static readonly Vector2I[] CardinalOffsets = new Vector2I[]
        {
            new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1),
        };

        // Copia, na camada de preenchimento de borda, o MESMO tile que ja esta na camada de
        // chao - mas SO pras celulas que encostam (lado a lado) em outro terrain_set. Essa
        // camada carrega o shader de preenchimento (ver tile_edge_fill.gdshader), entao so essas
        // celulas recebem o efeito; o resto do chao fica intocado. Celulas sem vizinho
        // estrangeiro sao limpas dessa camada.
        public static void PaintEdgeFillOverlay(TileMapLayer groundLayer, TileMapLayer fillLayer, IEnumerable<Vector2I> cells)
        {
            foreach (var cell in cells)
            {
                var tileData = groundLayer.GetCellTileData(cell);

                if (tileData == null)
                {
                    fillLayer.SetCell(cell, -1);

                    continue;
                }

                var bordersForeignTileset = false;

                foreach (var offset in CardinalOffsets)
                {
                    var neighbor = cell + offset;

                    if (groundLayer.GetCellSourceId(neighbor) == -1)
                    {
                        continue;
                    }

                    var neighborTileData = groundLayer.GetCellTileData(neighbor);

                    if (neighborTileData == null || neighborTileData.TerrainSet == tileData.TerrainSet)
                    {
                        continue;
                    }

                    bordersForeignTileset = true;

                    break;
                }

                if (!bordersForeignTileset)
                {
                    fillLayer.SetCell(cell, -1);

                    continue;
                }

                fillLayer.SetCell(cell, groundLayer.GetCellSourceId(cell), groundLayer.GetCellAtlasCoords(cell), groundLayer.GetCellAlternativeTile(cell));
            }
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
