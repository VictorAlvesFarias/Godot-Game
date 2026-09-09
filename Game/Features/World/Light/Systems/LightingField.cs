using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Jogo25D.Light
{
    public readonly record struct LightCell(int X, int Y)
    {
        public static LightCell operator +(LightCell a, LightCell b) => new(a.X + b.X, a.Y + b.Y);
    }

    public readonly record struct LightValue(byte Sky, byte R, byte G, byte B)
    {
        public LightValue Max(LightValue b) => new(Math.Max(Sky, b.Sky), Math.Max(R, b.R), Math.Max(G, b.G), Math.Max(B, b.B));
        public LightValue Attenuate(int loss)
        {
            int peak = Math.Max(R, Math.Max(G, B));
            int remaining = Math.Max(0, peak - loss);
            return new((byte)Math.Max(0, Sky - loss),
                peak == 0 ? (byte)0 : (byte)(R * remaining / peak),
                peak == 0 ? (byte)0 : (byte)(G * remaining / peak),
                peak == 0 ? (byte)0 : (byte)(B * remaining / peak));
        }
    }

    public interface ILightGeometry
    {
        // 0 = air, 255 = opaque. Positive extinction represents translucent matter.
        byte Opacity(int x, int y);
        byte Sky(int x, int y);
    }

    /// <summary>
    /// Engine-independent, finite-support lighting. Unknown boundary values are zero.
    /// Four-neighbour relaxation handles increases AND decreases, including alternate paths.
    /// Strictly positive loss makes the fixed point unique: stale light cannot sustain a cycle.
    /// Only changed values enqueue neighbours; no connected-room classification is performed.
    /// </summary>
    public sealed class LightingField
    {
        public const int ChunkSize = 32;
        public const int AirLoss = 12;
        public const int Support = 255 / AirLoss + 1;
        public static readonly LightCell[] Neighbours = { new(-1, 0), new(1, 0), new(0, -1), new(0, 1) };
        private sealed class Chunk
        {
            public readonly byte[] Opacity = new byte[1024];
            public readonly byte[] Sky = new byte[1024];
            public readonly LightValue[] Values = new LightValue[1024];
            public readonly bool[] Queued = new bool[1024];
        }

        private readonly ILightGeometry _geometry;
        private readonly Dictionary<LightCell, Chunk> _chunks = new();
        private readonly Dictionary<long, (LightCell Cell, LightValue Value)> _sources = new();
        private readonly Dictionary<LightCell, LightValue> _emission = new();
        private readonly Queue<LightCell> _queue = new();
        private int _left, _top, _right, _bottom;
        public long Revision { get; private set; }
        public long ProcessedCells { get; private set; }
        public int PendingCells => _queue.Count;
        public int ResidentChunks => _chunks.Count;
        public bool Settled => _queue.Count == 0;

        public LightingField(ILightGeometry geometry) => _geometry = geometry;
        public static int FloorChunk(int value) => (int)Math.Floor(value / 32.0);
        private static LightCell Key(LightCell p) => new(FloorChunk(p.X), FloorChunk(p.Y));
        private static int Index(LightCell p) => (p.Y & 31) * 32 + (p.X & 31);

        // Request a presentation rectangle. Halo exceeds the maximum possible influence path.
        // Evicted fields are derived caches; procedural geometry and mutations remain authoritative.
        public void SetRegion(int x, int y, int width, int height)
        {
            _left = x; _top = y; _right = x + width; _bottom = y + height;
            int left = FloorChunk(x - Support), top = FloorChunk(y - Support);
            int right = FloorChunk(x + width - 1 + Support), bottom = FloorChunk(y + height - 1 + Support);
            var removed = new List<LightCell>();
            foreach (var key in _chunks.Keys)
                if (key.X < left || key.X > right || key.Y < top || key.Y > bottom) removed.Add(key);
            foreach (var key in removed) _chunks.Remove(key);
            if (removed.Count > 0)
            {
                // Rebuild the work queue, not the field, to discard entries belonging to evicted chunks.
                _queue.Clear();
                foreach (var pair in _chunks)
                    for (int i = 0; i < 1024; i++)
                        if (pair.Value.Queued[i]) _queue.Enqueue(new(pair.Key.X * 32 + i % 32, pair.Key.Y * 32 + i / 32));
                foreach (var key in removed) EnqueueEdges(key);
            }
            for (int cy = top; cy <= bottom; cy++)
                for (int cx = left; cx <= right; cx++)
                {
                    var key = new LightCell(cx, cy);
                    if (_chunks.ContainsKey(key)) continue;
                    var chunk = new Chunk();
                    _chunks.Add(key, chunk);
                    for (int i = 0; i < 1024; i++)
                    {
                        var p = new LightCell(cx * 32 + i % 32, cy * 32 + i / 32);
                        chunk.Opacity[i] = _geometry.Opacity(p.X, p.Y);
                        chunk.Sky[i] = _geometry.Sky(p.X, p.Y);
                        Enqueue(p);
                    }
                    EnqueueEdges(key);
                    Revision++;
                }
        }

        private void EnqueueEdges(LightCell key)
        {
            for (int i = 0; i < 32; i++)
            {
                Enqueue(new(key.X * 32 - 1, key.Y * 32 + i));
                Enqueue(new(key.X * 32 + 32, key.Y * 32 + i));
                Enqueue(new(key.X * 32 + i, key.Y * 32 - 1));
                Enqueue(new(key.X * 32 + i, key.Y * 32 + 32));
            }
        }

        public LightValue Get(int x, int y)
        {
            var p = new LightCell(x, y);
            return _chunks.TryGetValue(Key(p), out var chunk) ? chunk.Values[Index(p)] : default;
        }

        /// <summary>Gameplay queries must distinguish unavailable/pending state from darkness.
        /// Only the requested region, with its complete dependency halo, is authoritative.</summary>
        public bool TryGetSettled(int x, int y, out LightValue value)
        {
            value = default;
            if (!Settled || x < _left || y < _top || x >= _right || y >= _bottom) return false;
            value = Get(x, y);
            return true;
        }

        public void GeometryChanged(int x, int y)
        {
            // The vertical sky oracle can change below the edit, even across unloaded chunks.
            // Only this column is inspected; propagation then follows actual value changes.
            foreach (var pair in _chunks)
            {
                if (pair.Key.X != FloorChunk(x)) continue;
                for (int row = 0; row < 32; row++)
                {
                    var p = new LightCell(x, pair.Key.Y * 32 + row);
                    int i = Index(p);
                    byte opacity = _geometry.Opacity(p.X, p.Y), sky = _geometry.Sky(p.X, p.Y);
                    if (opacity == pair.Value.Opacity[i] && sky == pair.Value.Sky[i]) continue;
                    pair.Value.Opacity[i] = opacity;
                    pair.Value.Sky[i] = sky;
                    Enqueue(p);
                }
            }
            Revision++;
        }

        public void SetSource(long id, LightCell cell, LightValue value)
        {
            if (_sources.TryGetValue(id, out var old) && old == (cell, value)) return;
            bool hadOld = _sources.Remove(id, out old);
            if (value != default) _sources[id] = (cell, value with { Sky = 0 });
            if (hadOld) RebuildEmission(old.Cell);
            RebuildEmission(cell);
        }

        public void RemoveSource(long id)
        {
            if (_sources.Remove(id, out var old)) RebuildEmission(old.Cell);
        }

        private void RebuildEmission(LightCell cell)
        {
            LightValue value = default;
            foreach (var source in _sources.Values) if (source.Cell == cell) value = value.Max(source.Value);
            if (value == default) _emission.Remove(cell); else _emission[cell] = value;
            Enqueue(cell);
        }

        private void Enqueue(LightCell p)
        {
            if (!_chunks.TryGetValue(Key(p), out var chunk)) return;
            int i = Index(p);
            if (chunk.Queued[i]) return;
            chunk.Queued[i] = true;
            _queue.Enqueue(p);
        }

        public int Process(int maxCells = 12000, double milliseconds = 2.0)
        {
            long start = Stopwatch.GetTimestamp();
            int processed = 0;
            while (_queue.Count > 0 && processed < maxCells)
            {
                if ((processed & 255) == 0 && Stopwatch.GetElapsedTime(start).TotalMilliseconds >= milliseconds) break;
                var p = _queue.Dequeue();
                if (!_chunks.TryGetValue(Key(p), out var chunk)) continue;
                int i = Index(p);
                chunk.Queued[i] = false;
                processed++;
                LightValue next = default;
                if (chunk.Opacity[i] < 255)
                {
                    next = new(chunk.Sky[i], 0, 0, 0);
                    if (_emission.TryGetValue(p, out var emission)) next = next.Max(emission);
                    int loss = AirLoss + chunk.Opacity[i] / 3;
                    foreach (var d in Neighbours) next = next.Max(Get(p.X + d.X, p.Y + d.Y).Attenuate(loss));
                }
                if (chunk.Values[i] == next) continue;
                chunk.Values[i] = next;
                Revision++;
                foreach (var d in Neighbours) Enqueue(p + d);
            }
            ProcessedCells += processed;
            return processed;
        }
    }
}
