using Godot;
using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    // Builds presentation textures from the logical field. No TileMap reads and no camera rays.
    public sealed class LightMapComputer
    {
        public const int Subdivisions = 2;
        public const int ShadowSubdivisions = 6;
        public const int SunSamples = 9;
        private LogicalLightWorld _world;
        private byte[] _light = Array.Empty<byte>(), _emission = Array.Empty<byte>();
        private int _cursor;
        private byte[] _boundary = Array.Empty<byte>();
        private int _boundaryCursor;
        private int BoundaryWidth => Math.Max(Size.X, Size.Y) * Subdivisions + 1;
        private bool _reuseGeometry;
        private bool _reuseReceivers;
        private readonly HashSet<LightCell> _dirtyReceivers = new();
        public bool RebuiltSun => !_reuseGeometry;
        private int[] _receiverX = Array.Empty<int>(), _receiverY = Array.Empty<int>();
        private double[] _depth = Array.Empty<double>();
        private readonly double[] _dx = new double[SunSamples], _dy = new double[SunSamples];
        public Vector2I Origin { get; private set; }
        public Vector2I Size { get; private set; }
        public bool Complete => _cursor >= Size.X * Size.Y * Subdivisions * Subdivisions && _boundaryCursor >= _boundary.Length;
        public long WorldRevision { get; private set; }
        public long FieldRevision { get; private set; }
        public float Angle { get; private set; }
        public float Penumbra { get; private set; }

        public void Begin(LogicalLightWorld world, Vector2I origin, Vector2I size, float angle, float penumbra)
        {
            bool layout = Complete && _world == world && Origin == origin && Size == size;
            _reuseGeometry = layout
                && WorldRevision == world.Revision && Angle == angle && Penumbra == penumbra;
            bool reuseBoundary = layout && Angle == angle && Penumbra == penumbra;
            _dirtyReceivers.Clear();
            _reuseReceivers = layout && world.TryGetChanges(WorldRevision, out _);
            if (_reuseReceivers)
            {
                world.TryGetChanges(WorldRevision, out var changes);
                foreach (var cell in changes)
                {
                    if (cell.X < origin.X || cell.Y < origin.Y || cell.X >= origin.X + size.X || cell.Y >= origin.Y + size.Y) reuseBoundary = false;
                    for (int y = -3; y <= 3; y++) for (int x = -3; x <= 3; x++)
                        _dirtyReceivers.Add(new(cell.X + x, cell.Y + y));
                }
            }
            else reuseBoundary = false;
            _world = world; Origin = origin; Size = size; Angle = angle; Penumbra = penumbra;
            WorldRevision = world.Revision; FieldRevision = world.Field.Revision;
            int bytes = size.X * size.Y * Subdivisions * Subdivisions * 4;
            if (_light.Length != bytes)
            {
                _light = new byte[bytes]; _emission = new byte[bytes];
                _receiverX = new int[bytes / 4]; _receiverY = new int[bytes / 4]; _depth = new double[bytes / 4];
            }
            _cursor = 0;
            int boundaryBytes = BoundaryWidth * (SunSamples * 4);
            if (_boundary.Length != boundaryBytes) _boundary = new byte[boundaryBytes];
            _boundaryCursor = reuseBoundary ? boundaryBytes : 0;
            double spread = Math.Atan(Math.Tan(5 * Math.PI / 180) * (1 - penumbra));
            for (int i = 0; i < SunSamples; i++)
            {
                double a = angle * Math.PI / 180 + (i - (SunSamples - 1) / 2) * spread / ((SunSamples - 1) / 2);
                _dx[i] = Math.Sin(a); _dy[i] = -Math.Cos(a);
            }
        }

        public void Process(double milliseconds = 3)
        {
            long start = Stopwatch.GetTimestamp();
            int width = Size.X * Subdivisions;
            while (_cursor < Size.X * Size.Y * Subdivisions * Subdivisions)
            {
                if ((_cursor & 63) == 0 && Stopwatch.GetElapsedTime(start).TotalMilliseconds >= milliseconds) return;
                int sx = _cursor % width, sy = _cursor / width;
                double px = Origin.X + (sx + 0.5) / Subdivisions, py = Origin.Y + (sy + 0.5) / Subdivisions;
                int x = (int)Math.Floor(px), y = (int)Math.Floor(py);
                if (_reuseReceivers && !_dirtyReceivers.Contains(new(x, y)))
                {
                    WriteField(_world.Field.Get(_receiverX[_cursor], _receiverY[_cursor]), _depth[_cursor]);
                    _cursor++;
                    continue;
                }
                byte opacity = _world.Opacity(x, y);
                LightValue value = _world.Field.Get(x, y);
                double depth = 1;
                _receiverX[_cursor] = x; _receiverY[_cursor] = y;
                if (opacity == 255)
                {
                    // Surface reception is separate from transmission. Light on a rock face
                    // does not become a light source on the other side of the rock.
                    double nearest = double.PositiveInfinity;
                    value = default;
                    for (int distance = 1; distance <= 3; distance++)
                        foreach (var d in LightingField.Neighbours)
                        {
                            int nx = x + d.X * distance, ny = y + d.Y * distance;
                            if (_world.Opacity(nx, ny) == 255) continue;
                            double qx = d.X == 0 ? px : d.X > 0 ? nx + 0.001 : nx + 0.999;
                            double qy = d.Y == 0 ? py : d.Y > 0 ? ny + 0.001 : ny + 0.999;
                            double metric = Math.Abs(qx - px) + Math.Abs(qy - py);
                            if (metric >= nearest) continue;
                            nearest = metric;
                            value = _world.Field.Get(nx, ny);
                            _receiverX[_cursor] = nx; _receiverY[_cursor] = ny;
                            depth = Math.Pow(0.43, distance - 1);
                        }
                    if (double.IsPositiveInfinity(nearest)) depth = 0;
                }
                int p = _cursor * 4;
                _depth[_cursor] = depth;
                _light[p + 1] = (byte)Math.Round(depth * 255);
                _light[p + 2] = opacity;
                WriteField(value, depth);
                _cursor++;
            }
            while (_boundaryCursor < _boundary.Length)
            {
                if ((_boundaryCursor & 15) == 0 && Stopwatch.GetElapsedTime(start).TotalMilliseconds >= milliseconds) return;
                int row = _boundaryCursor / BoundaryWidth, sample = _boundaryCursor % BoundaryWidth;
                int direction = row / 4, edge = row % 4;
                double offset = sample / (double)Subdivisions;
                double x = edge == 0 || edge == 1 ? Math.Min(offset, Size.X) : edge == 2 ? 0 : Size.X;
                double y = edge == 2 || edge == 3 ? Math.Min(offset, Size.Y) : edge == 0 ? 0 : Size.Y;
                bool outgoing = edge == 0 ? _dy[direction] < 0 : edge == 1 ? _dy[direction] > 0 : edge == 2 ? _dx[direction] < 0 : _dx[direction] > 0;
                double value = outgoing ? _world.Sun(Origin.X + x + _dx[direction] * 0.0001,
                    Origin.Y + y + _dy[direction] * 0.0001, _dx[direction], _dy[direction]) : 0;
                _boundary[_boundaryCursor++] = (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
            }
        }

        private void WriteField(LightValue value, double depth)
        {
            int p = _cursor * 4;
            _light[p] = (byte)(value.Sky * depth);
            _light[p + 3] = 255;
            _emission[p] = (byte)(value.R * depth);
            _emission[p + 1] = (byte)(value.G * depth);
            _emission[p + 2] = (byte)(value.B * depth);
            _emission[p + 3] = 255;
        }

        public Image BoundaryImage() => Image.CreateFromData(BoundaryWidth, SunSamples * 4, false, Image.Format.R8, _boundary);
        public Image LightImage() => Image.CreateFromData(Size.X * Subdivisions, Size.Y * Subdivisions, false, Image.Format.Rgba8, _light);
        public Image EmissionImage() => Image.CreateFromData(Size.X * Subdivisions, Size.Y * Subdivisions, false, Image.Format.Rgba8, _emission);
    }
}
