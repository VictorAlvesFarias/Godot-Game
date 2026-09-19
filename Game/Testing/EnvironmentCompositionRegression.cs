using Godot;
using System;
namespace Jogo25D.Testing;
public partial class EnvironmentCompositionRegression : Node
{
 public override async void _Ready() {
  try {
   var vp=new SubViewport { Size=new Vector2I(32,32),Disable3D=true,RenderTargetUpdateMode=SubViewport.UpdateMode.Always };
   AddChild(vp);
   vp.AddChild(new ColorRect { Size=new Vector2(32,32),Color=Colors.White });
   ImageTexture Tex(Color c) { using var image=Image.CreateEmpty(1,1,false,Image.Format.Rgba8);image.Fill(c);return ImageTexture.CreateFromImage(image); }
   using var light=Tex(new Color(.5f,.5f,.5f));using var mask=Tex(Colors.White);using var cells=Tex(new Color(0,1,0));using var beam=Tex(Colors.Black);
   // Feed full occlusion to the actual composition shader, independently of geometry.
   var source=GD.Load<Shader>("res://Features/World/Light/Resources/TerrainLightOverlay.gdshader");
   var shader=new Shader { Code=source.Code.Replace("geometry_ready?projected_shadow(p,fwidth(p)):0.0","1.0") };
   var mat=new ShaderMaterial { Shader=shader };
   mat.SetShaderParameter("terrain_mask",mask);mat.SetShaderParameter("window_cells",cells);mat.SetShaderParameter("window_beam",beam);
   mat.SetShaderParameter("window_beam_ready",true);mat.SetShaderParameter("beam_size",new Vector2(32,32));mat.SetShaderParameter("beam_axis_x",Vector2.Right);mat.SetShaderParameter("beam_axis_y",Vector2.Down);
   vp.AddChild(new Sprite2D { Texture=light,Centered=false,Scale=new Vector2(32,32),Material=mat });
   async System.Threading.Tasks.Task<float> Pixel() { for(int i=0;i<4;i++) await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=vp.GetTexture().GetImage();return image.GetPixel(16,16).R; }
   void SetLight(float value) { using var image=Image.CreateEmpty(1,1,false,Image.Format.Rgba8);image.Fill(new Color(value,value,value));light.Update(image); }
   SetLight(.8f);
   mat.SetShaderParameter("shadow_strength",0f);float unshadowed=await Pixel();
   mat.SetShaderParameter("ambient_light_influence",0f);
   mat.SetShaderParameter("shadow_strength",1f);float opaque=await Pixel();
   if(opaque>.045f || unshadowed<.75f) throw new Exception("Shadow strength is not opacity");
   mat.SetShaderParameter("ambient_light_influence",1f);float open=await Pixel();
   if(open<.55f || open>=unshadowed) throw new Exception("Available light did not fill shadow");
   SetLight(.05f);float closed=await Pixel();
   if(closed>.045f || open<closed+.5f) throw new Exception("Ambient influence lights closed space like open space");
   mat.SetShaderParameter("ambient_light_influence",0f);
   mat.SetShaderParameter("window_beam",mask);float window=await Pixel();
   if(window<.9f) throw new Exception("Window light disappeared at zero ambient influence");
   using var halfBeam=Tex(new Color(.5f,.5f,.5f));
   mat.SetShaderParameter("window_beam",halfBeam);float partialWindow=await Pixel();
   if(Math.Abs(partialWindow-.5f)>.01f) throw new Exception("Window gradient was lost in composition");
   mat.SetShaderParameter("window_beam",beam);SetLight(.8f);
   mat.SetShaderParameter("shadow_strength",0f);mat.SetShaderParameter("ambient_light_influence",0f);float noFill=await Pixel();
   mat.SetShaderParameter("ambient_light_influence",1f);float fill=await Pixel();
   if(Math.Abs(noFill-fill)>.005f) throw new Exception("Influence changed unshadowed light intensity");
   SetLight(0);
   mat.SetShaderParameter("shadow_strength",1f);mat.SetShaderParameter("ambient_light_influence",0f);float sealed0=await Pixel();
   mat.SetShaderParameter("ambient_light_influence",1f);float sealed1=await Pixel();
   if(Math.Abs(sealed0-sealed1)>.005f) throw new Exception("Display floor fills sealed-room shadow");
   using var sky=Tex(new Color(0,1,1));mat.SetShaderParameter("window_cells",sky);mat.SetShaderParameter("sky_access",sky);
   mat.SetShaderParameter("shadow_strength",0f);mat.SetShaderParameter("window_sun_color",new Color(1,.1f,.1f));
   await Pixel();using(var image=vp.GetTexture().GetImage()) {
     var pixel=image.GetPixel(16,16);
     if(pixel.R<pixel.G+.3f) throw new Exception("Solar color does not tint ambient sky light");
   }
   var world=new Jogo25D.Light.LogicalLightWorld(0,"sky-access-test",1,false);
   for(int y=4;y<=27;y++) for(int x=4;x<=27;x++) {
     world.SetBackground(x,y,true);
     if(x==4||x==27||y==4||y==27) world.SetTerrain(x,y,0);
   }
   byte[] field=Jogo25D.Light.SkyAccessField.Build(world,Vector2I.Zero,new Vector2I(32,32),16);
   if(field[(16*32+16)*4+2]!=0) throw new Exception("Sky crossed a solid wall into sealed room");
   world.SetTerrain(15,4,-1);
   field=Jogo25D.Light.SkyAccessField.Build(world,Vector2I.Zero,new Vector2I(32,32),16);
   if(field[(16*32+16)*4+2]==0) throw new Exception("Opening did not admit sky");
   world=new Jogo25D.Light.LogicalLightWorld(0,"terrain-depth-test",1,false);
   for(int y=0;y<32;y++) for(int x=8;x<24;x++) world.SetTerrain(x,y,0);
   var original=Jogo25D.Light.SkyAccessField.Build(world,Vector2I.Zero,new Vector2I(32,32),3);
   for(int step=1;step<=4;step++) {
     float actual=original[(16*32+7+step)*4+2]/255f;
     float expected=Mathf.Pow(Jogo25D.Constants.LightingConstants.SOLID_FALLOFF,step);
     if(Math.Abs(actual-expected)>1f/255f) throw new Exception("Default terrain depth changed original exponential profile");
   }
   var shallow=Jogo25D.Light.SkyAccessField.Build(world,Vector2I.Zero,new Vector2I(32,32),2);
   var deep=Jogo25D.Light.SkyAccessField.Build(world,Vector2I.Zero,new Vector2I(32,32),12);
   if(deep[(16*32+12)*4+2]<=shallow[(16*32+12)*4+2]) throw new Exception("Terrain depth did not change penetration");
   GD.Print($"ENVIRONMENT COMPOSITION PASS: unshadowed={unshadowed}, opaque={opaque}, open={open}, closed={closed}, window={window}; influence leaves unshadowed light unchanged");GetTree().Quit();
  } catch(Exception e) { GD.PushError(e.ToString());GetTree().Quit(1); }
 }
}
