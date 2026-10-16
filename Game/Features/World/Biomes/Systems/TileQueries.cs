using Godot;
using System.Collections.Concurrent;

namespace Jogo25D.Biomes
{
    // O que um bloco e, lido do proprio tile: tem bloco (qualquer tile), colide (forma de colisao na camada de fisica 0)
    // e bloqueia luz (forma de oclusao na camada 0). As consultas de duas camadas olham o terreno (Compose) e o Base.
    // O resultado por tile fica em cache: a forma de um tile nao muda durante o jogo.
    public static class TileQueries
    {
        #region Dinamic properties

        private const int ShapeLayer = 0;

        private static readonly ConcurrentDictionary<(ulong TileSet, int Source, Vector2I Atlas, int Alternative), (bool Collides, bool Occludes)> _shapes = new();

        #endregion

        #region Core - Celula

        public static bool HasBlock(TileMapLayer layer, Vector2I cell)
        {
            return layer != null && layer.GetCellSourceId(cell) != -1;
        }

        public static bool HasBlock(TileMapLayer layer, TileMapLayer baseLayer, Vector2I cell)
        {
            return HasBlock(layer, cell) || HasBlock(baseLayer, cell);
        }

        public static bool Collides(TileMapLayer layer, Vector2I cell)
        {
            return ShapeAt(layer, cell).Collides;
        }

        public static bool Collides(TileMapLayer layer, TileMapLayer baseLayer, Vector2I cell)
        {
            return Collides(layer, cell) || Collides(baseLayer, cell);
        }

        public static bool Occludes(TileMapLayer layer, Vector2I cell)
        {
            return ShapeAt(layer, cell).Occludes;
        }

        public static bool Occludes(TileMapLayer layer, TileMapLayer baseLayer, Vector2I cell)
        {
            return Occludes(layer, cell) || Occludes(baseLayer, cell);
        }

        #endregion

        #region Core - Tile

        // Forma de um tile do tileset (sem precisar de uma celula pintada).
        public static (bool Collides, bool Occludes) ShapeOf(TileSet tileSet, int sourceId, Vector2I atlasCoord, int alternative = 0)
        {
            if (tileSet == null || sourceId < 0)
            {
                return (false, false);
            }

            var key = (tileSet.GetInstanceId(), sourceId, atlasCoord, alternative);

            if (_shapes.TryGetValue(key, out var shape))
            {
                return shape;
            }

            var data = (tileSet.GetSource(sourceId) as TileSetAtlasSource)?.GetTileData(atlasCoord, alternative);

            shape = data == null
                ? (false, false)
                : (tileSet.GetPhysicsLayersCount() > ShapeLayer && data.GetCollisionPolygonsCount(ShapeLayer) > 0,
                   tileSet.GetOcclusionLayersCount() > ShapeLayer && data.GetOccluderPolygonsCount(ShapeLayer) > 0);

            _shapes[key] = shape;

            return shape;
        }

        private static (bool Collides, bool Occludes) ShapeAt(TileMapLayer layer, Vector2I cell)
        {
            if (layer == null)
            {
                return (false, false);
            }

            var sourceId = layer.GetCellSourceId(cell);

            return sourceId == -1
                ? (false, false)
                : ShapeOf(layer.TileSet, sourceId, layer.GetCellAtlasCoords(cell), layer.GetCellAlternativeTile(cell));
        }

        #endregion
    }
}
