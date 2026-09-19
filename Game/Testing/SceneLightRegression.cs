using Godot;
using Jogo25D.Light;
using Jogo25D.Biomes;
using System;
namespace Jogo25D.Testing;
[Tool]
public partial class SceneLightRegression : Node
{
 public override async void _Ready() {
  if(Engine.IsEditorHint() && System.Environment.GetEnvironmentVariable("SCENE_LIGHT_TEST")!="1") return;
  try {
   var level=new Node2D();AddChild(level);
   var map=new LightMap2D { Name="LightMap" };level.AddChild(map);map.SetProcess(false);
   var grid=new TerrainLayer { Name="Compose",TileSet=new TileSet { TileSize=new Vector2I(16,16) } };level.AddChild(grid);grid.SetProcess(false);
   var fire=GD.Load<PackedScene>("res://Scenes/World/Props/Campfire.tscn").Instantiate<Node2D>();level.AddChild(fire);
   var source=fire.GetNode<LightSource2D>("Light");source.SetProcess(false);
   var region=new Rect2I(-16,-16,32,32);var index=LightSourceScanner.BuildLightEmittingBlockIndex();
   System.Collections.Generic.List<LightSource> Collect() { source._Process(0);return LightSourceScanner.CollectSources(grid,null,region,index,_=>false,false,_=>true); }
   var sources=Collect();if(sources.Count!=1) throw new Exception("Campfire is not registered in active collector");
   var cell=sources[0].Cell;
   long revision=SceneLightSources.Revision(level);Collect();
   if(SceneLightSources.Revision(level)!=revision) throw new Exception("Stationary source invalidates every frame");
   using var dispatcher=new LightPropagationDispatcher();
   using(var result=await dispatcher.ComputeTextureAsync(region,_=>false,sources)) {
    for(int i=0;i<3;i++) await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
    using var image=result.Texture.GetImage();var c=image.GetPixel(cell.X+16,cell.Y+16);
    if(c.R<.9 || c.R<=c.G || c.G<=c.B) throw new Exception("Campfire warm emission did not reach GPU output");
    if(image.GetPixel(cell.X+18,cell.Y+16).R<.5) throw new Exception("Campfire did not propagate into room");
   }
   source.Enabled=false;if(Collect().Count!=0) throw new Exception("Disabled campfire left a source");
   source.Enabled=true;source.Energy=.25f;source.LightColor=Colors.Blue;
   var changed=Collect();if(changed.Count!=1 || changed[0].Color.B<.24 || changed[0].Color.R!=0) throw new Exception("Energy/color did not update");
   fire.Position+=new Vector2(48,0);var moved=Collect();if(moved[0].Cell==cell) throw new Exception("Moving campfire did not move source");
   level.RemoveChild(fire);fire.QueueFree();
   if(LightSourceScanner.CollectSources(grid,null,region,index,_=>false,false,_=>true).Count!=0) throw new Exception("Removed campfire left stale light");
   var other=new Node2D();AddChild(other);var isolated=new System.Collections.Generic.List<LightSource>();SceneLightSources.Collect(other,region,isolated);
   if(isolated.Count!=0) throw new Exception("Light leaked between dimensions");
   GD.Print("SCENE LIGHT PASS: actual campfire, GPU emission, propagation, idle, toggle, energy, color, movement, removal, isolation");GetTree().Quit();
  } catch(Exception e) { GD.PushError(e.ToString());GetTree().Quit(1); }
 }
}
