using Godot;
using Jogo25D.Light;
using System;
namespace Jogo25D.Testing
{
    public partial class LayeredLightingRegression : Node
    {
        private static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
        private static void Settle(LogicalLightWorld world) { while(!world.Field.Settled) world.Field.Process(1000000,10000); }
        public override async void _Ready()
        {
            try
            {
                var world=new LogicalLightWorld(0,"layers",1,false) { DepthLightEnabled=true };
                for(int y=0;y<=12;y++) for(int x=0;x<=12;x++)
                    if(x==0||x==12||y==0||y==12) world.SetTerrain(x,y,0);
                world.Field.SetRegion(0,0,32,32); Settle(world);
                Check(world.Field.Get(6,6).Sky==255,"Open back should admit depth daylight");
                long revision=world.Revision;
                for(int y=1;y<12;y++) for(int x=1;x<12;x++) world.SetBackground(x,y,true);
                Settle(world);
                Check(world.Field.Get(6,6).Sky==0,"Closed primary outline plus walls must be dark");
                Check(world.Revision==revision,"Walls changed primary shadow geometry");
                var computer=new LightMapComputer();
                foreach(bool open in new[]{false,true,false})
                {
                    world.SetBackground(6,6,!open); Settle(world);
                    computer.Begin(world,Vector2I.Zero,new(32,32),0,1,3,buildShadow:false);
                    while(!computer.Complete) computer.Process(10000);
                    using var light=computer.LightImage();
                    Check(open?light.GetPixel(12,12).R>0.99:light.GetPixel(12,12).R<0.01,"Light map did not follow wall open/close");
                    if(open) Check(world.Field.Get(8,6).Sky>0,"Opening did not propagate into wall-backed neighbours");
                }
                world.Field.SetSource(91,new LightCell(6,6),new LightValue(0,255,80,0));Settle(world);
                Check(world.Field.Get(7,6).R>0,"Emission did not propagate in closed room");
                world.Field.RemoveSource(91);Settle(world);
                Check(world.Field.Get(7,6).R==0,"Removed emission left stale light");
                world.Field.SetRegion(1000,1000,32,32);Settle(world);
                world.Field.SetRegion(0,0,32,32);Settle(world);
                Check(world.Field.Get(6,6).Sky==0,"Chunk cache rebuild lost wall darkness");
                GD.Print("LAYER LIGHT PASS: primary + walls, opening/removal, emission removal, chunk reconstruction");

                var casters=new LogicalLightWorld(0,"wall-receiver",1,false);
                for(int x=0;x<32;x++) casters.SetTerrain(x,2,0);
                var geometry=new AnalyticShadowGeometry();geometry.Begin(casters,Vector2I.Zero,new(32,32),0,1);
                while(!geometry.Complete) geometry.Process(10000);
                var material=new ShaderMaterial { Shader=GD.Load<Shader>("res://Assets/Shaders/wall_projected_shadow.gdshader") };
                material.SetShaderParameter("shadow_geometry",ImageTexture.CreateFromImage(geometry.Image()));
                material.SetShaderParameter("map_size",new Vector2(64,64));
                material.SetShaderParameter("sun_angle",0f);material.SetShaderParameter("penumbra",1f);
                material.SetShaderParameter("geometry_ready",true);material.SetShaderParameter("shadow_strength",0.65f);
                material.SetShaderParameter("map_world_origin",Vector2.Zero);
                material.SetShaderParameter("map_world_axis_x",new Vector2(1f/16,0));
                material.SetShaderParameter("map_world_axis_y",new Vector2(0,1f/16));
                using var white=Image.CreateEmpty(1,1,false,Image.Format.Rgba8);white.Fill(Colors.White);
                var pass=new SubViewport { Size=new(512,512),Disable3D=true,RenderTargetUpdateMode=SubViewport.UpdateMode.Always };AddChild(pass);
                pass.AddChild(new ColorRect { Size=pass.Size,Color=Colors.White });
                pass.AddChild(new Sprite2D { Texture=ImageTexture.CreateFromImage(white),Centered=false,Position=new(128,0),Scale=new(256,512),Material=material });
                pass.AddChild(new ColorRect { Position=new(256,160),Size=new(64,64),Color=Colors.Green });
                for(int i=0;i<4;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var frame=pass.GetTexture().GetImage();
                Check(frame.GetPixel(64,200).R>0.99,"Shadow contaminated sky outside wall fragments");
                Check(Math.Abs(frame.GetPixel(192,200).R-0.35)<0.01,"Wall did not receive projected shadow");
                Check(frame.GetPixel(272,200).G>0.99,"Wall shadow contaminated foreground entity");
                GD.Print("WALL RECEIVER PASS: walls shadowed, sky and foreground entity unaffected");
                GD.Print("LAYERED LIGHTING PASS");GetTree().Quit();
            }
            catch(Exception e) { GD.PushError(e.ToString());GetTree().Quit(1); }
        }
    }
}
