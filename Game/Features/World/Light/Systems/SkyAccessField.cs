using Godot;
using Jogo25D.Constants;
using System.Collections.Generic;
namespace Jogo25D.Light;

internal static class SkyAccessField
{
    // Air transport cannot cross terrain. Terrain receives light from adjacent
    // lit air, but never becomes an entrance into another air compartment.
    public static byte[] Build(LogicalLightWorld world,Vector2I origin,Vector2I size,float depth)
    {
        int w=size.X,h=size.Y,n=w*h;
        var data=new byte[n*4];var light=new float[n];
        var queue=new PriorityQueue<int,float>();
        for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
            int i=y*w+x;data[i*4]=world.Opacity(origin.X+x,origin.Y+y);
            data[i*4+1]=world.HasBackground(origin.X+x,origin.Y+y)?(byte)255:(byte)0;
            data[i*4+3]=255;
            if(data[i*4]==0 && data[i*4+1]==0) { light[i]=1;queue.Enqueue(i,-1); }
        }
        void Spread(bool solids) {
            while(queue.TryDequeue(out int i,out float priority)) {
                if(-priority<light[i]-.00001f) continue;
                int x=i%w,y=i/w;
                for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++) {
                    if(dx==0 && dy==0) continue;
                    int nx=x+dx,ny=y+dy;
                    if(nx<0||ny<0||nx>=w||ny>=h) continue;
                    int j=ny*w+nx;bool solid=data[j*4]>0;
                    if(solid!=solids) continue;
                    bool diagonal=dx!=0 && dy!=0;
                    if(!solids && diagonal && (data[(y*w+nx)*4]>0 || data[(ny*w+x)*4]>0)) continue;
                    float distance=diagonal?1.41421356f:1f;
                    // Preserve the original exponential terrain profile. Depth
                    // scales travel distance; at the default 3 it is exactly the
                    // previous SOLID_FALLOFF per tile, including the surface step.
                    float next=solids
                        ? light[i]*Mathf.Pow(LightingConstants.SOLID_FALLOFF,distance*3f/Mathf.Max(.25f,depth))
                        : light[i]-LightingConstants.AIR_LIGHT_LOSS*distance;
                    if(next<LightingConstants.MIN_LIGHT_THRESHOLD) continue;
                    if(next>light[j]) { light[j]=next;queue.Enqueue(j,-next); }
                }
            }
        }
        Spread(false);
        for(int i=0;i<n;i++) if(data[i*4]==0 && light[i]>0) queue.Enqueue(i,-light[i]);
        Spread(true);
        for(int i=0;i<n;i++) {
            float value=light[i];
            data[i*4+2]=(byte)Mathf.RoundToInt(Mathf.Clamp(value,0,1)*255);
        }
        return data;
    }
}
