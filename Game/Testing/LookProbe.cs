using Godot;
using Jogo25D.Core;
using Jogo25D.Light;
using System;

namespace Jogo25D.Testing
{
    public partial class LookProbe : Node
    {
        public override async void _Ready()
        {
            try
            {
                // Filho de Main: o _Ready daqui corre antes do dele, entao espera os managers.
                for (int i = 0; i < 240 && Game.Managers.WorldManager.Node == null; i++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Game.Managers.WorldManager.Node.SpawnWorld();
                foreach (var c in GetParent().GetNode("Ui").GetChildren())
                    if (c is CanvasItem ci) ci.Visible = false; else if (c is CanvasLayer cl) cl.Visible = false;
                var dims = Game.Managers.DimensionManager.Node;
                var shown = dims.ResolveParent("upsidedown");
                var vp = (SubViewport)shown.GetViewport();
                ((CanvasItem)vp.GetParent()).Visible = true;
                vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
                ((CanvasItem)dims.ResolveParent("overworld").GetViewport().GetParent()).Visible = false;
                Game.Managers.LightMapManager.Node.UseAuthoredWorlds();
                var grid = dims.ResolveLayer("upsidedown");
                var cam = shown.GetNode<Camera2D>("Camera2D");
                cam.SetPhysicsProcess(false);
                cam.GlobalPosition = grid.ToGlobal(grid.MapToLocal(new(-2, -21)));
                cam.Zoom = new(1.8f, 1.8f);
                var light = Game.Managers.LightMapManager.Node.Nodes["upsidedown"];
                light.Settings = (Resource)light.Settings.Duplicate();
                foreach (float v in new[] { 0f })
                {
                    light.Settings.Set(nameof(LightMapData.ShowRawMap), false);
                    for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    int f = 0;
                    while (!light.PresentationReady && ++f < 3000) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(
                        ProjectSettings.GlobalizePath("res://../.images") + "/revertido.png");
                    GD.Print($"EB {v}");
                }
                GD.Print("EB DONE");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
