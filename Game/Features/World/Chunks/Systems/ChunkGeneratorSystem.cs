using Godot;
using Jogo25D.Biomes;
using Jogo25D.Constants;
using Jogo25D.Structures;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jogo25D.Chunks
{
    public class ChunkGeneratorSystem
    {
        #region Core - Generation

        public void Paint(TerrainLayer target, TerrainLayer baseTarget, long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize)
        {
            var tileSet = target.TileSet;
            var worldScale = GetWorldScale(tileSet);
            var (solidCellsByBiome, columnSurfaces) = ResolveSolidCellsByBiome(worldSeed, dimensionId, chunkCoord, chunkSize, worldScale);

            if (tileSet.GetTerrainSetsCount() > 0)
            {
                var biomeGroups = BuildBiomeGroups(target, solidCellsByBiome, chunkCoord, chunkSize);

                foreach (var group in biomeGroups)
                {
                    target.Connect(group.Cells, group.BiomeDef.TerrainSet);
                }

                foreach (var group in biomeGroups)
                {
                    target.ReconnectForeignBorder(group.Cells, group.BiomeDef.TerrainSet, StructureDB.AllTerrainSets);
                }

                if (baseTarget != null)
                {
                    foreach (var group in biomeGroups)
                    {
                        baseTarget.ConnectDependent(target, group.Cells, group.BiomeDef.BorderCapTerrainSet);
                    }

                    foreach (var group in biomeGroups)
                    {
                        baseTarget.ReconnectForeignBorderDependent(target, group.Cells, group.BiomeDef.BorderCapTerrainSet, StructureDB.AllTerrainSets);
                    }
                }

                PlaceStructures(target, baseTarget, columnSurfaces, worldSeed, dimensionId, chunkCoord, chunkSize, worldScale);
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

        public async Task PaintAsync(TerrainLayer target, TerrainLayer baseTarget, long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize, int cellsPerFrame = 200)
        {
            var tileSet = target.TileSet;
            var worldScale = GetWorldScale(tileSet);
            var (solidCellsByBiome, columnSurfaces) = ResolveSolidCellsByBiome(worldSeed, dimensionId, chunkCoord, chunkSize, worldScale);

            if (tileSet.GetTerrainSetsCount() > 0)
            {
                var biomeGroups = BuildBiomeGroups(target, solidCellsByBiome, chunkCoord, chunkSize);

                foreach (var group in biomeGroups)
                {
                    await target.ConnectAsync(group.Cells, group.BiomeDef.TerrainSet, cellsPerFrame);
                }

                foreach (var group in biomeGroups)
                {
                    await target.ReconnectForeignBorderAsync(group.Cells, group.BiomeDef.TerrainSet, cellsPerFrame, StructureDB.AllTerrainSets);
                }

                if (baseTarget != null)
                {
                    foreach (var group in biomeGroups)
                    {
                        await baseTarget.ConnectDependentAsync(target, group.Cells, group.BiomeDef.BorderCapTerrainSet, cellsPerFrame);
                    }

                    foreach (var group in biomeGroups)
                    {
                        await baseTarget.ReconnectForeignBorderDependentAsync(target, group.Cells, group.BiomeDef.BorderCapTerrainSet, cellsPerFrame, StructureDB.AllTerrainSets);
                    }
                }

                PlaceStructures(target, baseTarget, columnSurfaces, worldSeed, dimensionId, chunkCoord, chunkSize, worldScale);
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

        #endregion

        #region Core - Biome resolution

        private const float BiomeNoiseFrequency = 0.004f;
        private const float MinBiomeBandWidth = 64f;
        private const int BiomeSmoothingSampleCount = 5;
        private const float WarpNoiseFrequency = 0.04f;
        private const float WarpAmplitude = 48f;
        private const int WarpFractalOctaves = 2;
        private const float WarpFractalLacunarity = 2.3f;
        private const float WarpFractalGain = 0.55f;
        private const float FadeRange = 0.2f;

        public string ResolveBiome(long worldSeed, string dimensionId, int worldX, int worldY)
        {
            var baseValue = GetSmoothedBaseNoiseValue(worldSeed, dimensionId, worldX);
            var proximity = Mathf.Clamp(1f - Mathf.Abs(baseValue) / FadeRange, 0f, 1f);

            if (proximity <= 0f)
            {
                return baseValue < 0f ? BiomeDB.LimeGroundId : BiomeDB.OliveGroundId;
            }

            var warpNoise = new FastNoiseLite
            {
                Seed = (int)CombineBiomeSeed(worldSeed, dimensionId, "biome_warp"),
                Frequency = WarpNoiseFrequency,
                FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
                FractalOctaves = WarpFractalOctaves,
                FractalLacunarity = WarpFractalLacunarity,
                FractalGain = WarpFractalGain,
            };

            var warpOffset = Mathf.RoundToInt(warpNoise.GetNoise1D(worldY) * WarpAmplitude * proximity);
            var shiftedValue = GetSmoothedBaseNoiseValue(worldSeed, dimensionId, worldX + warpOffset);

            return shiftedValue < 0f ? BiomeDB.LimeGroundId : BiomeDB.OliveGroundId;
        }

        private static float GetSmoothedBaseNoiseValue(long worldSeed, string dimensionId, int worldX)
        {
            var half = BiomeSmoothingSampleCount / 2;
            var step = MinBiomeBandWidth / BiomeSmoothingSampleCount;
            var sum = 0f;

            for (int i = -half; i <= half; i++)
            {
                sum += GetBaseNoiseValue(worldSeed, dimensionId, worldX + Mathf.RoundToInt(i * step));
            }

            return sum / BiomeSmoothingSampleCount;
        }

        private static float GetBaseNoiseValue(long worldSeed, string dimensionId, int worldX)
        {
            var noise = new FastNoiseLite
            {
                Seed = (int)CombineBiomeSeed(worldSeed, dimensionId, "biome"),
                Frequency = BiomeNoiseFrequency,
            };

            return noise.GetNoise1D(worldX);
        }

        private static long CombineBiomeSeed(long worldSeed, string dimensionId, string tag)
        {
            unchecked
            {
                long hash = worldSeed;
                hash = hash * 397 ^ WorldRandom.StableStringHash(dimensionId);
                hash = hash * 397 ^ WorldRandom.StableStringHash(tag);
                return hash;
            }
        }

        #endregion

        #region Core - Terrain resolution

        private readonly struct ColumnSurface
        {
            public readonly int WorldX;
            public readonly int GroundHeight;
            public readonly string Biome;

            public ColumnSurface(int worldX, int groundHeight, string biome)
            {
                WorldX = worldX;
                GroundHeight = groundHeight;
                Biome = biome;
            }
        }

        private (Dictionary<string, List<Vector2I>> SolidCellsByBiome, List<ColumnSurface> ColumnSurfaces) ResolveSolidCellsByBiome(long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize, int worldScale)
        {
            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;
            var solidCellsByBiome = new Dictionary<string, List<Vector2I>>();
            var columnSurfaces = new List<ColumnSurface>();
            var heightNoiseByBiome = new Dictionary<string, FastNoiseLite>();

            for (int localX = 0; localX < chunkSize; localX++)
            {
                var worldX = baseCellX + localX;
                var columnBiome = ResolveBiome(worldSeed, dimensionId, worldX, baseCellY + chunkSize / 2);
                var columnBiomeDef = BiomeDB.Get(columnBiome);

                if (!heightNoiseByBiome.TryGetValue(columnBiome, out var heightNoise))
                {
                    var noiseSeed = unchecked((long)worldSeed * 397 ^ WorldRandom.StableStringHash(dimensionId));

                    heightNoise = new FastNoiseLite
                    {
                        Seed = (int)noiseSeed,
                        Frequency = columnBiomeDef.NoiseFrequency / worldScale,
                    };

                    heightNoiseByBiome[columnBiome] = heightNoise;
                }

                var groundHeight = columnBiomeDef.HeightOffset * worldScale + Mathf.RoundToInt(heightNoise.GetNoise1D(worldX) * columnBiomeDef.HeightAmplitude * worldScale);

                columnSurfaces.Add(new ColumnSurface(worldX, groundHeight, columnBiome));

                for (int localY = 0; localY < chunkSize; localY++)
                {
                    var worldY = baseCellY + localY;

                    if (worldY < groundHeight)
                    {
                        continue;
                    }

                    var cellBiome = ResolveBiome(worldSeed, dimensionId, worldX, worldY);

                    if (!solidCellsByBiome.TryGetValue(cellBiome, out var cells))
                    {
                        cells = new List<Vector2I>();
                        solidCellsByBiome[cellBiome] = cells;
                    }

                    cells.Add(new Vector2I(worldX, worldY));
                }
            }

            return (solidCellsByBiome, columnSurfaces);
        }

        private static List<(BiomeDefinition BiomeDef, List<Vector2I> Cells)> BuildBiomeGroups(TerrainLayer target, Dictionary<string, List<Vector2I>> solidCellsByBiome, Vector2I chunkCoord, int chunkSize)
        {
            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;
            var biomeGroups = new List<(BiomeDefinition BiomeDef, List<Vector2I> Cells)>();

            foreach (var entry in solidCellsByBiome)
            {
                var biomeDef = BiomeDB.Get(entry.Key);
                var cells = entry.Value;

                AddSolidBorderNeighbors(target, cells, baseCellX, baseCellY, chunkSize, biomeDef.TerrainSet);

                biomeGroups.Add((biomeDef, cells));
            }

            return biomeGroups;
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

        #endregion

        #region Core - Structure placement

        private static void PlaceStructures(TerrainLayer target, TerrainLayer baseTarget, List<ColumnSurface> columnSurfaces, long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize, int worldScale)
        {
            if (target == null)
            {
                return;
            }

            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;
            var cellsByTerrainSet = new Dictionary<int, List<Vector2I>>();
            var lastRightEdgeByStructure = new Dictionary<string, int>();
            var minBoundsGapTiles = StructurePlacementConstants.MinBoundsGapTiles;

            foreach (var column in columnSurfaces)
            {
                var biomeDef = BiomeDB.Get(column.Biome);

                if (biomeDef.StructureIds == null || biomeDef.StructureIds.Count == 0)
                {
                    continue;
                }

                var localX = column.WorldX - baseCellX;
                var localSurfaceY = column.GroundHeight - baseCellY;

                if (localSurfaceY < 0 || localSurfaceY >= chunkSize)
                {
                    continue;
                }

                foreach (var structureId in biomeDef.StructureIds)
                {
                    var structure = StructureDB.Get(structureId);

                    if (structure == null || structure.Chance <= 0f)
                    {
                        continue;
                    }

                    if (!lastRightEdgeByStructure.ContainsKey(structureId))
                    {
                        var spanLookback = Mathf.Max(StructurePlacementConstants.MaxSpacingLookbackTiles, structure.GetMaxRightExtent(worldScale));

                        lastRightEdgeByStructure[structureId] = ResolveLastRightEdgeBefore(
                            structure,
                            worldSeed,
                            dimensionId,
                            baseCellX,
                            spanLookback,
                            minBoundsGapTiles,
                            worldScale);
                    }

                    if (WorldRandom.StructureRandom01(worldSeed, dimensionId, structureId, column.WorldX, 0) >= structure.Chance)
                    {
                        continue;
                    }

                    var bounds = structure.GetBounds(worldSeed, dimensionId, column.WorldX, worldScale);
                    var leftX = column.WorldX - bounds.Left;
                    var rightX = column.WorldX + bounds.Right;

                    if (leftX < baseCellX || rightX >= baseCellX + chunkSize)
                    {
                        continue;
                    }

                    if (!IsStructureVolumeClear(target, baseTarget, column.WorldX, column.GroundHeight, bounds))
                    {
                        continue;
                    }

                    var candidateLeftX = column.WorldX - bounds.Left;
                    var hasPreviousRightEdge = lastRightEdgeByStructure.TryGetValue(structureId, out var lastRightEdge) && lastRightEdge != int.MinValue;

                    if (hasPreviousRightEdge && candidateLeftX <= lastRightEdge + minBoundsGapTiles)
                    {
                        continue;
                    }

                    var groups = structure.CollectCells(new Vector2I(column.WorldX, column.GroundHeight), worldSeed, dimensionId, worldScale);

                    foreach (var group in groups)
                    {
                        if (!cellsByTerrainSet.TryGetValue(group.TerrainSet, out var cells))
                        {
                            cells = new List<Vector2I>();
                            cellsByTerrainSet[group.TerrainSet] = cells;
                        }

                        cells.AddRange(group.Cells);
                    }

                    lastRightEdgeByStructure[structureId] = column.WorldX + bounds.Right;
                }
            }

            foreach (var entry in cellsByTerrainSet)
            {
                target.Connect(entry.Value, entry.Key);
            }
        }

        // Escaneia pra tras do inicio do chunk (fora do range de colunas desse chunk) pra achar
        // a ultima instancia dessa estrutura que ficaria proxima o bastante pra ainda contar no
        // espacamento minimo - sem isso, o cursor de espacamento resetava a cada chunk novo e
        // duas instancias em chunks vizinhos podiam nascer coladas.
        private static int ResolveLastRightEdgeBefore(
            StructureDefinition structure,
            long worldSeed,
            string dimensionId,
            int chunkStartX,
            int lookbackTiles,
            int minBoundsGapTiles,
            int worldScale)
        {
            var scanStart = chunkStartX - lookbackTiles;
            var lastRightEdge = int.MinValue;

            for (int worldX = scanStart; worldX < chunkStartX; worldX++)
            {
                if (WorldRandom.StructureRandom01(worldSeed, dimensionId, structure.Id, worldX, 0) >= structure.Chance)
                {
                    continue;
                }

                var bounds = structure.GetBounds(worldSeed, dimensionId, worldX, worldScale);
                var candidateLeftX = worldX - bounds.Left;

                if (lastRightEdge != int.MinValue && candidateLeftX <= lastRightEdge + minBoundsGapTiles)
                {
                    continue;
                }

                lastRightEdge = worldX + bounds.Right;
            }

            return lastRightEdge;
        }

        private static bool IsStructureVolumeClear(TerrainLayer target, TerrainLayer baseTarget, int worldX, int groundHeight, StructureBounds bounds)
        {
            var leftX = worldX - bounds.Left;
            var rightX = worldX + bounds.Right;
            var topY = groundHeight - bounds.Top;
            var bottomY = groundHeight - 1;

            for (int x = leftX; x <= rightX; x++)
            {
                for (int y = topY; y <= bottomY; y++)
                {
                    var cell = new Vector2I(x, y);

                    if (target.GetCellSourceId(cell) != -1)
                    {
                        return false;
                    }

                    if (baseTarget != null && baseTarget.GetCellSourceId(cell) != -1)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion

        #region Core - Chunk lifecycle

        public void Erase(TileMapLayer target, TileMapLayer baseTarget, Vector2I chunkCoord, int chunkSize)
        {
            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;

            for (int localX = 0; localX < chunkSize; localX++)
            {
                for (int localY = 0; localY < chunkSize; localY++)
                {
                    var cell = new Vector2I(baseCellX + localX, baseCellY + localY);

                    target.SetCell(cell, -1);
                    baseTarget?.SetCell(cell, -1);
                }
            }
        }

        public async Task EraseAsync(TileMapLayer target, TileMapLayer baseTarget, Vector2I chunkCoord, int chunkSize, int cellsPerFrame = 200)
        {
            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;
            var processedSinceYield = 0;

            for (int localX = 0; localX < chunkSize; localX++)
            {
                for (int localY = 0; localY < chunkSize; localY++)
                {
                    var cell = new Vector2I(baseCellX + localX, baseCellY + localY);

                    target.SetCell(cell, -1);
                    baseTarget?.SetCell(cell, -1);

                    processedSinceYield++;

                    if (processedSinceYield >= cellsPerFrame)
                    {
                        processedSinceYield = 0;

                        await target.ToSignal(target.GetTree(), SceneTree.SignalName.ProcessFrame);
                    }
                }
            }
        }

        #endregion

        #region Utils

        private static int GetWorldScale(TileSet tileSet)
        {
            var tileSize = tileSet?.TileSize.X ?? ChunkStreamingConstants.REFERENCE_TILE_SIZE;

            return Mathf.Max(1, Mathf.RoundToInt(ChunkStreamingConstants.REFERENCE_TILE_SIZE / (float)tileSize));
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

        public TileSet GetTileSet()
        {
            return GD.Load<TileSet>(Textures.Tiles.WORLD_TILE_SET);
        }

        #endregion
    }
}
