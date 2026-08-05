using Godot;
using Jogo25D.Biomes;
using Jogo25D.Constants;
using System.Collections.Generic;

namespace Jogo25D.Chunks
{
    public static class ChunkGenerator
    {
        #region Core - Generation

        public static void Paint(TileMapLayer target, TileMapLayer[] edgeFillTargets, TileMapLayer borderCapTarget, long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize)
        {
            var tileSet = target.TileSet;
            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;

            // A altura do relevo de cada COLUNA usa um bioma "de referencia" (resolvido no
            // centro vertical do chunk) - mantem o relevo suave, sem degrau quando a fronteira
            // corta a coluna no meio. Ja o bioma de CADA CELULA solida (usado pra escolher a
            // textura) e resolvido individualmente (X e Y), entao perto da fronteira algumas
            // celulas divergem do bioma da coluna, criando a tendrilha organica em vez de uma
            // faixa reta.
            var solidCellsByBiome = new Dictionary<BiomeType, List<Vector2I>>();
            var heightNoiseByBiome = new Dictionary<BiomeType, FastNoiseLite>();

            for (int localX = 0; localX < chunkSize; localX++)
            {
                var worldX = baseCellX + localX;
                var columnBiome = BiomeResolver.Resolve(worldSeed, dimensionId, worldX, baseCellY + chunkSize / 2);
                var columnBiomeDef = BiomeDB.Get(columnBiome);

                if (!heightNoiseByBiome.TryGetValue(columnBiome, out var heightNoise))
                {
                    heightNoise = new FastNoiseLite
                    {
                        Seed = (int)CombineSeed(worldSeed, dimensionId, chunkCoord),
                        Frequency = columnBiomeDef.NoiseFrequency,
                    };
                    heightNoiseByBiome[columnBiome] = heightNoise;
                }

                var groundHeight = columnBiomeDef.HeightOffset + Mathf.RoundToInt(heightNoise.GetNoise1D(worldX) * columnBiomeDef.HeightAmplitude);

                for (int localY = 0; localY < chunkSize; localY++)
                {
                    var worldY = baseCellY + localY;

                    if (worldY < groundHeight)
                    {
                        continue;
                    }

                    var cellBiome = BiomeResolver.Resolve(worldSeed, dimensionId, worldX, worldY);

                    if (!solidCellsByBiome.TryGetValue(cellBiome, out var cells))
                    {
                        cells = new List<Vector2I>();
                        solidCellsByBiome[cellBiome] = cells;
                    }

                    cells.Add(new Vector2I(worldX, worldY));
                }
            }

            if (tileSet.GetTerrainSetsCount() > 0)
            {
                var biomeGroups = new List<(BiomeDefinition BiomeDef, List<Vector2I> Cells)>();

                foreach (var entry in solidCellsByBiome)
                {
                    var biomeDef = BiomeDB.Get(entry.Key);
                    var cells = entry.Value;

                    AddSolidBorderNeighbors(target, cells, baseCellX, baseCellY, chunkSize, biomeDef.TerrainSet);

                    biomeGroups.Add((biomeDef, cells));
                }

                // Conecta TODOS os grupos primeiro, so depois reconecta a fronteira estrangeira
                // de cada um - senao o grupo processado primeiro veria os outros biomas (ainda
                // nao pintados nessa mesma chamada) como vazio, tratando a divisa como
                // precipicio de um lado so.
                foreach (var group in biomeGroups)
                {
                    BiomeTerrainConnector.Connect(target, group.Cells, group.BiomeDef);
                }

                foreach (var group in biomeGroups)
                {
                    BiomeTerrainConnector.ReconnectForeignBorder(target, group.Cells, group.BiomeDef);
                }

                if (borderCapTarget != null)
                {
                    foreach (var group in biomeGroups)
                    {
                        BiomeTerrainConnector.PaintBorderCap(borderCapTarget, target, group.Cells, group.BiomeDef);
                    }
                }

                if (edgeFillTargets != null)
                {
                    foreach (var group in biomeGroups)
                    {
                        var foreignCells = BiomeTerrainConnector.GetForeignNeighborCells(target, group.Cells, group.BiomeDef.TerrainSet);

                        BiomeTerrainConnector.PaintEdgeFillOverlay(target, edgeFillTargets, group.Cells);
                        BiomeTerrainConnector.PaintEdgeFillOverlay(target, edgeFillTargets, foreignCells);
                    }
                }
            }
            else
            {
                var (sourceId, atlasCoord) = GetFallbackTile(tileSet);

                foreach (var cells in solidCellsByBiome.Values)
                {
                    foreach (var cell in cells)
                    {
                        target.SetCell(cell, sourceId, atlasCoord);
                    }
                }
            }
        }

