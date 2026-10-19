using Godot;
using Jogo25D.Dimensions;
using Jogo25D.Utils.Coordinates;
using System.Collections.Generic;

namespace Jogo25D.Items
{
    // Itens do chao por dimensao, em baldes por celula: acordar os apoiados numa celula, juntar pilhas e achar itens
    // perto do jogador sem varrer a cena.
    public static class WorldItemIndex
    {
        #region Properties

        private static readonly Dictionary<string, CellIndex<WorldItem>> _byDimension = new();
        private static readonly Dictionary<WorldItem, string> _dimensionOf = new();

        #endregion

        #region Core - Escrita

        // Coloca (ou move) o item na celula da posicao atual. A dimensao e lida ao entrar e guardada (trocar de dimensao
        // passa por sair e entrar na arvore).
        public static void Place(WorldItem item)
        {
            if (!_dimensionOf.TryGetValue(item, out var dimension))
            {
                dimension = Dimension.IdOf(item);
                _dimensionOf[item] = dimension;
            }

            For(dimension).Place(item, CellOf(item.GlobalPosition));
        }

        public static string DimensionOf(WorldItem item)
        {
            return _dimensionOf.TryGetValue(item, out var dimension) ? dimension : null;
        }

        public static void Remove(WorldItem item)
        {
            if (_dimensionOf.Remove(item, out var dimension) && _byDimension.TryGetValue(dimension, out var index))
            {
                index.Remove(item);
            }
        }

        public static void SetSupport(WorldItem item, IReadOnlyList<Vector2I> cells)
        {
            if (_dimensionOf.TryGetValue(item, out var dimension))
            {
                For(dimension).SetSupport(item, cells);
            }
        }

        public static void ClearSupport(WorldItem item)
        {
            if (_dimensionOf.TryGetValue(item, out var dimension) && _byDimension.TryGetValue(dimension, out var index))
            {
                index.ClearSupport(item);
            }
        }

        #endregion

        #region Core - Consulta

        public static List<WorldItem> SupportedBy(string dimension, Vector2I cell)
        {
            return _byDimension.TryGetValue(dimension, out var index) ? index.SupportedBy(cell) : new List<WorldItem>();
        }

        // Itens a ate `radius` (pixels) de `position`.
        public static List<WorldItem> Near(string dimension, Vector2 position, float radius)
        {
            var result = new List<WorldItem>();

            if (!_byDimension.TryGetValue(dimension, out var index))
            {
                return result;
            }

            var cells = Mathf.CeilToInt(radius / Dimension.TileSize);

            foreach (var item in index.Around(CellOf(position), cells))
            {
                if (GodotObject.IsInstanceValid(item) && item.GlobalPosition.DistanceTo(position) <= radius)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        public static WorldItem Nearest(string dimension, Vector2 position, float radius, System.Func<WorldItem, bool> accept = null)
        {
            return _byDimension.TryGetValue(dimension, out var index)
                ? index.Nearest(position, Dimension.TileSize, radius, item => item.GlobalPosition, item => GodotObject.IsInstanceValid(item) && (accept == null || accept(item)))
                : null;
        }

        public static Vector2I CellOf(Vector2 globalPosition)
        {
            return CoordinateUtilities.WorldToCell(globalPosition, Dimension.TileSize);
        }

        #endregion

        #region Helpers

        private static CellIndex<WorldItem> For(string dimension)
        {
            if (!_byDimension.TryGetValue(dimension, out var index))
            {
                _byDimension[dimension] = index = new CellIndex<WorldItem>();
            }

            return index;
        }

        #endregion
    }
}
