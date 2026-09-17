using Godot;
using System;
using System.Collections.Generic;

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
            _gpu?.Dispose();
            _gpu = null;
        }
    }
}