        private static void AddSolidBorderNeighbors(TileMapLayer target, List<Vector2I> solidCells, int baseCellX, int baseCellY, int chunkSize, int terrainSet)
        {
            for (int x = baseCellX - 1; x <= baseCellX + chunkSize; x++)
            {
                AddIfSolid(target, solidCells, new Vector2I(x, baseCellY - 1), terrainSet);
                AddIfSolid(target, solidCells, new Vector2I(x, baseCellY + chunkSize), terrainSet);
            }

            for (int y = baseCellY; y < baseCellY + chunkSize; y++)
            {
                AddIfSolid(target, solidCells, new Vector2I(baseCellX - 1, y), terrainSet);
                AddIfSolid(target, solidCells, new Vector2I(baseCellX + chunkSize, y), terrainSet);
            }
        }

        private static void AddIfSolid(TileMapLayer target, List<Vector2I> solidCells, Vector2I cell, int terrainSet)
        {
            if (target.GetCellSourceId(cell) == -1)
            {
                return;
            }

            var neighborTileData = target.GetCellTileData(cell);

            if (neighborTileData != null && neighborTileData.TerrainSet != terrainSet)
            {
                return;
            }

            solidCells.Add(cell);
        }

        public static void Erase(TileMapLayer target, TileMapLayer[] edgeFillTargets, TileMapLayer borderCapTarget, Vector2I chunkCoord, int chunkSize)
        {
            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;

            for (int localX = 0; localX < chunkSize; localX++)
            {
                for (int localY = 0; localY < chunkSize; localY++)
                {
                    var cell = new Vector2I(baseCellX + localX, baseCellY + localY);

                    target.SetCell(cell, -1);
                    borderCapTarget?.SetCell(cell, -1);

                    if (edgeFillTargets != null)
                    {
                        foreach (var edgeFillTarget in edgeFillTargets)
                        {
                            edgeFillTarget?.SetCell(cell, -1);
                        }
                    }
                }
            }
        }

        private static (int sourceId, Vector2I atlasCoord) GetFallbackTile(TileSet tileSet)
        {
            for (int i = 0; i < tileSet.GetSourceCount(); i++)
            {
                var sourceId = tileSet.GetSourceId(i);

                if (tileSet.GetSource(sourceId) is TileSetAtlasSource atlasSource && atlasSource.GetTilesCount() > 0)
                {
                    return (sourceId, atlasSource.GetTileId(0));
                }
            }

            return (0, Vector2I.Zero);
        }

        public static TileSet GetTileSet()
        {
            return GD.Load<TileSet>(Textures.Tiles.WORLD_TILE_SET);
        }

        private static long CombineSeed(long worldSeed, string dimensionId, Vector2I chunkCoord)
        {
            unchecked
            {
                long hash = worldSeed;

                hash = hash * 397 ^ StableStringHash(dimensionId);
                hash = hash * 397 ^ chunkCoord.X;
                hash = hash * 397 ^ chunkCoord.Y;

                return hash;
            }
        }

        private static long StableStringHash(string value)
        {
            unchecked
            {
                long hash = 1469598103934665603;

                foreach (var c in value)
                {
                    hash ^= c;
                    hash *= 1099511628211;
                }

                return hash;
            }
        }

        #endregion
    }
}
