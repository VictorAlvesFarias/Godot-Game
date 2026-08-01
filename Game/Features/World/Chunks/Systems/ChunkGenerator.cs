using Godot;
using System.Collections.Generic;

namespace Jogo25D.Chunks
{
    public static class ChunkGenerator
    {
        public const int TerrainSetId = 0;
        public const int TerrainId = 0;

        private const string OverworldTileSetPath = "res://Assets/Textures/Tiles/lime_ground/lime_ground_tileset.tres";
        private const string UpsidedownTileSetPath = "res://Assets/Textures/Tiles/olive_ground/olive_ground_tileset.tres";

        private static readonly Dictionary<string, TileSet> _tileSetCache = new();

        public static void Paint(TileMapLayer target, long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize)
        {
            var tileSet = target.TileSet;
            var noise = new FastNoiseLite
            {
                Seed = (int)CombineSeed(worldSeed, dimensionId, chunkCoord),
                Frequency = 0.05f,
            };

            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;
            var solidCells = new Godot.Collections.Array<Vector2I>();

            for (int localX = 0; localX < chunkSize; localX++)
            {
                var worldX = baseCellX + localX;
                var groundHeight = Mathf.RoundToInt(noise.GetNoise1D(worldX) * 4f);

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

            if (tileSet.GetTerrainSetsCount() > TerrainSetId)
            {
                AddSolidBorderNeighbors(target, solidCells, baseCellX, baseCellY, chunkSize);

                target.SetCellsTerrainConnect(solidCells, TerrainSetId, TerrainId, false);
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

        private static void AddSolidBorderNeighbors(TileMapLayer target, Godot.Collections.Array<Vector2I> solidCells, int baseCellX, int baseCellY, int chunkSize)
        {
            for (int x = baseCellX - 1; x <= baseCellX + chunkSize; x++)
            {
                AddIfSolid(target, solidCells, new Vector2I(x, baseCellY - 1));
                AddIfSolid(target, solidCells, new Vector2I(x, baseCellY + chunkSize));
            }

            for (int y = baseCellY; y < baseCellY + chunkSize; y++)
            {
                AddIfSolid(target, solidCells, new Vector2I(baseCellX - 1, y));
                AddIfSolid(target, solidCells, new Vector2I(baseCellX + chunkSize, y));
            }
        }

        private static void AddIfSolid(TileMapLayer target, Godot.Collections.Array<Vector2I> solidCells, Vector2I cell)
        {
            if (target.GetCellSourceId(cell) != -1)
            {
                solidCells.Add(cell);
            }
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

        public static TileSet GetTileSet(string dimensionId)
        {
            if (_tileSetCache.TryGetValue(dimensionId, out var cached))
            {
                return cached;
            }

            var path = dimensionId == "upsidedown" ? UpsidedownTileSetPath : OverworldTileSetPath;
            var tileSet = GD.Load<TileSet>(path);

            _tileSetCache[dimensionId] = tileSet;

            return tileSet;
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
    }
}
