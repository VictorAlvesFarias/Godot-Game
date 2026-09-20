using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jogo25D.Light
{
    public sealed class LightPropagationDispatcher : IDisposable
    {
        #region Dinamic properties

        private LightPropagationGpu _gpu;
        private LightPropagationGpu _renderGpu;

        private bool _attempted;
        private bool _disposed;
        private bool _renderFailed;

        #endregion

        #region Core - Textura

        public async Task<LightTextureOutput> ComputeTextureAsync(Rect2I region, Func<Vector2I, bool> isSolid, IReadOnlyList<LightSource> sources)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LightPropagationDispatcher));
            }

            if (!_renderFailed && RenderingServer.GetRenderingDevice() != null)
            {
                var solid = new bool[region.Size.X * region.Size.Y];

                for (var y = 0; y < region.Size.Y; y++)
                {
                    for (var x = 0; x < region.Size.X; x++)
                    {
                        solid[y * region.Size.X + x] = isSolid(region.Position + new Vector2I(x, y));
                    }
                }

                var seeds = new List<LightSource>(sources);
                var completion = new TaskCompletionSource<Rid>(TaskCreationOptions.RunContinuationsAsynchronously);

                RenderingServer.CallOnRenderThread(Callable.From(() =>
                {
                    try
                    {
                        _renderGpu ??= new LightPropagationGpu(RenderingServer.GetRenderingDevice());

                        completion.SetResult(_renderGpu.ComputeOutput(
                            region,
                            cell => solid[(cell.Y - region.Position.Y) * region.Size.X + cell.X - region.Position.X],
                            seeds));
                    }
                    catch (Exception error)
                    {
                        completion.SetException(error);
                    }
                }));

                try
                {
                    var rid = await completion.Task;
                    var result = new LightTextureOutput(rid);

                    if (_disposed)
                    {
                        result.Dispose();

                        throw new ObjectDisposedException(nameof(LightPropagationDispatcher));
                    }

                    return result;
                }
                catch (ObjectDisposedException)
                {
                    throw;
                }
                catch (Exception error)
                {
                    _renderFailed = true;

                    GD.PushWarning("GPU texture path unavailable: " + error.Message);
                }
            }

            var grid = LightPropagationSystem.Compute(region, isSolid, sources);

            using var image = Image.CreateEmpty(region.Size.X, region.Size.Y, false, Image.Format.Rgba8);

            for (var y = 0; y < region.Size.Y; y++)
            {
                for (var x = 0; x < region.Size.X; x++)
                {
                    image.SetPixel(x, y, grid[x, y]);
                }
            }

            return new LightTextureOutput(ImageTexture.CreateFromImage(image));
        }

        #endregion

        #region Core - Grade

        public Color[,] Compute(Rect2I region, Func<Vector2I, bool> isSolid, IReadOnlyList<LightSource> sources)
        {
            if (!_attempted)
            {
                _attempted = true;

                try
                {
                    _gpu = new LightPropagationGpu();
                }
                catch (Exception error)
                {
                    GD.PushWarning("[LightPropagationDispatcher] Sem compute de luz na GPU, seguindo na CPU: " + error.Message);
                }
            }

            if (_gpu != null)
            {
                try
                {
                    return _gpu.Compute(region, isSolid, sources);
                }
                catch (Exception error)
                {
                    GD.PushError("[LightPropagationDispatcher] Compute de luz falhou, caindo para a CPU: " + error);

                    _gpu.Dispose();
                    _gpu = null;
                }
            }

            return LightPropagationSystem.Compute(region, isSolid, sources);
        }

        #endregion

        #region Core - Descarte

        public void Dispose()
        {
            _disposed = true;

            RenderingServer.CallOnRenderThread(Callable.From(() =>
            {
                _renderGpu?.Dispose();
                _renderGpu = null;
            }));

            _gpu?.Dispose();
            _gpu = null;
        }

        #endregion
    }
}
