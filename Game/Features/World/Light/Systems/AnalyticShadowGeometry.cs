using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Jogo25D.Light
{
    // Compressed logical material rectangles, projected and indexed into 16-tile bins.
    // No angular rays or sampled boundary atlas. Remote geometry uses the same path.
    public sealed class AnalyticShadowGeometry
    {
        public const int BinSize = 16, TextureWidth = 1024;
        private readonly record struct Rect(double Left, double Top, double Right, double Bottom, float Opacity);
        private readonly List<Rect> _rects = new();
        private readonly Dictionary<(int Top, int Bottom, int Terrain), int> _active = new();
        private LogicalLightWorld _world;
        private Vector2I _origin, _size;
        private int _column, _lastColumn, _rectCursor, _binCursor;
        private double _leftSlope, _rightSlope;
        private List<int>[] _bins;
        private List<float> _packed;
        private byte[] _bytes;
        public bool Complete { get; private set; }

        public void Begin(LogicalLightWorld world, Vector2I origin, Vector2I size, float angle, float penumbra)
        {
            _world = world; _origin = origin; _size = size;
            Complete = false; _rects.Clear(); _active.Clear(); _rectCursor = 0; _binCursor = 0;
            double spread = 8 * Math.Pow(1 - Math.Clamp(penumbra, 0, 1), 2);
            double a = Math.Clamp(angle - spread, -89.5, 89.5) * Math.PI / 180;
            double b = Math.Clamp(angle + spread, -89.5, 89.5) * Math.PI / 180;
            _leftSlope = -Math.Tan(b); _rightSlope = -Math.Tan(a);
            double reach = Math.Max(0, (double)origin.Y + size.Y - world.OpticalSkyTop);
            _column = (int)Math.Max(int.MinValue + 1, Math.Floor(origin.X + Math.Min(0, Math.Tan(a) * reach)) - 1);
            _lastColumn = (int)Math.Min(int.MaxValue - 1, Math.Ceiling((double)origin.X + size.X + Math.Max(0, Math.Tan(b) * reach)) + 1);
            if (Math.Cos(angle * Math.PI / 180) <= 0.001) _lastColumn = _column - 1;
            _bins = new List<int>[((size.X + 15) / 16) * ((size.Y + 15) / 16)];
            for (int i = 0; i < _bins.Length; i++) _bins[i] = new();
            _packed = new List<float>(new float[_bins.Length * 4]);
        }

        public void Process(double milliseconds)
        {
            if (Complete) return;
            long start = Stopwatch.GetTimestamp();
            while (_column <= _lastColumn)
            {
                if (Stopwatch.GetElapsedTime(start).TotalMilliseconds >= milliseconds) return;
                // Merge equal vertical runs across adjacent columns into rectangles.
                foreach (var run in _world.OpticalColumn(_column))
                {
                    int bottom = (int)Math.Min((long)run.End, (long)_origin.Y + _size.Y);
                    if (run.Start >= bottom) continue;
                    var key = (run.Start, bottom, run.Terrain);
                    if (_active.TryGetValue(key, out int index) && _rects[index].Right == _column)
                        _rects[index] = _rects[index] with { Right = (double)_column + 1 };
                    else
                    {
                        _active[key] = _rects.Count;
                        _rects.Add(new(_column, run.Start, (double)_column + 1, bottom, LogicalLightWorld.TerrainOpacity(run.Terrain) / 255f));
                    }
                }
                _column++;
            }
            int columns = (_size.X + 15) / 16;
            while (_rectCursor < _rects.Count)
            {
                if (Stopwatch.GetElapsedTime(start).TotalMilliseconds >= milliseconds) return;
                var r = _rects[_rectCursor];
                double reach = (double)_origin.Y + _size.Y - r.Top;
                double left = r.Left + Math.Min(0, _leftSlope * reach) - _origin.X - 1;
                double right = r.Right + Math.Max(0, _rightSlope * reach) - _origin.X + 1;
                int x0 = Math.Max(0, (int)Math.Floor(left / 16)), x1 = Math.Min(columns - 1, (int)Math.Floor(right / 16));
                int y0 = Math.Max(0, (int)Math.Floor((r.Top - _origin.Y - 1) / 16));
                for (int y = y0; y < (_size.Y + 15) / 16; y++)
                    for (int x = x0; x <= x1; x++) _bins[y * columns + x].Add(_rectCursor);
                _rectCursor++;
            }
            while (_binCursor < _bins.Length)
            {
                if (Stopwatch.GetElapsedTime(start).TotalMilliseconds >= milliseconds) return;
                _packed[_binCursor * 4] = _packed.Count / 4;
                _packed[_binCursor * 4 + 1] = _bins[_binCursor].Count;
                foreach (int index in _bins[_binCursor])
                {
                    var r = _rects[index];
                    _packed.Add((float)(r.Left - _origin.X)); _packed.Add((float)(r.Top - _origin.Y));
                    _packed.Add((float)(r.Right - _origin.X)); _packed.Add((float)(r.Bottom - _origin.Y));
                    _packed.Add(r.Opacity); _packed.Add(0); _packed.Add(0); _packed.Add(0);
                }
                _binCursor++;
            }
            int height = Math.Max(1, (_packed.Count / 4 + TextureWidth - 1) / TextureWidth);
            _bytes = new byte[TextureWidth * height * 16];
            Buffer.BlockCopy(_packed.ToArray(), 0, _bytes, 0, _packed.Count * 4);
            Complete = true;
        }

        public Image Image() => Godot.Image.CreateFromData(TextureWidth, _bytes.Length / (TextureWidth * 16), false, Godot.Image.Format.Rgbaf, _bytes);
    }
}
