using Godot;
using Jogo25D.Dimensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Jogo25D.Light
{
    public sealed class AnalyticShadowGeometry
    {
        #region Dinamic properties

        public const int BinSize = 16;
        public const int TextureWidth = 1024;

        private const double MaxSpreadDegrees = 8;
        private const double MaxSlopeDegrees = 89.5;
        private const int SummaryTerrain = int.MaxValue;

        public bool Complete { get; private set; }

        private readonly List<Rect> _rects = new();
        private readonly Dictionary<(int Top, int Bottom, int Terrain), int> _active = new();

        private TerrainCache _world;
        private Vector2I _origin;
        private Vector2I _size;
        private int _column;
        private int _lastColumn;
        private int _nearLeft;
        private int _nearRight;
        private int _rectCursor;
        private int _binCursor;
        private double _leftSlope;
        private double _rightSlope;
        private List<int>[] _bins;
        private List<float> _packed;
        private byte[] _bytes;

        private int BinColumns => (_size.X + BinSize - 1) / BinSize;
        private int BinRows => (_size.Y + BinSize - 1) / BinSize;

        #endregion

        #region Core - Preparacao

        public void Begin(TerrainCache world, Vector2I origin, Vector2I size, float angle, float penumbra)
        {
            _world = world;
            _origin = origin;
            _size = size;
            _rectCursor = 0;
            _binCursor = 0;

            Complete = false;

            _rects.Clear();
            _active.Clear();

            var spread = MaxSpreadDegrees * Math.Pow(1 - Math.Clamp(penumbra, 0, 1), 2);
            var left = Math.Clamp(angle - spread, -MaxSlopeDegrees, MaxSlopeDegrees) * Math.PI / 180;
            var right = Math.Clamp(angle + spread, -MaxSlopeDegrees, MaxSlopeDegrees) * Math.PI / 180;

            _leftSlope = -Math.Tan(right);
            _rightSlope = -Math.Tan(left);

            var reach = Math.Max(0, (double)origin.Y + size.Y - world.OpticalSkyTop);

            _column = (int)Math.Max(int.MinValue + 1, Math.Floor(origin.X + Math.Min(0, Math.Tan(left) * reach)) - 1);
            _lastColumn = (int)Math.Min(int.MaxValue - 1, Math.Ceiling((double)origin.X + size.X + Math.Max(0, Math.Tan(right) * reach)) + 1);

            if (Math.Cos(angle * Math.PI / 180) <= 0.001)
            {
                _lastColumn = _column - 1;
            }

            // Colunas cujos runs dentro da janela ainda podem sombrear algum pixel dela.
            _nearLeft = (int)Math.Max(int.MinValue + 2, Math.Floor(origin.X - Math.Max(0, _rightSlope) * size.Y) - 2);
            _nearRight = (int)Math.Min(int.MaxValue - 2, Math.Ceiling(origin.X + size.X - Math.Min(0, _leftSlope) * size.Y) + 2);

            _bins = new List<int>[BinColumns * BinRows];

            for (var i = 0; i < _bins.Length; i++)
            {
                _bins[i] = new List<int>();
            }

            _packed = new List<float>(new float[_bins.Length * 4]);
        }

        #endregion

        #region Core - Construcao

        public void Process(double milliseconds)
        {
            if (Complete)
            {
                return;
            }

            var start = Stopwatch.GetTimestamp();

            if (!CollectRects(start, milliseconds) || !BinRects(start, milliseconds) || !PackBins(start, milliseconds))
            {
                return;
            }

            var height = Math.Max(1, (_packed.Count / 4 + TextureWidth - 1) / TextureWidth);

            _bytes = new byte[TextureWidth * height * 16];

            Buffer.BlockCopy(_packed.ToArray(), 0, _bytes, 0, _packed.Count * 4);

            Complete = true;
        }

        public Image ToImage()
        {
            return Image.CreateFromData(TextureWidth, _bytes.Length / (TextureWidth * 16), false, Image.Format.Rgbaf, _bytes);
        }

        private bool CollectRects(long start, double milliseconds)
        {
            while (_column <= _lastColumn)
            {
                if (Expired(start, milliseconds))
                {
                    return false;
                }

                var top = _origin.Y;
                var bottom = _origin.Y + _size.Y;
                var near = _column >= _nearLeft && _column <= _nearRight;
                var firstSolid = _world.FirstSolid(_column, near ? bottom : top);

                // Acima da janela, tudo abaixo do primeiro solido vira um unico retangulo opaco:
                // o sol so entra no subsolo por aberturas na propria coluna.
                if (firstSolid < top)
                {
                    AddRect(firstSolid, top, SummaryTerrain);
                }

                if (near)
                {
                    foreach (var run in _world.OpticalColumn(_column, Math.Max(top, firstSolid), bottom))
                    {
                        AddRect(run.Start, run.End, run.Terrain);
                    }
                }

                _column++;
            }

            return true;
        }

        private void AddRect(int start, int bottom, int terrain)
        {
            var key = (start, bottom, terrain);

            if (_active.TryGetValue(key, out var index) && _rects[index].Right == _column)
            {
                _rects[index] = _rects[index] with
                {
                    Right = (double)_column + 1
                };
            }
            else
            {
                _active[key] = _rects.Count;

                _rects.Add(new Rect(
                    _column,
                    start,
                    (double)_column + 1,
                    bottom,
                    TerrainCache.TerrainOpacity(terrain) / 255f));
            }
        }

        private bool BinRects(long start, double milliseconds)
        {
            while (_rectCursor < _rects.Count)
            {
                if (Expired(start, milliseconds))
                {
                    return false;
                }

                var rect = _rects[_rectCursor];
                var reach = (double)_origin.Y + _size.Y - rect.Top;
                var left = rect.Left + Math.Min(0, _leftSlope * reach) - _origin.X - 1;
                var right = rect.Right + Math.Max(0, _rightSlope * reach) - _origin.X + 1;
                var x0 = Math.Max(0, (int)Math.Floor(left / BinSize));
                var x1 = Math.Min(BinColumns - 1, (int)Math.Floor(right / BinSize));
                var y0 = Math.Max(0, (int)Math.Floor((rect.Top - _origin.Y - 1) / BinSize));

                for (var y = y0; y < BinRows; y++)
                {
                    for (var x = x0; x <= x1; x++)
                    {
                        _bins[y * BinColumns + x].Add(_rectCursor);
                    }
                }

                _rectCursor++;
            }

            return true;
        }

        private bool PackBins(long start, double milliseconds)
        {
            while (_binCursor < _bins.Length)
            {
                if (Expired(start, milliseconds))
                {
                    return false;
                }

                _packed[_binCursor * 4] = _packed.Count / 4;
                _packed[_binCursor * 4 + 1] = _bins[_binCursor].Count;

                foreach (var index in _bins[_binCursor])
                {
                    var rect = _rects[index];

                    _packed.Add((float)(rect.Left - _origin.X));
                    _packed.Add((float)(rect.Top - _origin.Y));
                    _packed.Add((float)(rect.Right - _origin.X));
                    _packed.Add((float)(rect.Bottom - _origin.Y));
                    _packed.Add(rect.Opacity);
                    _packed.Add(0);
                    _packed.Add(0);
                    _packed.Add(0);
                }

                _binCursor++;
            }

            return true;
        }

        #endregion

        #region Utils

        private static bool Expired(long start, double milliseconds)
        {
            return Stopwatch.GetElapsedTime(start).TotalMilliseconds >= milliseconds;
        }

        #endregion

        #region Types

        private readonly record struct Rect(double Left, double Top, double Right, double Bottom, float Opacity);

        #endregion
    }
}
