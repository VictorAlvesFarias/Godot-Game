using Godot;
using Jogo25D.Biomes;
using Jogo25D.Constants;
using System.Collections.Generic;

namespace Jogo25D.Chunks
{
    public static class ChunkGenerator
    {
        #region Core - Generation

        public static void Paint(TileMapLayer target, long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize)
        {
            var tileSet = target.TileSet;
            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;

            var biome = BiomeResolver.Resolve(worldSeed, dimensionId, baseCellX + chunkSize / 2);
            var biomeDef = BiomeDB.Get(biome);

            var noise = new FastNoiseLite
            {
                Seed = (int)CombineSeed(worldSeed, dimensionId, chunkCoord),
                Frequency = biomeDef.NoiseFrequency,
            };
            var solidCells = new Godot.Collections.Array<Vector2I>();

            for (int localX = 0; localX < chunkSize; localX++)
            {
                var worldX = baseCellX + localX;
                var groundHeight = biomeDef.HeightOffset + Mathf.RoundToInt(noise.GetNoise1D(worldX) * biomeDef.HeightAmplitude);

                for (int localY = 0; localY < chunkSize; localY++)
                {
                    var worldY = baseCellY + localY;

                    if (worldY < groundHeight)
                    {
                        continue;
                    }

                    solidCells.Add(new Vector2I(worldX, worldY));
                }
            }

            if (tileSet.GetTerrainSetsCount() > 0)
            {
                AddSolidBorderNeighbors(target, solidCells, baseCellX, baseCellY, chunkSize, biomeDef.TerrainSet);

                var cellsToConnect = new List<Vector2I>(solidCells);

                BiomeTerrainConnector.Connect(target, cellsToConnect, biomeDef);
                BiomeTerrainConnector.ReconnectForeignBorder(target, cellsToConnect, biomeDef);
            }
            else
            {
                var (sourceId, atlasCoord) = GetFallbackTile(tileSet);

                foreach (var cell in solidCells)
                {
                    target.SetCell(cell, sourceId, atlasCoord);
                }
            }
        }

        private static void AddSolidBorderNeighbors(TileMapLayer target, Godot.Collections.Array<Vector2I> solidCells, int baseCellX, int baseCellY, int chunkSize, int terrainSet)
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

        private static void AddIfSolid(TileMapLayer target, Godot.Collections.Array<Vector2I> solidCells, Vector2I cell, int terrainSet)
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

        public static void Erase(TileMapLayer target, Vector2I chunkCoord, int chunkSize)
        {
            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;

            for (int localX = 0; localX < chunkSize; localX++)
            {
                for (int localY = 0; localY < chunkSize; localY++)
                {
                    target.SetCell(new Vector2I(baseCellX + localX, baseCellY + localY), -1);
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
