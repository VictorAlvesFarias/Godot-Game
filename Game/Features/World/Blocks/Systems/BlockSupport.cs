using Godot;
using Jogo25D.Biomes;

namespace Jogo25D.Blocks
{
    // Apoio de blocos que nao se sustentam sozinhos (tocha): uma parede na propria celula, ou um bloco que colide
    // embaixo, a esquerda ou a direita.
    public static class BlockSupport
    {
        #region Dinamic properties

        private static readonly Vector2I[] SupportOffsets = { Vector2I.Down, Vector2I.Left, Vector2I.Right };

        // Celulas que um bloco em `cell` pode estar apoiando (o apoio delas e embaixo ou dos lados).
        public static readonly Vector2I[] DependentOffsets = { Vector2I.Up, Vector2I.Right, Vector2I.Left };

        #endregion

        #region Core - Apoio

        public static bool HasSupport(TileMapLayer layer, TileMapLayer baseLayer, TileMapLayer walls, Vector2I cell)
        {
            if (walls != null && walls.GetCellSourceId(cell) != -1)
            {
                return true;
            }

            foreach (var offset in SupportOffsets)
            {
                if (TileQueries.Collides(layer, baseLayer, cell + offset))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
