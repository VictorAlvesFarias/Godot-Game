using Godot;
using Jogo25D.Biomes;
using Jogo25D.Constants;
using Jogo25D.Structures;
using Jogo25D.Light;
using Jogo25D.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jogo25D.Chunks
{
    public class ChunkGeneratorSystem
    {
        private readonly Dictionary<(long, string, string), FastNoiseLite> _noiseCache = new();
        private FastNoiseLite Noise(long seed, string dimension, string tag, float frequency)
        {
            var key = (seed, dimension, tag);
            if (!_noiseCache.TryGetValue(key, out var noise))
            {
                if (_noiseCache.Count > 64) { foreach (var old in _noiseCache.Values) old.Dispose(); _noiseCache.Clear(); }
                _noiseCache[key] = noise = new FastNoiseLite { Seed = (int)CombineBiomeSeed(seed, dimension, tag), Frequency = frequency };
                if (tag == "biome_warp")
                {
                    noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
                    noise.FractalOctaves = ChunkGenerationConstants.WARP_FRACTAL_OCTAVES;
                    noise.FractalLacunarity = ChunkGenerationConstants.WARP_FRACTAL_LACUNARITY;
                    noise.FractalGain = ChunkGenerationConstants.WARP_FRACTAL_GAIN;
                }
            }
            return noise;
        }
        #region Core - Generation

        public async Task PaintTilesAsync(TerrainLayer target, TerrainLayer baseTarget, long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize, int cellsPerFrame = 200)
        {
            var tileSet = target.TileSet;
            var worldScale = GetWorldScale(tileSet);
            // The exact same logical geometry feeds both rendering and lighting. Structures
            // are generated independently of neighbouring TileMap visibility/load order.
            var world = Game.Managers.LightMapManager.Node?.GetWorld(dimensionId)
                ?? new LogicalLightWorld(worldSeed, dimensionId, worldScale);
            var groupsByTerrain = new Dictionary<int, List<Vector2I>>();
            for (int y = chunkCoord.Y * chunkSize; y < (chunkCoord.Y + 1) * chunkSize; y++)
                for (int x = chunkCoord.X * chunkSize; x < (chunkCoord.X + 1) * chunkSize; x++)
                {
                    int terrain = world.Terrain(x, y);
                    if (terrain < 0) continue;
                    if (!groupsByTerrain.TryGetValue(terrain, out var cells)) groupsByTerrain[terrain] = cells = new();
                    cells.Add(new Vector2I(x, y));
                }

            if (tileSet.GetTerrainSetsCount() > 0)
            {
                foreach (var group in groupsByTerrain)
                    await target.ConnectAsync(group.Value, group.Key, cellsPerFrame);

                foreach (var group in groupsByTerrain)
                    await target.ReconnectForeignBorderAsync(group.Value, group.Key, cellsPerFrame);

                if (baseTarget != null)
                {
                    foreach (var group in groupsByTerrain)
                    {
                        var biome = BiomeDB.GetByTerrainSet(group.Key);
                        if (biome == null) continue;
                        await baseTarget.ConnectDependentAsync(target, group.Value, biome.BorderCapTerrainSet, cellsPerFrame);
                        await baseTarget.ReconnectForeignBorderDependentAsync(target, group.Value, biome.BorderCapTerrainSet, cellsPerFrame);
                    }
                }

            }
            else
            {
                var (sourceId, atlasCoord) = GetFallbackTile(tileSet);

                foreach (var cells in groupsByTerrain.Values)
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
            var noise = Noise(worldSeed, dimensionId, "biome", ChunkGenerationConstants.BIOME_NOISE_FREQUENCY);

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
            var warpNoise = Noise(worldSeed, dimensionId, "biome_warp", ChunkGenerationConstants.WARP_NOISE_FREQUENCY);

            return Mathf.RoundToInt(warpNoise.GetNoise1D(worldY) * ChunkGenerationConstants.WARP_AMPLITUDE * proximityToBoundary);
        }

        private string PickBiomeIdForAxisValue(float axisValue, IReadOnlyList<string> biomeIds)
        {
            var normalized = Mathf.Clamp((axisValue + 1f) * 0.5f, 0f, 0.999999f);
            var index = Mathf.Clamp((int)(normalized * biomeIds.Count), 0, biomeIds.Count - 1);

            return biomeIds[index];
        }

        #endregion

        #region Core - Terrain resolution

        public int GetSurfaceBound(int worldScale)
        {
            int bound = 1;
            foreach (var id in BiomeDB.OrderedIds)
            {
                var biome = BiomeDB.Get(id);
                bound = Mathf.Max(bound, Mathf.CeilToInt((Mathf.Abs(biome.HeightOffset) + biome.HeightAmplitude) * worldScale) + 2);
            }
            return bound;
        }

        public int GetSkyTop(int worldScale)
        {
            int height = 0;
            foreach (var id in BiomeDB.OrderedIds)
                foreach (var structureId in BiomeDB.Get(id).StructureIds)
                    height = Mathf.Max(height, StructureDB.Get(structureId)?.GetMaxTopExtent(worldScale) ?? 0);
            return -GetSurfaceBound(worldScale) - height - 1;
        }

        public Dictionary<Vector2I, int> GenerateLogicalStrip(long seed, string dimensionId, int chunkX, int worldScale)
        {
            int bound = GetSurfaceBound(worldScale);
            var result = new Dictionary<Vector2I, int>();
            var surfaces = new List<ColumnSurface>();
            for (int cy = LightingField.FloorChunk(-bound); cy <= LightingField.FloorChunk(bound); cy++)
            {
                var (groups, columns) = ResolveSolidCellsByBiome(seed, dimensionId, new(chunkX, cy), 32, worldScale);
                foreach (var group in groups)
                    foreach (var cell in group.Value) result[cell] = BiomeDB.Get(group.Key).TerrainSet;
                foreach (var column in columns)
                    if (LightingField.FloorChunk(column.GroundHeight) == cy) surfaces.Add(column);
            }
            surfaces.Sort((a, b) => a.WorldX.CompareTo(b.WorldX));
            var last = new Dictionary<string, int>();
            int left = chunkX * 32;
            foreach (var column in surfaces)
                foreach (var id in BiomeDB.Get(column.Biome).StructureIds)
                {
                    var structure = StructureDB.Get(id);
                    if (structure == null || structure.Chance <= 0) continue;
                    if (!last.ContainsKey(id))
                        last[id] = ResolveLastRightEdgeBefore(structure, seed, dimensionId, left,
                            Mathf.Max(StructurePlacementConstants.MaxSpacingLookbackTiles, structure.GetMaxRightExtent(worldScale)),
                            StructurePlacementConstants.MinBoundsGapTiles, worldScale);
                    if (WorldRandom.StructureRandom(seed, dimensionId, id, column.WorldX, 0) >= structure.Chance) continue;
                    var bounds = structure.GetBounds(seed, dimensionId, column.WorldX, worldScale);
                    if (column.WorldX - bounds.Left < left || column.WorldX + bounds.Right >= left + 32) continue;
                    if (last[id] != int.MinValue && column.WorldX - bounds.Left <= last[id] + StructurePlacementConstants.MinBoundsGapTiles) continue;
                    bool clear = true;
                    for (int x = column.WorldX - bounds.Left; x <= column.WorldX + bounds.Right && clear; x++)
                        for (int y = column.GroundHeight - bounds.Top; y < column.GroundHeight; y++)
                            if (result.ContainsKey(new(x, y))) { clear = false; break; }
                    if (!clear) continue;
                    foreach (var group in structure.CollectCells(new(column.WorldX, column.GroundHeight), seed, dimensionId, worldScale))
                        foreach (var cell in group.Cells) result[cell] = group.TerrainSet;
                    last[id] = column.WorldX + bounds.Right;
                }
            return result;
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

        #endregion

        #region Core - Structure spacing

        private int ResolveLastRightEdgeBefore(StructureDefinition structure, long worldSeed, string dimensionId, int chunkStartX, int lookbackTiles, int minBoundsGapTiles, int worldScale)
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
