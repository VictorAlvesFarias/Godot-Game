using Godot;
using Jogo25D.Core;
using Jogo25D.Light;
using System;
namespace Jogo25D.Testing
{
    public partial class LayeredWorldIntegration : Node
    {
        public override async void _Ready()
        {
            try
            {
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                Game.Managers.WorldManager.Node.SpawnWorld();
                Game.Managers.LightMapManager.Node.UseAuthoredWorlds();
                var dims=Game.Managers.DimensionManager.Node;
                int baseOnly=0;
                foreach(var id in dims.Ids)
                {
                    var basis=dims.ResolveBaseLayer(id);var compose=dims.ResolveLayer(id);
                    if(basis==null) continue;
                    var logical=Game.Managers.LightMapManager.Node.GetWorld(id);
                    foreach(var cell in basis.GetUsedCells())
                    {
                        if(compose.GetCellSourceId(cell)!=-1) continue;
                        baseOnly++;
                        if(logical.Opacity(cell.X,cell.Y)!=255)
                            throw new Exception($"Base-only terrain missing from light map: {id} {cell}");
                    }
                }
                if(baseOnly==0) throw new Exception("Fixture has no Base-only terrain to verify");
                GD.Print($"BASE OCCUPANCY PASS: {baseOnly} cells absent from Compose remain solid");
                var shown=dims.ResolveParent("upsidedown");
                var viewport=(SubViewport)shown.GetViewport();
                ((CanvasItem)viewport.GetParent()).Visible=true;
                viewport.RenderTargetUpdateMode=SubViewport.UpdateMode.Always;
                ((CanvasItem)dims.ResolveParent("overworld").GetViewport().GetParent()).Visible=false;
                foreach(var child in GetParent().GetNode("Ui").GetChildren())
                    if(child is CanvasItem ci) ci.Visible=false;else if(child is CanvasLayer cl) cl.Visible=false;
                var grid=dims.ResolveLayer("upsidedown");
                var camera=shown.GetNode<Camera2D>("Camera2D");camera.SetPhysicsProcess(false);
                camera.GlobalPosition=grid.ToGlobal(grid.MapToLocal(new(2,-25)));camera.Zoom=new(2,2);
                var walls=shown.GetNode<Jogo25D.Blocks.BackgroundWallLayer>("BackgroundWalls");
                var originalWallMaterial=walls.Material;
                var world=Game.Managers.LightMapManager.Node.GetWorld("upsidedown");
                for(int y=-34;y<-12;y++) for(int x=-8;x<11;x++)
                { walls.SetCell(new(x,y),0,new(1,1));world.SetBackground(x,y,true); }
                var light=Game.Managers.LightMapManager.Node.Nodes["upsidedown"];
                light.Settings=(Resource)light.Settings.Duplicate();
                light.Settings.Set(nameof(LightMapData.DebugShadow),false);
                int frames=0;
                do { await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
                while(!light.PresentationReady && ++frames<1200);
                if(!light.PresentationReady) throw new Exception("Layered world did not settle");
                if(walls.Material is not ShaderMaterial) throw new Exception("Wall did not receive its shadow material");
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://../.images/layered-world.png"));
                long updates=light.SolarUpdates;
                light.Settings.Set(nameof(LightMapData.DebugShadow),true);
                for(int i=0;i<4;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                var overlay=light.GetNode<Sprite2D>("ProjectedShadow");
                if(!((ShaderMaterial)overlay.Material).Shader.ResourcePath.EndsWith("projected_shadow_debug.gdshader")) throw new Exception("Raw debug did not activate");
                light.Settings.Set(nameof(LightMapData.DebugShadow),false);
                for(int i=0;i<4;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                if(!((ShaderMaterial)overlay.Material).Shader.ResourcePath.EndsWith("layered_light.gdshader") || light.SolarUpdates!=updates)
                    throw new Exception("Debug toggle rebuilt geometry or failed to restore darkness");
                light.LightMapEnabled=false;
                for(int i=0;i<2;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                if(overlay.Visible || walls.Material!=originalWallMaterial) throw new Exception("Disabled lighting left a shadow material active");
                GD.Print($"LAYERED WORLD PASS: settled {frames} frames, wall material, debug toggle, disabled cleanup");GetTree().Quit();
            }
            catch(Exception e) { GD.PushError(e.ToString());GetTree().Quit(1); }
        }
    }
}
