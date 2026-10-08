using Godot;
using Jogo25D.Constants;
using System;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    public sealed class LightPropagationGpu : IDisposable
    {
        #region Constructors

        public LightPropagationGpu(RenderingDevice device = null)
        {
            _device = device ?? RenderingServer.CreateLocalRenderingDevice();
            _ownsDevice = device == null;

            if (_device == null)
            {
                throw new InvalidOperationException("Sem RenderingDevice: compute de luz indisponivel.");
            }

            var file = GD.Load<RDShaderFile>(ShaderPath);
            var spirv = file.GetSpirV();
            var error = spirv.CompileErrorCompute;

            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException(ShaderPath + ": " + error);
            }

            _shader = _device.ShaderCreateFromSpirV(spirv);
            _pipeline = _device.ComputePipelineCreate(_shader);
        }

        #endregion

        #region Dinamic properties

        private const string ShaderPath = "res://Assets/Shaders/light_propagation.glsl";
        private const int GroupSize = 8;
        private const int SeedStride = 16;

        public static readonly int Iterations = Mathf.CeilToInt(1f / LightingConstants.AIR_LIGHT_LOSS);

        private Rid Result => Iterations % 2 == 0 ? _ping : _pong;

        private readonly RenderingDevice _device;
        private readonly bool _ownsDevice;

        private Rid _shader;
        private Rid _pipeline;
        private Rid _terrain;
        private Rid _seed;
        private Rid _ping;
        private Rid _pong;
        private Rid _setPing;
        private Rid _setPong;
        private Vector2I _size;

        #endregion

        #region Core - Compute

        public Color[,] Compute(Rect2I region, Func<Vector2I, bool> isSolid, IReadOnlyList<LightSource> sources, bool readback = true)
        {
            var size = region.Size;

            Allocate(size);

            var solid = new byte[size.X * size.Y];
            var seed = new byte[size.X * size.Y * SeedStride];

            for (var y = 0; y < size.Y; y++)
            {
                for (var x = 0; x < size.X; x++)
                {
                    solid[y * size.X + x] = isSolid(new Vector2I(region.Position.X + x, region.Position.Y + y)) ? (byte)255 : (byte)0;
                }
            }

            var peaks = new Color[size.X, size.Y];

            foreach (var source in sources)
            {
                var local = source.Cell - region.Position;

                if (local.X < 0 || local.Y < 0 || local.X >= size.X || local.Y >= size.Y)
                {
                    continue;
                }

                var current = peaks[local.X, local.Y];

                peaks[local.X, local.Y] = new Color(
                    Mathf.Max(current.R, source.Color.R),
                    Mathf.Max(current.G, source.Color.G),
                    Mathf.Max(current.B, source.Color.B));
            }

            for (var y = 0; y < size.Y; y++)
            {
                for (var x = 0; x < size.X; x++)
                {
                    var slot = (y * size.X + x) * SeedStride;
                    var color = peaks[x, y];

                    WriteFloat(seed, slot, color.R);
                    WriteFloat(seed, slot + 4, color.G);
                    WriteFloat(seed, slot + 8, color.B);
                    WriteFloat(seed, slot + 12, 1f);
                }
            }

            _device.TextureUpdate(_terrain, 0, solid);
            _device.TextureUpdate(_seed, 0, seed);
            _device.TextureUpdate(_ping, 0, seed);
            _device.TextureUpdate(_pong, 0, seed);

            var constants = new byte[32];

            Buffer.BlockCopy(new[] { size.X, size.Y }, 0, constants, 0, 8);
            Buffer.BlockCopy(
                new[] { LightingConstants.AIR_LIGHT_LOSS, LightingConstants.SOLID_FALLOFF, LightingConstants.MIN_LIGHT_THRESHOLD, 0f, 0f, 0f },
                0,
                constants,
                8,
                24);

            var groupsX = (uint)((size.X + GroupSize - 1) / GroupSize);
            var groupsY = (uint)((size.Y + GroupSize - 1) / GroupSize);
            var list = _device.ComputeListBegin();

            _device.ComputeListBindComputePipeline(list, _pipeline);

            for (var i = 0; i < Iterations; i++)
            {
                _device.ComputeListBindUniformSet(list, i % 2 == 0 ? _setPing : _setPong, 0);
                _device.ComputeListSetPushConstant(list, constants, (uint)constants.Length);
                _device.ComputeListDispatch(list, groupsX, groupsY, 1);
                _device.ComputeListAddBarrier(list);
            }

            _device.ComputeListEnd();

            if (_ownsDevice)
            {
                _device.Submit();
                _device.Sync();
            }

            if (!readback)
            {
                return null;
            }

            var data = _device.TextureGetData(Result, 0);
            var grid = new Color[size.X, size.Y];

            for (var y = 0; y < size.Y; y++)
            {
                for (var x = 0; x < size.X; x++)
                {
                    var slot = (y * size.X + x) * SeedStride;

                    grid[x, y] = new Color(BitConverter.ToSingle(data, slot), BitConverter.ToSingle(data, slot + 4), BitConverter.ToSingle(data, slot + 8));
                }
            }

            return grid;
        }

        public Rid ComputeOutput(Rect2I region, Func<Vector2I, bool> solid, IReadOnlyList<LightSource> sources)
        {
            Compute(region, solid, sources, readback: false);

            var output = CreateImage(RenderingDevice.DataFormat.R32G32B32A32Sfloat, region.Size);

            _device.TextureCopy(Result, output, Vector3.Zero, Vector3.Zero, new Vector3(region.Size.X, region.Size.Y, 1), 0, 0, 0, 0);

            return output;
        }

        #endregion

        #region Core - Alocacao

        private void Allocate(Vector2I size)
        {
            if (_size == size && _terrain.IsValid)
            {
                return;
            }

            Release();

            _size = size;
            _terrain = CreateImage(RenderingDevice.DataFormat.R8Unorm, size);
            _seed = CreateImage(RenderingDevice.DataFormat.R32G32B32A32Sfloat, size);
            _ping = CreateImage(RenderingDevice.DataFormat.R32G32B32A32Sfloat, size);
            _pong = CreateImage(RenderingDevice.DataFormat.R32G32B32A32Sfloat, size);
            _setPing = CreateSet(_ping, _pong);
            _setPong = CreateSet(_pong, _ping);
        }

        private Rid CreateImage(RenderingDevice.DataFormat format, Vector2I size)
        {
            var info = new RDTextureFormat
            {
                Format = format,
                Width = (uint)size.X,
                Height = (uint)size.Y,
                Depth = 1,
                ArrayLayers = 1,
                Mipmaps = 1,
                TextureType = RenderingDevice.TextureType.Type2D,
                UsageBits = RenderingDevice.TextureUsageBits.StorageBit
                    | RenderingDevice.TextureUsageBits.CanUpdateBit
                    | RenderingDevice.TextureUsageBits.CanCopyToBit
                    | RenderingDevice.TextureUsageBits.CanCopyFromBit
                    | RenderingDevice.TextureUsageBits.SamplingBit,
            };

            return _device.TextureCreate(info, new RDTextureView(), new Godot.Collections.Array<byte[]>());
        }

        private Rid CreateSet(Rid source, Rid destination)
        {
            var uniforms = new Godot.Collections.Array<RDUniform>();

            AddUniform(uniforms, 0, _terrain);
            AddUniform(uniforms, 1, _seed);
            AddUniform(uniforms, 2, source);
            AddUniform(uniforms, 3, destination);

            return _device.UniformSetCreate(uniforms, _shader, 0);
        }

        private static void AddUniform(Godot.Collections.Array<RDUniform> uniforms, int binding, Rid texture)
        {
            var uniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.Image,
                Binding = binding
            };

            uniform.AddId(texture);
            uniforms.Add(uniform);
        }

        #endregion

        #region Core - Descarte

        public void Dispose()
        {
            Release();

            if (_pipeline.IsValid)
            {
                _device.FreeRid(_pipeline);
            }

            if (_shader.IsValid)
            {
                _device.FreeRid(_shader);
            }

            _pipeline = _shader = default;

            if (_ownsDevice)
            {
                _device?.Free();
            }
        }

        private void Release()
        {
            foreach (var rid in new[] { _setPing, _setPong, _terrain, _seed, _ping, _pong })
            {
                if (rid.IsValid)
                {
                    _device.FreeRid(rid);
                }
            }

            _setPing = _setPong = _terrain = _seed = _ping = _pong = default;
            _size = default;
        }

        #endregion

        #region Utils

        private static void WriteFloat(byte[] target, int offset, float value)
        {
            BitConverter.TryWriteBytes(target.AsSpan(offset, 4), value);
        }

        #endregion
    }
}
