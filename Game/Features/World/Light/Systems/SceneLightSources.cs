using Godot;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
namespace Jogo25D.Light;

internal static class SceneLightSources
{
    private sealed class State { public long Revision; public readonly Dictionary<ulong,LightSource> Sources=new(); }
    private static readonly ConditionalWeakTable<Node,State> Levels=new();
    public static long Revision(Node level) => level!=null && Levels.TryGetValue(level,out var state)?state.Revision:0;
    public static bool Set(Node level,ulong id,Vector2I cell,Color color)
    {
        var state=Levels.GetOrCreateValue(level);
        if(state.Sources.TryGetValue(id,out var old) && old.Cell==cell && old.Color==color) return false;
        state.Sources[id]=new LightSource(cell,color);state.Revision++;return true;
    }
    public static bool Remove(Node level,ulong id)
    {
        if(level==null || !Levels.TryGetValue(level,out var state) || !state.Sources.Remove(id)) return false;
        state.Revision++;return true;
    }
    public static void Collect(Node level,Rect2I region,List<LightSource> sources)
    {
        if(level==null || !Levels.TryGetValue(level,out var state)) return;
        foreach(var source in state.Sources.Values) if(region.HasPoint(source.Cell)) sources.Add(source);
    }
}
