using Godot;
using Jogo25D.Biomes;
using Jogo25D.Blocks;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using Jogo25D.Utils.Coordinates;
using System;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    public sealed class LogicalLightWorld
    {
        #region Constructors

        public LogicalLightWorld(long seed, string dimensionId, int worldScale, bool procedural = true)
        {
            Seed = seed;
            DimensionId = dimensionId;
            WorldScale = worldScale;
            Procedural = procedural;

            var bound = _generator.GetSurfaceBound(worldScale);

            TerrainBottom = (CoordinateUtilities.CellToChunk(bound) + 1) * ChunkStreamingConstants.CHUNK_SIZE;
            SkyTop = _generator.GetSkyTop(worldScale);
        }

        #endregion

        #region Dinamic properties

        private const int ProceduralInterior = int.MaxValue;
        private const int StripCacheLimit = 128;

        public long Seed { get; }
        public string DimensionId { get; }
        public int WorldScale { get; }
        public int TerrainBottom { get; }
        public int SkyTop { get; }
        public bool Procedural { get; }
        public long Revision { get; private set; }
        public long BackgroundRevision { get; private set; }

        public int OpticalSkyTop => Math.Min(SkyTop, _highestEdit);
        public IEnumerable<Vector2I> BackgroundCells => _background;

        private readonly ChunkGeneratorSystem _generator = new();
        private readonly Dictionary<int, Strip> _strips = new();
        private readonly Dictionary<int, SortedDictionary<int, int>> _edits = new();
        private readonly Dictionary<int, List<Run>> _editedColumns = new();
        private readonly SortedDictionary<int, int> _placedHeights = new();
        private readonly HashSet<Vector2I> _background = new();

        private long _clock;
        private int _highestEdit = int.MaxValue;

        #endregion

        #region Core - Consulta

        public static int TileTerrain(TileMapLayer layer, Vector2I cell)
        {
            var terrain = layer.GetCellTileData(cell)?.TerrainSet ?? -1;

            return terrain >= 0 ? terrain : layer.GetCellSourceId(cell);
        }

        public static byte TerrainOpacity(int terrain)
        {
            return terrain < 0 ? (byte)0 : (byte)255;
        }

        public IReadOnlyList<Run> OpticalColumn(int x)
        {
            return Column(x);
        }

        public int Terrain(int x, int y)
        {
            var material = Material(x, y);

            if (material != ProceduralInterior)
            {
                return material;
            }

            return BiomeDB.Get(_generator.GetBiomeIdAtPosition(Seed, DimensionId, x, y)).TerrainSet;
        }

        public byte Opacity(int x, int y)
        {
            return TerrainOpacity(Material(x, y));
        }

        public bool HasBackground(int x, int y)
        {
            return _background.Contains(new Vector2I(x, y));
        }

        private int Material(int x, int y)
        {
            foreach (var run in Column(x))
            {
                if (y < run.Start)
                {
                    break;
                }

                if (y < run.End)
                {
                    return run.Terrain;
                }
            }

            return -1;
        }

        #endregion

        #region Core - Mutacao

        public IEnumerable<(int X, int Y, int Terrain)> Edits()
        {
            foreach (var column in _edits)
            {
                foreach (var edit in column.Value)
                {
                    yield return (column.Key, edit.Key, edit.Value);
                }
            }
        }

        public void SetBackground(int x, int y, bool present)
        {
            var cell = new Vector2I(x, y);
            var changed = present ? _background.Add(cell) : _background.Remove(cell);

            if (changed)
            {
                BackgroundRevision++;
            }
        }

        public void SetTerrain(int x, int y, int terrain)
        {
            if (!_edits.TryGetValue(x, out var column))
            {
                _edits[x] = column = new SortedDictionary<int, int>();
            }

            var hadPrevious = column.TryGetValue(y, out var previous);

            if (hadPrevious && previous == terrain)
            {
                return;
            }

            if (hadPrevious && previous >= 0 && --_placedHeights[y] == 0)
            {
                _placedHeights.Remove(y);
            }

            column[y] = terrain;

            _editedColumns.Remove(x);

            if (terrain >= 0)
            {
                _placedHeights[y] = _placedHeights.GetValueOrDefault(y) + 1;
            }

            _highestEdit = int.MaxValue;

            foreach (var height in _placedHeights.Keys)
            {
                _highestEdit = height;

                break;
            }

            Revision++;
        }

        public void ApplyMutation(int x, int y, string type, string blockId)
        {
            if (type == "wall_break")
            {
                SetBackground(x, y, false);
            }
            else if (type == "wall_place")
            {
                SetBackground(x, y, true);
            }
            else if (type == "break")
            {
                SetTerrain(x, y, -1);
            }
            else if (type == "place" && BlockDB.TryGet(blockId, out var block))
            {
                SetTerrain(x, y, block.TerrainSet ?? block.SourceId);
            }
        }

        #endregion

        #region Core - Colunas

        private static void Append(List<Run> runs, int start, int end, int terrain)
        {
            if (start >= end || terrain < 0)
            {
                return;
            }

            if (runs.Count > 0 && runs[^1].End == start && runs[^1].Terrain == terrain)
            {
                runs[^1] = runs[^1] with
                {
                    End = end
                };
            }
            else
            {
                runs.Add(new Run(start, end, terrain));
            }
        }

        private List<Run> BaseColumn(int x)
        {
            var key = CoordinateUtilities.CellToChunk(x);

            if (!_strips.TryGetValue(key, out var strip))
            {
                strip = BuildStrip(key);

                if (_strips.Count >= StripCacheLimit)
                {
                    DropOldestStrip();
                }

                _strips[key] = strip;
            }

            strip.LastUse = ++_clock;

            return strip.Columns[x];
        }

        private Strip BuildStrip(int key)
        {
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var cells = Procedural
                ? _generator.GenerateLogicalStrip(Seed, DimensionId, key, WorldScale)
                : new Dictionary<Vector2I, int>();
            var strip = new Strip();

            for (var cx = key * chunkSize; cx < key * chunkSize + chunkSize; cx++)
            {
                var column = new SortedDictionary<int, int>();

                foreach (var pair in cells)
                {
                    if (pair.Key.X == cx)
                    {
                        column[pair.Key.Y] = pair.Value;
                    }
                }

                var runs = new List<Run>();

                foreach (var pair in column)
                {
                    Append(runs, pair.Key, pair.Key + 1, pair.Value);
                }

                if (Procedural)
                {
                    Append(runs, TerrainBottom, int.MaxValue, ProceduralInterior);
                }

                strip.Columns[cx] = runs;
            }

            return strip;
        }

        private void DropOldestStrip()
        {
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var oldestKey = 0;
            var oldest = long.MaxValue;

            foreach (var pair in _strips)
            {
                if (pair.Value.LastUse < oldest)
                {
                    oldest = pair.Value.LastUse;
                    oldestKey = pair.Key;
                }
            }

            _strips.Remove(oldestKey);

            for (var cx = oldestKey * chunkSize; cx < oldestKey * chunkSize + chunkSize; cx++)
            {
                _editedColumns.Remove(cx);
            }
        }

        private List<Run> Column(int x)
        {
            var original = BaseColumn(x);

            if (!_edits.TryGetValue(x, out var edits))
            {
                return original;
            }

            if (_editedColumns.TryGetValue(x, out var cached))
            {
                return cached;
            }

            var result = new List<Run>();
            var index = 0;
            var start = original.Count > 0 ? original[0].Start : 0;

            foreach (var edit in edits)
            {
                while (index < original.Count && original[index].End <= edit.Key)
                {
                    Append(result, start, original[index].End, original[index].Terrain);

                    index++;

                    if (index < original.Count)
                    {
                        start = original[index].Start;
                    }
                }

                if (index < original.Count && original[index].Start <= edit.Key)
                {
                    Append(result, start, edit.Key, original[index].Terrain);

                    start = edit.Key + 1;
                }

                Append(result, edit.Key, edit.Key + 1, edit.Value);
            }

            while (index < original.Count)
            {
                Append(result, start, original[index].End, original[index].Terrain);

                index++;

                if (index < original.Count)
                {
                    start = original[index].Start;
                }
            }

            _editedColumns[x] = result;

            return result;
        }

        #endregion

        #region Types

        public readonly record struct Run(int Start, int End, int Terrain);

        private sealed class Strip
        {
            public readonly Dictionary<int, List<Run>> Columns = new();

            public long LastUse;
        }

        #endregion
    }
}
