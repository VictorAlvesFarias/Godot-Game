using Godot;
using Jogo25D.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Testing
{
    // Anda com a camera pelo mundo e mede o tempo de frame, ligando e desligando cada subsistema
    // de luz. Serve pra atribuir o engasgo a quem ele pertence, em vez de adivinhar.
    public partial class LightWalkProfile : Node
    {
        private const int FramesPorPasse = 420;
        private const float VelocidadeTilesPorFrame = 0.45f;

        public override async void _Ready()
        {
            try
            {
                // Sem vsync o tempo de frame mostra o custo real; com ele tudo vira 16,7 ms.
                DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
                Engine.MaxFps = 0;

                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Game.Managers.WorldManager.Node.SpawnWorld();
                Game.Managers.LightMapManager.Node.UseAuthoredWorlds();

                var streaming = Game.Managers.TileStreamingManager.Node;
                if (streaming != null) streaming.Enabled = false;

                var dims = Game.Managers.DimensionManager.Node;
                var mostrar = dims.ResolveParent("upsidedown");
                var viewport = (SubViewport)mostrar.GetViewport();
                ((CanvasItem)viewport.GetParent()).Visible = true;
                viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
                ((CanvasItem)dims.ResolveParent("overworld").GetViewport().GetParent()).Visible = false;

                var grid = dims.ResolveLayer("upsidedown");
                var camera = mostrar.GetNode<Camera2D>("Camera2D");
                camera.SetPhysicsProcess(false);
                camera.Zoom = new Vector2(2, 2);

                var lightMap = mostrar.GetNodeOrNull<Jogo25D.Light.LightMap2D>("LightMap");
                var manager = Game.Managers.LightingManager.Node;
                int tileSize = grid.TileSet.TileSize.X;

                // Deixa tudo assentar antes de comecar a medir.
                for (int i = 0; i < 300; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                async System.Threading.Tasks.Task<List<double>> Passe(bool luzAmbiente, bool solEParedes)
                {
                    if (manager != null) manager.SetProcess(luzAmbiente);
                    if (lightMap != null) lightMap.LightMapEnabled = solEParedes;

                    camera.GlobalPosition = grid.ToGlobal(grid.MapToLocal(new Vector2I(-100, -25)));
                    for (int i = 0; i < 90; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                    var tempos = new List<double>(FramesPorPasse);
                    ulong anterior = Time.GetTicksUsec();
                    for (int i = 0; i < FramesPorPasse; i++)
                    {
                        camera.GlobalPosition += new Vector2(VelocidadeTilesPorFrame * tileSize, 0);
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        ulong agora = Time.GetTicksUsec();
                        tempos.Add((agora - anterior) / 1000.0);
                        anterior = agora;
                    }
                    return tempos;
                }

                void Relatar(string nome, List<double> tempos)
                {
                    var ordenado = tempos.OrderBy(t => t).ToList();
                    double p50 = ordenado[ordenado.Count / 2];
                    double p95 = ordenado[(int)(ordenado.Count * 0.95)];
                    double pior = ordenado[^1];
                    int acima16 = tempos.Count(t => t > 16.7);
                    int acima33 = tempos.Count(t => t > 33.3);
                    GD.Print($"{nome,-34} p50={p50,6:F2}  p95={p95,6:F2}  pior={pior,7:F2}   " +
                        $">16.7ms: {acima16,3}   >33.3ms: {acima33,3}  (de {tempos.Count})");
                }

                GD.Print("ANDANDO " + FramesPorPasse + " frames por passe, "
                    + VelocidadeTilesPorFrame + " tile/frame, zoom 2x");
                Relatar("tudo ligado", await Passe(true, true));
                Relatar("so luz ambiente (sem sol/parede)", await Passe(true, false));
                Relatar("so sol/parede (sem luz ambiente)", await Passe(false, true));
                Relatar("tudo desligado (linha de base)", await Passe(false, false));

                GD.Print("PERFIL OK");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
