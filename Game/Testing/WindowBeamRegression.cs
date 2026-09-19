using Godot;
using Jogo25D.Light;
using System;
namespace Jogo25D.Testing;
[Tool]
public partial class WindowBeamRegression : Node
{
    public override async void _Ready()
    {
        if(Engine.IsEditorHint() && System.Environment.GetEnvironmentVariable("WINDOW_BEAM_EDITOR_TEST")!="1") return;
        try {
            var world=new LogicalLightWorld(0,"beam-test",1,false);
            for(int y=0;y<32;y++) for(int x=0;x<32;x++) world.SetBackground(x,y,true);
            var cache=new WindowBeamCache();
            async System.Threading.Tasks.Task<Image> Render(float angle= -10,float shadow=1,float ambient=1) {
                cache.Update(this,world,Vector2I.Zero,new Vector2I(32,32),angle,0,shadow,ambient);
                for(int i=0;i<10;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                return GetNode<SubViewport>("WindowBeamCache").GetTexture().GetImage();
            }
            using(var closed=await Render()) if(closed.GetPixel(72,64).R>0.01) throw new Exception("Closed room has a beam");
            for(int y=4;y<8;y++) for(int x=12;x<16;x++) world.SetBackground(x,y,false);
            using(var open=await Render()) {
                if(open.GetPixel(72,64).R<0.3) throw new Exception("Window did not illuminate downstream wall");
                if(open.GetPixel(24,64).R>0.01) throw new Exception("Beam is not directional");
                open.Resize(512,512,Image.Interpolation.Nearest);
                open.SavePng("res://../.images/window-beam-regression.png");
            }
            world.SetBackground(14,2,false);
            for(int x=0;x<32;x++) if(x<12 || x>=18) world.SetTerrain(x,8,0);
            SceneLightSources.Set(GetParent(),987654,new Vector2I(14,2),new Color(1,.4f,.1f));
            using(var ignored=await Render()) { }
            using(var local=GetNode<SubViewport>("LocalWindowBeamCache").GetTexture().GetImage()) {
                var c=local.GetPixel(72,64);
                if(c.R<.25f || c.R<c.G+.1f) throw new Exception("Local source did not transmit its color through window");
                local.SavePng("res://../.images/local-window-beam.png");
            }
            world.SetBackground(14,2,true);
            using(var ignored=await Render()) { }
            using(var interior=GetNode<SubViewport>("LocalWindowBeamCache").GetTexture().GetImage())
                if(interior.GetPixel(72,64).R>.01f) throw new Exception("Interior source projected an aperture beam");
            var registered=new System.Collections.Generic.List<LightSource>();
            SceneLightSources.Collect(GetParent(),new Rect2I(0,0,32,32),registered);
            if(registered.Count!=1) throw new Exception("Interior source lost its normal local illumination");
            world.SetBackground(14,2,false);
            world.SetTerrain(14,3,0);
            using(var ignored=await Render()) { }
            using(var blockedLocal=GetNode<SubViewport>("LocalWindowBeamCache").GetTexture().GetImage())
                if(blockedLocal.GetPixel(72,64).R>.01f) throw new Exception("Local beam ignored blocker beyond aperture");
            world.SetTerrain(14,3,-1);
            for(int x=12;x<18;x++) world.SetTerrain(x,8,0);
            for(int y=4;y<8;y++) for(int x=12;x<16;x++) world.SetBackground(x,y,true);
            using(var ignored=await Render()) { }
            using(var local=GetNode<SubViewport>("LocalWindowBeamCache").GetTexture().GetImage())
                if(local.GetPixel(72,64).R>.01f) throw new Exception("Closed window retained local beam");
            SceneLightSources.Remove(GetParent(),987654);
            world.SetBackground(14,2,true);
            for(int x=0;x<32;x++) world.SetTerrain(x,8,-1);
            for(int y=4;y<8;y++) for(int x=12;x<16;x++) world.SetBackground(x,y,false);
            using(var rotated=await Render(35)) {
                if(rotated.GetPixel(32,64).R<0.15 || rotated.GetPixel(76,64).R>0.01) throw new Exception("Sun angle did not rotate the beam");
            }
            for(int y=4;y<8;y++) for(int x=12;x<16;x++) world.SetBackground(x,y,true);
            using(var shut=await Render()) if(shut.GetPixel(72,64).R>0.01) throw new Exception("Closing the window left cached light");
            for(int y=4;y<8;y++) for(int x=12;x<16;x++) world.SetBackground(x,y,false);
            using(var normal=await Render())
            using(var reshaped=await Render(-10,0,0))
                if(System.Linq.Enumerable.SequenceEqual(normal.GetData(),reshaped.GetData())) throw new Exception("Transition controls did not update cached pixels");
            using var settings=new LightMapData();
            int changes=0;settings.Changed+=()=>changes++;
            settings.SunAngleDegrees=30;settings.SunColor=Colors.Orange;settings.SunPenumbra=0.5f;
            if(changes!=3) throw new Exception("Solar settings did not emit changes");
            using var grid=new TileMapLayer { TileSet=new TileSet { TileSize=new Vector2I(16,16) } };
            using var material=new ShaderMaterial { Shader=GD.Load<Shader>("res://Features/World/Light/Resources/TerrainLightOverlay.gdshader") };
            cache.Bind(material,grid,true,settings.SunColor);
            if(material.GetShaderParameter("window_sun_color").AsColor()!=Colors.Orange) throw new Exception("Solar color was not published");
            for(int x=0;x<32;x++) world.SetTerrain(x,12,0);
            using(var blocked=await Render()) if(blocked.GetPixel(72,64).R>0.01) throw new Exception("Beam crosses foreground obstacle");
            GD.Print("WINDOW BEAM PASS: closed, window, direction, rotation, closing, obstacle, transitions, color, resource notifications");
            GetTree().Quit();
        } catch(Exception e) { GD.PushError(e.ToString());GetTree().Quit(1); }
    }
}
