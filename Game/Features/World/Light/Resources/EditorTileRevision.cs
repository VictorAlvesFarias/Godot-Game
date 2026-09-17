using Godot;
using System;
using System.Runtime.CompilerServices;
namespace Jogo25D.Light;
// TileMapLayer.Changed does not cover every direct cell edit. Compare its packed
// data in one native call, shared by preview consumers, instead of reading every tile.
internal static class EditorTileRevision
{
    private sealed class State { public byte[] Data;public ulong Next;public long Revision; }
    private static readonly ConditionalWeakTable<TileMapLayer,State> States=new();
    public static long Get(TileMapLayer layer)
    {
        var state=States.GetOrCreateValue(layer);
        ulong now=Time.GetTicksMsec();
        if(now<state.Next) return state.Revision;
        state.Next=now+200;
        var data=layer.TileMapData;
        if(state.Data==null || !data.AsSpan().SequenceEqual(state.Data)) { state.Data=data;state.Revision++; }
        return state.Revision;
    }
}
