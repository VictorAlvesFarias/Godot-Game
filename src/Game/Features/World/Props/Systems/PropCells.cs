using Godot;
using System.Collections.Generic;

namespace Jogo25D.Props
{
    // Celulas ocupadas pela forma dos props carregados, por dimensao. Montado em todos os peers (os props existem em
    // todos): o servidor decide colocacoes, e os clientes usam para o indicador e a mira.
    public static class PropCells
    {
        #region Properties

        private static readonly Dictionary<(string Dimension, Vector2I Cell), Prop> _props = new();

        #endregion

        #region Core - Consulta

        public static Prop At(string dimensionId, Vector2I cell)
        {
            return _props.TryGetValue((dimensionId, cell), out var prop) && GodotObject.IsInstanceValid(prop) ? prop : null;
        }

        #endregion

        #region Core - Escrita

        public static void Occupy(Prop prop, string dimensionId, IEnumerable<Vector2I> cells)
        {
            foreach (var cell in cells)
            {
                _props[(dimensionId, cell)] = prop;
            }
        }

        // Retira so as celulas que ainda sao deste prop.
        public static void Release(Prop prop, string dimensionId, IEnumerable<Vector2I> cells)
        {
            foreach (var cell in cells)
            {
                var key = (dimensionId, cell);

                if (_props.TryGetValue(key, out var current) && current == prop)
                {
                    _props.Remove(key);
                }
            }
        }

        #endregion
    }
}
