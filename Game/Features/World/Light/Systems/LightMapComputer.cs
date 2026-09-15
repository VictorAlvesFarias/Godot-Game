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
        public const int ShadowSubdivisions = 4;
        private LogicalLightWorld _world;
        private byte[] _light = Array.Empty<byte>(), _emission = Array.Empty<byte>();
        private int _cursor;
        private readonly AnalyticShadowGeometry _geometry = new();
        private bool _reuseGeometry;
        private bool _skipShadow;
        private bool _reuseReceivers;
        private readonly HashSet<LightCell> _dirtyReceivers = new();
        public bool RebuiltSun => !_reuseGeometry;
        private int[] _receiverX = Array.Empty<int>(), _receiverY = Array.Empty<int>();
        private double[] _depth = Array.Empty<double>();
        // Quanto a luz ja caminhou dentro do solido ate esta celula, por face. Serve para espalhar:
        // quanto mais fundo, mais larga a janela de onde ela vem.
        private double[] _travel = Array.Empty<double>();
        // Valor cru por subpixel e a distancia que a luz percorreu dentro do solido ate ele. A
        // imagem final borra o cru na horizontal com raio vindo dessa distancia: colunas vizinhas
        // tem superficie em degrau, percorrem distancias diferentes e recebem atenuacoes
        // diferentes - e essa costura entre colunas que aparece como listra vertical na terra.
        private byte[] _rawSky = Array.Empty<byte>();
        private double[] _travelUsed = Array.Empty<double>();
        public Vector2I Origin { get; private set; }
        public Vector2I Size { get; private set; }
        public bool Complete => _cursor >= Size.X * Size.Y * Subdivisions * Subdivisions && (_skipShadow || _geometry.Complete);
        public long WorldRevision { get; private set; }
        public long FieldRevision { get; private set; }
        public long BackgroundRevision { get; private set; }
        public float Angle { get; private set; }
        public float Penumbra { get; private set; }
        public float TerrainTransition { get; private set; } = 3;
        public float ShadowSoftness { get; private set; } = 1;
        public float AmbientSoftness { get; private set; } = 1;
        public bool IsWorld(LogicalLightWorld world) => _world == world;

        public void Begin(LogicalLightWorld world, Vector2I origin, Vector2I size, float angle, float penumbra,
            float terrainTransition = 3, float shadowSoftness = 1, float ambientSoftness = 1, bool buildShadow = true)
        {
            bool previouslySkipped = _skipShadow;
            bool layout = Complete && _world == world && Origin == origin && Size == size;
            _skipShadow = !buildShadow;
            terrainTransition = Math.Clamp(terrainTransition, 0.25f, 16);
            bool reuseGeometryData = !previouslySkipped && layout
                && WorldRevision == world.Revision && Angle == angle && Penumbra == penumbra;
            _reuseGeometry = reuseGeometryData && TerrainTransition == terrainTransition
                && ShadowSoftness == shadowSoftness && AmbientSoftness == ambientSoftness;
            _dirtyReceivers.Clear();
            _reuseReceivers = layout && TerrainTransition == terrainTransition && world.TryGetChanges(WorldRevision, out _);
            if (_reuseReceivers)
            {
                world.TryGetChanges(WorldRevision, out var changes);
                foreach (var cell in changes)
                {
                    int radius = (int)Math.Ceiling(terrainTransition);
                    for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
                        _dirtyReceivers.Add(new(cell.X + x, cell.Y + y));
                }
            }
            _world = world; Origin = origin; Size = size; Angle = angle; Penumbra = penumbra;
            TerrainTransition = terrainTransition;
            ShadowSoftness = shadowSoftness; AmbientSoftness = ambientSoftness;
            WorldRevision = world.Revision; FieldRevision = world.Field.Revision; BackgroundRevision = world.BackgroundRevision;
            int bytes = size.X * size.Y * Subdivisions * Subdivisions * 4;
            if (_light.Length != bytes)
            {
                _light = new byte[bytes]; _emission = new byte[bytes];
                _receiverX = new int[bytes]; _receiverY = new int[bytes]; _depth = new double[bytes];
                _travel = new double[bytes];
                _rawSky = new byte[bytes]; _travelUsed = new double[bytes];
            }
            _cursor = 0;
            if (!_skipShadow && !reuseGeometryData) _geometry.Begin(world, origin, size, angle, penumbra);
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
                    WriteReceivers();
                    _cursor++;
                    continue;
                }
                byte opacity = _world.Opacity(x, y);
                int slot = _cursor * 4;
                for (int side = 0; side < 4; side++) { _depth[slot + side] = 0; _travel[slot + side] = 0; }
                _receiverX[slot] = x; _receiverY[slot] = y; _depth[slot] = 1;
                if (opacity == 255)
                {
                    // Surface reception is separate from transmission. Light on a rock face
                    // does not become a light source on the other side of the rock.
                    _depth[slot] = 0;
                    for (int side = 0; side < 4; side++)
                        for (int distance = 1; distance <= (int)Math.Ceiling(TerrainTransition); distance++)
                        {
                            var d = LightingField.Neighbours[side];
                            int nx = x + d.X * distance, ny = y + d.Y * distance;
                            if (_world.Opacity(nx, ny) == 255) continue;
                            double qx = d.X == 0 ? px : d.X > 0 ? nx + 0.001 : nx + 0.999;
                            double qy = d.Y == 0 ? py : d.Y > 0 ? ny + 0.001 : ny + 0.999;
                            double metric = Math.Abs(qx - px) + Math.Abs(qy - py);
                            _receiverX[slot + side] = nx; _receiverY[slot + side] = ny;
                            // Continuous surface falloff. Keep all exposed faces so switching
                            // the nearest face cannot cut a dark diagonal into a solid corner.
                            double t = Math.Clamp(metric / TerrainTransition, 0, 1);
                            _depth[slot + side] = 1 - t * t * (3 - 2 * t);
                            _travel[slot + side] = metric;
                            break;
                        }
                }
                int p = _cursor * 4;
                _light[p + 1] = 255;
                _light[p + 2] = opacity;
                WriteReceivers();
                _cursor++;
            }
            if (!_skipShadow) _geometry.Process(Math.Max(0.01, milliseconds - Stopwatch.GetElapsedTime(start).TotalMilliseconds));
        }

        private void WriteReceivers()
        {
            int p = _cursor * 4;
            LightValue value = default;
            double travelled = 0;
            for (int side = 0; side < 4; side++)
            {
                int slot = p + side;
                double depth = _depth[slot];
                if (depth == 0) continue;
                var source = _world.Field.Get(_receiverX[slot], _receiverY[slot]);
                var candidate = new LightValue((byte)(source.Sky * depth), (byte)(source.R * depth),
                    (byte)(source.G * depth), (byte)(source.B * depth));
                if (candidate.Sky >= value.Sky) travelled = _travel[slot];
                value = value.Max(candidate);
            }
            _rawSky[p] = value.Sky;
            _travelUsed[p] = travelled;
            _light[p] = value.Sky;
            _light[p + 3] = 255;
            _emission[p] = value.R;
            _emission[p + 1] = value.G;
            _emission[p + 2] = value.B;
            _emission[p + 3] = 255;
        }

        public const int BeamMargin = 24;
        public Image DepthBeamImage()
        {
            int width = Size.X + BeamMargin * 2, height = Size.Y + BeamMargin * 2;
            byte[] data = new byte[width * height * 4];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int wx = Origin.X + x - BeamMargin, wy = Origin.Y + y - BeamMargin;
                int i = (y * width + x) * 4;
                bool solid = _world.Opacity(wx,wy) == 255;
                data[i] = (byte)(_world.DepthLightEnabled && !solid && !_world.HasBackground(wx,wy) ? 255 : 0);
                data[i+1] = solid ? (byte)255 : (byte)0;
                data[i+2] = _world.HasBackground(wx,wy) ? (byte)255 : (byte)0;
                data[i+3] = 255;
            }
            var image = Image.CreateFromData(width,height,false,Image.Format.Rgba8,data);
            image.GenerateMipmaps();
            return image;
        }

        public Image ShadowGeometryImage() => _geometry.Image();
        // Borra o ceu na horizontal apenas dentro do solido, com raio proporcional a distancia que
        // a luz percorreu ate chegar ali. Na face o raio e zero, entao a superficie continua nitida;
        // fundo adentro a costura entre colunas se dissolve. Le sempre do cru, entao reprocessar
        // nao acumula borrao.
        private void BlurIntoLight()
        {
            int width = Size.X * Subdivisions, height = Size.Y * Subdivisions;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int p = (y * width + x) * 4;
                    if (_light[p + 2] != 255 || _travelUsed[p] <= 0.5) { _light[p] = _rawSky[p]; continue; }
                    int radius = Math.Min(12, (int)Math.Round(_travelUsed[p]));
                    int sum = 0, taken = 0;
                    for (int offset = -radius; offset <= radius; offset++)
                    {
                        int sx = x + offset;
                        if (sx < 0 || sx >= width) continue;
                        int q = (y * width + sx) * 4;
                        if (_light[q + 2] != 255) continue;   // so mistura terra com terra
                        sum += _rawSky[q]; taken++;
                    }
                    _light[p] = taken > 0 ? (byte)(sum / taken) : _rawSky[p];
                }
        }

        public Image LightImage()
        {
            BlurIntoLight();
            return Image.CreateFromData(Size.X * Subdivisions, Size.Y * Subdivisions, false, Image.Format.Rgba8, _light);
        }
        public Image EmissionImage() => Image.CreateFromData(Size.X * Subdivisions, Size.Y * Subdivisions, false, Image.Format.Rgba8, _emission);
    }
}
