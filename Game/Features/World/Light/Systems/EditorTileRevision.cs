using Godot;
using Jogo25D.Constants;
using System;
using System.Runtime.CompilerServices;

namespace Jogo25D.Light
{
    internal static class EditorTileRevision
    {
        #region Dinamic properties

        private static readonly ConditionalWeakTable<TileMapLayer, State> States = new();

        #endregion

        #region Core - Consulta

        public static long Get(TileMapLayer layer)
        {
            var state = States.GetOrCreateValue(layer);
            var now = Time.GetTicksMsec();

            if (now < state.Next)
            {
                return state.Revision;
            }

            state.Next = now + LightingConstants.EDITOR_TILE_POLL_INTERVAL_MSEC;

            var data = layer.TileMapData;

            if (state.Data == null || !data.AsSpan().SequenceEqual(state.Data))
            {
                state.Data = data;
                state.Revision++;
            }

            return state.Revision;
        }

        #endregion

        #region Types

        private sealed class State
        {
            public byte[] Data;
            public ulong Next;
            public long Revision;
        }

        #endregion
    }
}
