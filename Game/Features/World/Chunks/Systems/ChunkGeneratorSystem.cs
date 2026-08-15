using Game.Features.World.Chunks.Singletons;
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

        public async Task PainTilesAsync(TerrainLayer target, TerrainLayer baseTarget, long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize, int cellsPerFrame = 200)
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
                    await target.ReconnectForeignBorderAsync(group.Cells, group.BiomeDef.TerrainSet, cellsPerFrame);
                }

                if (baseTarget != null)
                {
                    foreach (var group in biomeGroups)
                    {
                        await baseTarget.ConnectDependentAsync(target, group.Cells, group.BiomeDef.BorderCapTerrainSet, cellsPerFrame);
                    }

                    foreach (var group in biomeGroups)
                    {
                        await baseTarget.ReconnectForeignBorderDependentAsync(target, group.Cells, group.BiomeDef.BorderCapTerrainSet, cellsPerFrame);
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

        public async Task EraseTilesAsync(TileMapLayer target, TileMapLayer baseTarget, Vector2I chunkCoord, int chunkSize, int cellsPerFrame = 200)
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

        #region Core - Biome resolution

        public string GetBiomeIdAtPosition(long worldSeed, string dimensionId, int worldX, int worldY)
        {
            var biomeIds = BiomeDB.OrderedIds;
            var axisValue = GetSmoothedBiomeAxisValue(worldSeed, dimensionId, worldX);
            var proximityToBoundary = GetProximityToNearestBiomeBoundary(axisValue, biomeIds.Count);

            if (proximityToBoundary > 0f)
            {
                var warpOffset = GetBiomeBoundaryWarpOffset(worldSeed, dimensionId, worldY, proximityToBoundary);

                axisValue = GetSmoothedBiomeAxisValue(worldSeed, dimensionId, worldX + warpOffset);
            }

            return PickBiomeIdForAxisValue(axisValue, biomeIds);
        }

        private float GetProximityToNearestBiomeBoundary(float axisValue, int biomeCount)
        {
            if (biomeCount <= 1)
            {
                return 0f;
            }

            var bandWidth = 2f / biomeCount;
            var distanceToNearestBoundary = float.MaxValue;

            for (int i = 1; i < biomeCount; i++)
            {
                var boundary = -1f + i * bandWidth;

                distanceToNearestBoundary = Mathf.Min(distanceToNearestBoundary, Mathf.Abs(axisValue - boundary));
            }

            return Mathf.Clamp(1f - distanceToNearestBoundary / ChunkGenerationConstants.FADE_RANGE, 0f, 1f);
        }

        private int GetBiomeBoundaryWarpOffset(long worldSeed, string dimensionId, int worldY, float proximityToBoundary)
        {
            var warpNoise = new FastNoiseLite
            {
                Seed = (int)CombineBiomeSeed(worldSeed, dimensionId, "biome_warp"),
                Frequency = ChunkGenerationConstants.WARP_NOISE_FREQUENCY,
                FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
                FractalOctaves = ChunkGenerationConstants.WARP_FRACTAL_OCTAVES,
                FractalLacunarity = ChunkGenerationConstants.WARP_FRACTAL_LACUNARITY,
                FractalGain = ChunkGenerationConstants.WARP_FRACTAL_GAIN,
            };

            return Mathf.RoundToInt(warpNoise.GetNoise1D(worldY) * ChunkGenerationConstants.WARP_AMPLITUDE * proximityToBoundary);
        }

        private string PickBiomeIdForAxisValue(float axisValue, IReadOnlyList<string> biomeIds)
        {
            var normalized = Mathf.Clamp((axisValue + 1f) * 0.5f, 0f, 0.999999f);
            var index = Mathf.Clamp((int)(normalized * biomeIds.Count), 0, biomeIds.Count - 1);

            return biomeIds[index];
        }

        private float GetSmoothedBiomeAxisValue(long worldSeed, string dimensionId, int worldX)
        {
            var half = ChunkGenerationConstants.BIOME_SMOOTHING_SAMPLE_COUNT / 2;
            var step = ChunkGenerationConstants.MIN_BIOME_BAND_WIDTH / ChunkGenerationConstants.BIOME_SMOOTHING_SAMPLE_COUNT;
            var sum = 0f;

            for (int i = -half; i <= half; i++)
            {
                sum += SampleBiomeAxisNoise(worldSeed, dimensionId, worldX + Mathf.RoundToInt(i * step));
            }

            return sum / ChunkGenerationConstants.BIOME_SMOOTHING_SAMPLE_COUNT;
        }

        private float SampleBiomeAxisNoise(long worldSeed, string dimensionId, int worldX)
        {
            var noise = new FastNoiseLite
            {
                Seed = (int)CombineBiomeSeed(worldSeed, dimensionId, "biome"),
                Frequency = ChunkGenerationConstants.BIOME_NOISE_FREQUENCY,
            };

            return noise.GetNoise1D(worldX);
        }

        private long CombineBiomeSeed(long worldSeed, string dimensionId, string tag)
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
                var columnBiome = GetBiomeIdAtPosition(worldSeed, dimensionId, worldX, baseCellY + chunkSize / 2);
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

                    var cellBiome = GetBiomeIdAtPosition(worldSeed, dimensionId, worldX, worldY);

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

        private List<(BiomeDefinition BiomeDef, List<Vector2I> Cells)> BuildBiomeGroups(TerrainLayer target, Dictionary<string, List<Vector2I>> solidCellsByBiome, Vector2I chunkCoord, int chunkSize)
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

        private void AddSolidBorderNeighbors(TileMapLayer target, List<Vector2I> solidCells, int baseCellX, int baseCellY, int chunkSize, int terrainSet)
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

        private void AddIfSolid(TileMapLayer target, List<Vector2I> solidCells, Vector2I cell, int terrainSet)
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

        private void PlaceStructures(TerrainLayer target, TerrainLayer baseTarget, List<ColumnSurface> columnSurfaces, long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize, int worldScale)
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

                    if (WorldRandom.StructureRandom(worldSeed, dimensionId, structureId, column.WorldX, 0) >= structure.Chance)
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

        private int ResolveLastRightEdgeBefore( StructureDefinition structure, long worldSeed, string dimensionId, int chunkStartX, int lookbackTiles, int minBoundsGapTiles, int worldScale)
        {
            var scanStart = chunkStartX - lookbackTiles;
            var lastRightEdge = int.MinValue;

            for (int worldX = scanStart; worldX < chunkStartX; worldX++)
            {
                if (WorldRandom.StructureRandom(worldSeed, dimensionId, structure.Id, worldX, 0) >= structure.Chance)
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

        private bool IsStructureVolumeClear(TerrainLayer target, TerrainLayer baseTarget, int worldX, int groundHeight, StructureBounds bounds)
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

        #region Utils

        private int GetWorldScale(TileSet tileSet)
        {
            var tileSize = tileSet?.TileSize.X ?? ChunkStreamingConstants.REFERENCE_TILE_SIZE;

            return Mathf.Max(1, Mathf.RoundToInt(ChunkStreamingConstants.REFERENCE_TILE_SIZE / (float)tileSize));
        }

        private (int sourceId, Vector2I atlasCoord) GetFallbackTile(TileSet tileSet)
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

        #endregion
    }
}
