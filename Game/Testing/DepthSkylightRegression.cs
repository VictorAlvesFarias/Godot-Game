using Godot;
using Jogo25D.Light;
using Jogo25D.Biomes;
using System;
namespace Jogo25D.Testing;
public partial class DepthSkylightRegression : Node
{
    public override async void _Ready()
    {
        try
        {
            using var layer=new TerrainLayer();
            var index=LightSourceScanner.BuildLightEmittingBlockIndex();
            var region=new Rect2I(0,0,32,32);
            bool Solid(Vector2I cell) => cell.Y==4 || cell.Y==24;
            bool closed=false,window=false;
            bool Wall(Vector2I cell) => closed && !(window && cell==new Vector2I(16,12));
            using var gpu=new LightPropagationDispatcher();
            foreach(var mode in new[]{0,1,2,1})
            {
                closed=mode!=0;window=mode==2;
                var sources=LightSourceScanner.CollectSources(layer,null,region,index,Solid,hasBackground:Wall);
                var cpu=LightPropagationSystem.Compute(region,Solid,sources);
                if(mode==0 && cpu[16,12].R<0.999f) throw new Exception("Roof darkened open background");
                if(mode==1 && cpu[16,12].R>0.001f) throw new Exception("Closed background retained daylight");
                if(mode==2 && cpu[16,20].R<0.65f) throw new Exception("Window daylight fades before illuminating the room");
                if(mode==2 && (cpu[16,12].R<0.999f || cpu[19,12].R<=0 || cpu[19,12].R>=1)) throw new Exception("Window did not propagate attenuated light");
                using var result=await gpu.ComputeTextureAsync(region,Solid,sources);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var image=result.Texture.GetImage();
                for(int y=0;y<32;y++) for(int x=0;x<32;x++)
                    if(Math.Abs(image.GetPixel(x,y).R-cpu[x,y].R)>0.005f) throw new Exception("GPU mismatch");
            }
            var off=LightSourceScanner.CollectSources(layer,null,region,index,Solid,false,Wall);
            if(off.Count!=0) throw new Exception("Disabled skylight still seeded");
            GD.Print("DEPTH SKY PASS: roof + open back, closed walls, window propagation, closing window, GPU parity");
            GetTree().Quit();
        }
        catch(Exception e) { GD.PushError(e.ToString());GetTree().Quit(1); }
    }
}
