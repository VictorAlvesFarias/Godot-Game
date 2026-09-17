using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jogo25D.Light
{
    // Escolhe onde a propagacao roda. Usa o compute shader quando existe RenderingDevice, e cai de
    // volta no LightPropagationSystem (CPU) quando nao existe - headless, driver sem Vulkan, ou
    // falha em tempo de execucao. Os dois caminhos produzem o mesmo grid: a conta e a mesma, so o
    // lugar muda. Um dispatcher por consumidor, porque as texturas sao dimensionadas pela regiao.
    public sealed class LightPropagationDispatcher : IDisposable
    {
        private LightPropagationGpu _gpu;
        private bool _tentou;
        private LightPropagationGpu _renderGpu;
        private bool _disposed;
        private bool _renderFailed;
        public async Task<LightTextureOutput> ComputeTextureAsync(Rect2I region, Func<Vector2I,bool> isSolid, IReadOnlyList<LightSource> sources)
        {
            if(_disposed) throw new ObjectDisposedException(nameof(LightPropagationDispatcher));
            if(!_renderFailed && RenderingServer.GetRenderingDevice()!=null)
            {
                // Snapshot scene data on the main thread; no TileMap/Node access in GPU jobs.
                var solid=new bool[region.Size.X*region.Size.Y];
                for(int y=0;y<region.Size.Y;y++) for(int x=0;x<region.Size.X;x++)
                    solid[y*region.Size.X+x]=isSolid(region.Position+new Vector2I(x,y));
                var seeds=new List<LightSource>(sources);
                var completion=new TaskCompletionSource<Rid>(TaskCreationOptions.RunContinuationsAsynchronously);
                RenderingServer.CallOnRenderThread(Callable.From(() =>
                {
                    try
                    {
                        _renderGpu ??= new LightPropagationGpu(RenderingServer.GetRenderingDevice());
                        completion.SetResult(_renderGpu.ComputeOutput(region,c => solid[(c.Y-region.Position.Y)*region.Size.X+c.X-region.Position.X],seeds));
                    }
                    catch(Exception error) { completion.SetException(error); }
                }));
                try
                {
                    var rid=await completion.Task;
                    var result=new LightTextureOutput(rid);
                    if(_disposed) { result.Dispose();throw new ObjectDisposedException(nameof(LightPropagationDispatcher)); }
                    return result;
                }
                catch(ObjectDisposedException) { throw; }
                catch(Exception error) { _renderFailed=true;GD.PushWarning("GPU texture path unavailable: "+error.Message); }
            }
            var grid=LightPropagationSystem.Compute(region,isSolid,sources);
            using var image=Image.CreateEmpty(region.Size.X,region.Size.Y,false,Image.Format.Rgba8);
            for(int y=0;y<region.Size.Y;y++) for(int x=0;x<region.Size.X;x++) image.SetPixel(x,y,grid[x,y]);
            return new LightTextureOutput(ImageTexture.CreateFromImage(image));
        }

        public bool UsandoGpu => _gpu != null;

        public Color[,] Compute(Rect2I region, Func<Vector2I, bool> isSolid, IReadOnlyList<LightSource> sources)
        {
            if (!_tentou)
            {
                _tentou = true;

                try
                {
                    _gpu = new LightPropagationGpu();
                }
                catch (Exception e)
                {
                    GD.PushWarning("[LightPropagationDispatcher] Sem compute de luz na GPU, seguindo na CPU: " + e.Message);
                }
            }

            if (_gpu != null)
            {
                try
                {
                    return _gpu.Compute(region, isSolid, sources);
                }
                catch (Exception e)
                {
                    GD.PushError("[LightPropagationDispatcher] Compute de luz falhou, caindo para a CPU: " + e);
                    _gpu.Dispose();
                    _gpu = null;
                }
            }

            return LightPropagationSystem.Compute(region, isSolid, sources);
        }

        public void Dispose()
        {
            _disposed=true;
            RenderingServer.CallOnRenderThread(Callable.From(() => { _renderGpu?.Dispose();_renderGpu=null; }));
            _gpu?.Dispose();
            _gpu = null;
        }
    }
}
