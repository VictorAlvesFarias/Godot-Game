using Godot;
using Jogo25D.Blocks;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using System;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    /// <summary>
    /// Procedural columns + sparse final edits. No TileMap, scene visibility, or loaded-chunk input.
    /// Columns are compressed into material runs. Sun queries skip empty vertical space and
    /// test runs, so a distant ceiling remains an occluder without rendering its geometry.
    /// </summary>
    public sealed class LogicalLightWorld : ILightGeometry
    {
        private const int ProceduralInterior = int.MaxValue;
        public readonly record struct Run(int Start, int End, int Terrain);
        private sealed class Strip
        {
            public readonly Dictionary<int, List<Run>> Columns = new();
            public long LastUse;
        }
        private readonly ChunkGeneratorSystem _generator = new();
        private readonly Dictionary<int, Strip> _strips = new();
        private readonly Dictionary<int, SortedDictionary<int, int>> _edits = new();
        private readonly Dictionary<int, List<Run>> _editedColumns = new();
        private long _clock;
        private int _highestEdit = int.MaxValue;
        private readonly SortedDictionary<int, int> _placedHeights = new();
        private readonly Queue<(long Revision, LightCell Cell)> _changes = new();
        private readonly bool _procedural;
        public long Seed { get; }
        public string DimensionId { get; }
        public int WorldScale { get; }
        public int TerrainBottom { get; }
        public int SkyTop { get; }
        public long Revision { get; private set; }
        public LightingField Field { get; }
        public bool Procedural => _procedural;

        public static int TileTerrain(TileMapLayer layer, Vector2I cell)
        {
            int terrain = layer.GetCellTileData(cell)?.TerrainSet ?? -1;
            return terrain >= 0 ? terrain : layer.GetCellSourceId(cell);
        }

        public bool TryGetChanges(long since, out List<LightCell> cells)
        {
            cells = new();
            if (since == Revision) return true;
            if (_changes.Count == 0 || _changes.Peek().Revision > since + 1) return false;
            foreach (var change in _changes) if (change.Revision > since) cells.Add(change.Cell);
            return true;
        }

        public IEnumerable<(int X, int Y, int Terrain)> Edits()
        {
            foreach (var column in _edits)
                foreach (var edit in column.Value) yield return (column.Key, edit.Key, edit.Value);
        }

        public LogicalLightWorld(long seed, string dimensionId, int worldScale, bool procedural = true)
        {
            _procedural = procedural;
            Seed = seed;
            DimensionId = dimensionId;
            WorldScale = worldScale;
            int bound = _generator.GetSurfaceBound(worldScale);
            TerrainBottom = (LightingField.FloorChunk(bound) + 1) * 32;
            SkyTop = _generator.GetSkyTop(worldScale);
            Field = new LightingField(this);
        }

        private List<Run> BaseColumn(int x)
        {
            int key = LightingField.FloorChunk(x);
            if (!_strips.TryGetValue(key, out var strip))
            {
                var cells = _procedural ? _generator.GenerateLogicalStrip(Seed, DimensionId, key, WorldScale) : new Dictionary<Vector2I, int>();
                strip = new Strip();
                for (int cx = key * 32; cx < key * 32 + 32; cx++)
                {
                    var column = new SortedDictionary<int, int>();
                    foreach (var pair in cells) if (pair.Key.X == cx) column[pair.Key.Y] = pair.Value;
                    var runs = new List<Run>();
                    foreach (var pair in column) Append(runs, pair.Key, pair.Key + 1, pair.Value);
                    if (_procedural) Append(runs, TerrainBottom, int.MaxValue, ProceduralInterior);
                    strip.Columns[cx] = runs;
                }
                if (_strips.Count >= 128)
                {
                    int oldestKey = 0; long oldest = long.MaxValue;
                    foreach (var pair in _strips) if (pair.Value.LastUse < oldest) { oldest = pair.Value.LastUse; oldestKey = pair.Key; }
                    _strips.Remove(oldestKey);
                    for (int cx = oldestKey * 32; cx < oldestKey * 32 + 32; cx++) _editedColumns.Remove(cx);
                }
                _strips[key] = strip;
            }
            strip.LastUse = ++_clock;
            return strip.Columns[x];
        }

        private static void Append(List<Run> runs, int start, int end, int terrain)
        {
            if (start >= end || terrain < 0) return;
            if (runs.Count > 0 && runs[^1].End == start && runs[^1].Terrain == terrain)
                runs[^1] = runs[^1] with { End = end };
            else runs.Add(new(start, end, terrain));
        }

        private List<Run> Column(int x)
        {
            var original = BaseColumn(x);
            if (!_edits.TryGetValue(x, out var edits)) return original;
            if (_editedColumns.TryGetValue(x, out var cached)) return cached;
            var result = new List<Run>();
            int index = 0, start = original.Count > 0 ? original[0].Start : 0;
            foreach (var edit in edits)
            {
                while (index < original.Count && original[index].End <= edit.Key)
                {
                    Append(result, start, original[index].End, original[index].Terrain);
                    index++;
                    if (index < original.Count) start = original[index].Start;
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
                if (index < original.Count) start = original[index].Start;
            }
            _editedColumns[x] = result;
            return result;
        }

        public int Terrain(int x, int y)
        {
            int material = Material(x, y);
            return material == ProceduralInterior
                ? Jogo25D.Biomes.BiomeDB.Get(_generator.GetBiomeIdAtPosition(Seed, DimensionId, x, y)).TerrainSet
                : material;
        }

        private int Material(int x, int y)
        {
            foreach (var run in Column(x))
            {
                if (y < run.Start) break;
                if (y < run.End) return run.Terrain;
            }
            return -1;
        }

        public static byte TerrainOpacity(int terrain) => terrain < 0 ? (byte)0 : terrain == TerrainsConstants.LIME_LEAF ? (byte)72 : (byte)255;
        public byte Opacity(int x, int y) => TerrainOpacity(Material(x, y));

        public byte Sky(int x, int y)
        {
            double transmission = 1;
            foreach (var run in Column(x))
            {
                if (run.Start > y) break;
                byte opacity = TerrainOpacity(run.Terrain);
                if (opacity == 255) return 0;
                transmission *= Math.Pow(1 - opacity / 255.0, Math.Min((long)y + 1, run.End) - run.Start);
                if (transmission < 1.0 / 255) return 0;
            }
            return (byte)Math.Round(transmission * 255);
        }

        public void SetTerrain(int x, int y, int terrain)
        {
            if (!_edits.TryGetValue(x, out var column)) _edits[x] = column = new();
            bool hadPrevious = column.TryGetValue(y, out int previous);
            if (hadPrevious && previous == terrain) return;
            if (hadPrevious && previous >= 0)
            {
                if (--_placedHeights[y] == 0) _placedHeights.Remove(y);
            }
            column[y] = terrain;
            _editedColumns.Remove(x);
            if (terrain >= 0) _placedHeights[y] = _placedHeights.GetValueOrDefault(y) + 1;
            _highestEdit = int.MaxValue;
            foreach (int height in _placedHeights.Keys) { _highestEdit = height; break; }
            Revision++;
            _changes.Enqueue((Revision, new(x, y)));
            if (_changes.Count > 256) _changes.Dequeue();
            Field.GeometryChanged(x, y);
        }

        public void ApplyMutation(int x, int y, string type, string blockId)
        {
            if (type == "break") SetTerrain(x, y, -1);
            else if (type == "place" && BlockDB.TryGet(blockId, out var block)) SetTerrain(x, y, block.TerrainSet ?? block.SourceId);
        }

        /// <summary>Transmission towards the sun. Zero below the horizon; no arbitrary distance cutoff.</summary>
        public double Sun(double px, double py, double dx, double dy)
        {
            if (dy >= -0.001) return 0;
            int top = Math.Min(SkyTop, _highestEdit);
            if (py < top) return 1;
            double end = (top - py - 1) / dy;
            double t = 0, transmission = 1;
            int x = (int)Math.Floor(px);
            int step = dx >= 0 ? 1 : -1;
            double delta = Math.Abs(dx) < 1e-9 ? double.PositiveInfinity : Math.Abs(1 / dx);
            double next = Math.Abs(dx) < 1e-9 ? double.PositiveInfinity : ((dx > 0 ? x + 1 : x) - px) / dx;
            while (t < end)
            {
                double stop = Math.Min(next, end);
                double highY = py + dy * t, lowY = py + dy * stop;
                foreach (var run in Column(x))
                {
                    if (run.Start >= highY) break;
                    if (run.End <= lowY) continue;
                    double length = (Math.Min(highY, run.End) - Math.Max(lowY, run.Start)) / -dy;
                    if (length <= 1e-7) continue;
                    byte opacity = TerrainOpacity(run.Terrain);
                    if (opacity == 255) return 0;
                    transmission *= Math.Pow(1 - opacity / 255.0, length);
                    if (transmission < 1.0 / 255) return 0;
                }
                t = stop;
                next += delta;
                x += step;
            }
            return transmission;
        }
    }
}
