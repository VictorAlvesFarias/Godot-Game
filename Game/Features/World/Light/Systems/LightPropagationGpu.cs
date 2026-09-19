using Godot;
using Jogo25D.Constants;
using System;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    // Same eight-neighbor solver as CPU: linear loss in air, multiplicative absorption in solids.
    public sealed class LightPropagationGpu : IDisposable
    {
        // Unit intensity travels at most 24 air tiles; solid absorption shortens the path.
        public static readonly int Iterations =
            Mathf.CeilToInt(1f / LightingConstants.AIR_LIGHT_LOSS);

        private readonly RenderingDevice _device;
        private readonly bool _ownsDevice;
        private Rid _shader, _pipeline;
        private Rid _terrain, _seed, _ping, _pong;
        private Rid _setPing, _setPong;
        private Vector2I _size;

        public LightPropagationGpu(RenderingDevice device = null)
        {
            _device = device ?? RenderingServer.CreateLocalRenderingDevice();
            _ownsDevice = device == null;
            if (_device == null) throw new InvalidOperationException("Sem RenderingDevice: compute de luz indisponivel.");

            var file = GD.Load<RDShaderFile>("res://Features/World/Light/Resources/LightPropagation.glsl");
            var spirv = file.GetSpirV();
            var erro = spirv.CompileErrorCompute;
            if (!string.IsNullOrEmpty(erro)) throw new InvalidOperationException("LightPropagation.glsl: " + erro);
            _shader = _device.ShaderCreateFromSpirV(spirv);
            _pipeline = _device.ComputePipelineCreate(_shader);
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

        private void Allocate(Vector2I size)
        {
            if (_size == size && _terrain.IsValid) return;
            Release();
            _size = size;
            _terrain = CreateImage(RenderingDevice.DataFormat.R8Unorm, size);
            _seed = CreateImage(RenderingDevice.DataFormat.R32G32B32A32Sfloat, size);
            _ping = CreateImage(RenderingDevice.DataFormat.R32G32B32A32Sfloat, size);
            _pong = CreateImage(RenderingDevice.DataFormat.R32G32B32A32Sfloat, size);
            _setPing = CreateSet(_ping, _pong);
            _setPong = CreateSet(_pong, _ping);
        }

        private Rid CreateSet(Rid source, Rid destination)
        {
            var uniforms = new Godot.Collections.Array<RDUniform>();
            void Add(int binding, Rid texture)
            {
                var uniform = new RDUniform { UniformType = RenderingDevice.UniformType.Image, Binding = binding };
                uniform.AddId(texture);
                uniforms.Add(uniform);
            }
            Add(0, _terrain);
            Add(1, _seed);
            Add(2, source);
            Add(3, destination);
            return _device.UniformSetCreate(uniforms, _shader, 0);
        }

        // Recebe as mesmas entradas do LightPropagationSystem.Compute e devolve o mesmo grid.
        public Color[,] Compute(Rect2I region, Func<Vector2I, bool> isSolid, IReadOnlyList<LightSource> sources, bool readback = true)
        {
            var size = region.Size;
            Allocate(size);

            var solid = new byte[size.X * size.Y];
            var seed = new byte[size.X * size.Y * 16];
            for (int y = 0; y < size.Y; y++)
            for (int x = 0; x < size.X; x++)
                solid[y * size.X + x] = isSolid(new Vector2I(region.Position.X + x, region.Position.Y + y)) ? (byte)255 : (byte)0;

            // Semeadura por max, igual ao Seed() do BFS.
            var maxima = new Color[size.X, size.Y];
            foreach (var source in sources)
            {
                var local = source.Cell - region.Position;
                if (local.X < 0 || local.Y < 0 || local.X >= size.X || local.Y >= size.Y) continue;
                var atual = maxima[local.X, local.Y];
                maxima[local.X, local.Y] = new Color(
                    Mathf.Max(atual.R, source.Color.R),
                    Mathf.Max(atual.G, source.Color.G),
                    Mathf.Max(atual.B, source.Color.B));
            }
            for (int y = 0; y < size.Y; y++)
            for (int x = 0; x < size.X; x++)
            {
                int slot = (y * size.X + x) * 16;
                var c = maxima[x, y];
                WriteFloat(seed, slot, c.R);
                WriteFloat(seed, slot + 4, c.G);
                WriteFloat(seed, slot + 8, c.B);
                WriteFloat(seed, slot + 12, 1f);
            }

            _device.TextureUpdate(_terrain, 0, solid);
            _device.TextureUpdate(_seed, 0, seed);
            _device.TextureUpdate(_ping, 0, seed);
            _device.TextureUpdate(_pong, 0, seed);

            var constants = new byte[32];
            Buffer.BlockCopy(new[] { size.X, size.Y }, 0, constants, 0, 8);
            Buffer.BlockCopy(new[]
            {
                LightingConstants.AIR_LIGHT_LOSS,
                LightingConstants.SOLID_FALLOFF,
                LightingConstants.MIN_LIGHT_THRESHOLD,
                0f, 0f, 0f,
            }, 0, constants, 8, 24);

            uint groupsX = (uint)((size.X + 7) / 8);
            uint groupsY = (uint)((size.Y + 7) / 8);
            var list = _device.ComputeListBegin();
            _device.ComputeListBindComputePipeline(list, _pipeline);
            for (int i = 0; i < Iterations; i++)
            {
                _device.ComputeListBindUniformSet(list, (i % 2 == 0) ? _setPing : _setPong, 0);
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

            var final = (Iterations % 2 == 0) ? _ping : _pong;
            if (!readback) return null;
            var data = _device.TextureGetData(final, 0);
            var grid = new Color[size.X, size.Y];
            for (int y = 0; y < size.Y; y++)
            for (int x = 0; x < size.X; x++)
            {
                int slot = (y * size.X + x) * 16;
                grid[x, y] = new Color(ReadFloat(data, slot), ReadFloat(data, slot + 4), ReadFloat(data, slot + 8));
            }
            return grid;
        }

        // Called on the render thread with the main RenderingDevice. The output stays
        // on the GPU; copy it so subsequent jobs can reuse the ping/pong working set.
        public Rid ComputeOutput(Rect2I region, Func<Vector2I,bool> solid, IReadOnlyList<LightSource> sources)
        {
            Compute(region,solid,sources,readback:false);
            var output=CreateImage(RenderingDevice.DataFormat.R32G32B32A32Sfloat,region.Size);
            var final=(Iterations%2==0)?_ping:_pong;
            _device.TextureCopy(final,output,Vector3.Zero,Vector3.Zero,new Vector3(region.Size.X,region.Size.Y,1),0,0,0,0);
            return output;
        }

        private static void WriteFloat(byte[] target, int offset, float value) =>
            BitConverter.TryWriteBytes(target.AsSpan(offset, 4), value);

        private static float ReadFloat(byte[] source, int offset) =>
            BitConverter.ToSingle(source, offset);

        private void Release()
        {
            foreach (var rid in new[] { _setPing, _setPong, _terrain, _seed, _ping, _pong })
            {
                if (rid.IsValid) _device.FreeRid(rid);
            }
            _setPing = _setPong = _terrain = _seed = _ping = _pong = default;
            _size = default;
        }

        public void Dispose()
        {
            Release();
            if (_pipeline.IsValid) _device.FreeRid(_pipeline);
            if (_shader.IsValid) _device.FreeRid(_shader);
            _pipeline = _shader = default;
            if (_ownsDevice) _device?.Free();
        }
    }
}
