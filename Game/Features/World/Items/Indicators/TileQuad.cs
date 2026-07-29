using Godot;

namespace Jogo25D.Items.Indicators
{
    public static class TileQuad
    {
        public static Vector2[] Build(TileMapLayer layer)
        {
            var half = (Vector2)layer.TileSet.TileSize / 2f;

            return new[]
            {
                new Vector2(-half.X, -half.Y),
                new Vector2(half.X, -half.Y),
                new Vector2(half.X, half.Y),
                new Vector2(-half.X, half.Y),
            };
        }
    }
}
