using Godot;
using Jogo25D.Biomes;
using Jogo25D.Dimensions;
using System.Collections.Generic;

namespace Jogo25D.Props
{
    // Onde um prop cabe: todas as celulas da forma sem bloco e sem outro prop, e chao logo abaixo da celula mais baixa de
    // cada coluna da forma.
    public static class PropPlacement
    {
        #region Core - Regra

        public static bool CanPlace(Dimension dimension, string propId, Vector2I anchor)
        {
            var layer = dimension?.Layer;

            if (layer == null)
            {
                return false;
            }

            var baseLayer = dimension.BaseLayer;
            var lowest = new Dictionary<int, int>();

            foreach (var offset in PropFootprints.Of(propId))
            {
                var cell = anchor + offset;

                if (TileQueries.HasBlock(layer, baseLayer, cell) || PropCells.At(dimension.DimensionId, cell) != null)
                {
                    return false;
                }

                lowest[offset.X] = lowest.TryGetValue(offset.X, out var y) ? Mathf.Max(y, offset.Y) : offset.Y;
            }

            foreach (var (x, y) in lowest)
            {
                if (!TileQueries.Collides(layer, baseLayer, anchor + new Vector2I(x, y + 1)))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
