using Godot;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Jogo25D.Light
{
    internal static class SceneLightSources
    {
        #region Dinamic properties

        private static readonly ConditionalWeakTable<Node, State> Levels = new();

        #endregion

        #region Core - Registro

        public static bool Set(Node level, ulong id, Vector2I cell, Color color)
        {
            var state = Levels.GetOrCreateValue(level);

            if (state.Sources.TryGetValue(id, out var current) && current.Cell == cell && current.Color == color)
            {
                return false;
            }

            state.Sources[id] = new LightSource(cell, color);
            state.Revision++;

            return true;
        }

        public static bool Remove(Node level, ulong id)
        {
            if (level == null || !Levels.TryGetValue(level, out var state) || !state.Sources.Remove(id))
            {
                return false;
            }

            state.Revision++;

            return true;
        }

        #endregion

        #region Core - Consulta

        public static long Revision(Node level)
        {
            if (level == null || !Levels.TryGetValue(level, out var state))
            {
                return 0;
            }

            return state.Revision;
        }

        public static void Collect(Node level, Rect2I region, List<LightSource> sources)
        {
            if (level == null || !Levels.TryGetValue(level, out var state))
            {
                return;
            }

            foreach (var source in state.Sources.Values)
            {
                if (region.HasPoint(source.Cell))
                {
                    sources.Add(source);
                }
            }
        }

        #endregion

        #region Types

        private sealed class State
        {
            public readonly Dictionary<ulong, LightSource> Sources = new();

            public long Revision;
        }

        #endregion
    }
}
