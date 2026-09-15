using Godot;
using Jogo25D.Light;
using System;
namespace Jogo25D.Testing
{
    public partial class ProjectedShadowRegression : Node
    {
        public override async void _Ready()
        {
            try
            {
                var world=new LogicalLightWorld(0,"projection",1,false);
                for(int y=4;y<8;y++) for(int x=12;x<20;x++) world.SetTerrain(x,y,0);
                var geometry=new AnalyticShadowGeometry();
                var material=new ShaderMaterial { Shader=GD.Load<Shader>("res://Assets/Shaders/projected_shadow.gdshader") };
                var pass=new SubViewport { Size=new(512,512),Disable3D=true,RenderTargetUpdateMode=SubViewport.UpdateMode.Always };
                AddChild(pass);
                pass.AddChild(new ColorRect { Size=pass.Size,Color=Colors.White });
                pass.AddChild(new ColorRect { Size=pass.Size,Material=material });
                material.SetShaderParameter("map_size",new Vector2(64,64));
                material.SetShaderParameter("sun_angle",0f);
                material.SetShaderParameter("shadow_strength",1f);
                material.SetShaderParameter("geometry_ready",true);
                foreach(float penumbra in new[]{1f,0f})
                {
                    geometry.Begin(world,Vector2I.Zero,new(32,32),0,penumbra);
                    while(!geometry.Complete) geometry.Process(10000);
                    material.SetShaderParameter("shadow_geometry",ImageTexture.CreateFromImage(geometry.Image()));
                    material.SetShaderParameter("penumbra",penumbra);
                    for(int i=0;i<4;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    using var frame=pass.GetTexture().GetImage();
                    if(frame.GetPixel(16*16,24*16).R>0.01 || frame.GetPixel(2*16,24*16).R<0.99)
                        throw new Exception("Shadow/light endpoints invalid");
                    int near=0,far=0;
                    for(int x=0;x<512;x++)
                    {
                        float a=frame.GetPixel(x,10*16).R,b=frame.GetPixel(x,28*16).R;
                        if(a>0.02&&a<0.98) near++;
                        if(b>0.02&&b<0.98) far++;
                        if(Math.Abs(b-frame.GetPixel(511-x,28*16).R)>0.01) throw new Exception("Projection changed solar axis");
                    }
                    if(penumbra==0 && far<=near+15) throw new Exception("Penumbra does not expand continuously");
                    GD.Print($"PROJECTED SHADOW aperture={penumbra}: transition near={near} far={far}");
                    frame.SavePng(ProjectSettings.GlobalizePath($"res://../.images/projected-shadow-{penumbra}.png"));
                }
                material.SetShaderParameter("shadow_strength",0f);
                for(int i=0;i<3;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var disabled=pass.GetTexture().GetImage();
                if(disabled.GetPixel(256,384).R<0.99) throw new Exception("Hidden lighting still affects output");
                material.Shader=GD.Load<Shader>("res://Assets/Shaders/projected_shadow_debug.gdshader");
                material.SetShaderParameter("shadow_geometry",ImageTexture.CreateFromImage(geometry.Image()));
                material.SetShaderParameter("map_size",new Vector2(64,64));
                material.SetShaderParameter("sun_angle",0f);
                material.SetShaderParameter("penumbra",0f);
                material.SetShaderParameter("geometry_ready",true);
                pass.GetChild<ColorRect>(0).Color=Colors.Red;
                for(int i=0;i<3;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var debug=pass.GetTexture().GetImage();
                if(debug.GetPixel(32,384).G<0.99 || debug.GetPixel(256,384).R>0.01)
                    throw new Exception("Debug is contaminated by scene color or shadow strength");
                int gradients=0;
                for(int x=0;x<512;x++)
                {
                    var c=debug.GetPixel(x,448);
                    if(Math.Abs(c.R-c.G)>0.004 || Math.Abs(c.R-c.B)>0.004) throw new Exception("Debug is not grayscale");
                    if(c.R>0.02 && c.R<0.98) gradients++;
                }
                if(gradients<20) throw new Exception("Debug lost penumbra gradient");
                GD.Print("SHADOW DEBUG PASS: opaque grayscale, penumbra preserved, independent of scene color and strength");
                // ESTAGIO DESLIGADO. Ele cobra a exclusao do trecho solido inicial e a regra de
                // corpo proprio, que sairam do calculo: a sombra agora projeta tudo sobre tudo.
                // Volta quando o comportamento voltar como MASCARA DE CAMADA, e nao como regra
                // dentro da sombra - ai o que se testa e quem RECEBE, nao quem projeta.
                /*
                for(int y=12;y<16;y++) for(int x=12;x<20;x++) world.SetTerrain(x,y,6);
                foreach(bool connected in new[]{false,true,false})
                {
                    // Connection deliberately outside the rendered 32x32 window.
                    for(int x=-5;x<=12;x++) { world.SetTerrain(x,7,connected?7:-1); world.SetTerrain(x,12,connected?6:-1); }
                    for(int y=7;y<=12;y++) world.SetTerrain(-5,y,connected?0:-1);
                    world.SetTerrain(12,7,0); world.SetTerrain(12,12,6);
                    geometry.Begin(world,Vector2I.Zero,new(32,32),0,0);
                    while(!geometry.Complete) geometry.Process(10000);
                    material.SetShaderParameter("shadow_geometry",ImageTexture.CreateFromImage(geometry.Image()));
                    for(int i=0;i<3;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    using var bodies=pass.GetTexture().GetImage();
                    float receiver=bodies.GetPixel(16*16,14*16).R;
                    if(receiver>0.01) throw new Exception("An air gap must cast shadow even with a remote connection");
                    if(bodies.GetPixel(16*16,6*16).R<0.99) throw new Exception("Body shadows itself");
                    if(bodies.GetPixel(16*16,10*16).R>0.01) throw new Exception("Body exclusion erased the shadow in air");
                }
                foreach(float aperture in new[]{0f,1f})
                foreach(bool gap in new[]{false,true,false})
                {
                    for(int y=8;y<12;y++) for(int x=12;x<20;x++) world.SetTerrain(x,y,gap?-1:7);
                    geometry.Begin(world,Vector2I.Zero,new(32,32),0,aperture);
                    while(!geometry.Complete) geometry.Process(10000);
                    material.SetShaderParameter("shadow_geometry",ImageTexture.CreateFromImage(geometry.Image()));
                    material.SetShaderParameter("penumbra",aperture);
                    for(int i=0;i<3;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    using var continuity=pass.GetTexture().GetImage();
                    float value=continuity.GetPixel(16*16,14*16).R;
                    if(gap?value>0.01:value<0.99) throw new Exception("Continuous solid / open gap / closed gap rule failed");
                }
                GD.Print("AIR GAP SHADOW PASS: solid prefix excluded, gap casts shadow despite remote connection");
                */
                GD.Print("PROJECTED SHADOW PASS: endpoints, symmetry, growing penumbra, zero strength");
                GetTree().Quit();
            }
            catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
