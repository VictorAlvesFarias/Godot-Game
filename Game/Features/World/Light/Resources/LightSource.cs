using Godot;

namespace Jogo25D.Light
{
    public readonly struct LightSource
    {
        #region Constructors

        public LightSource(Vector2I cell, Color color)
        {
            Cell = cell;
            Color = color;
        }

        #endregion

        #region Dinamic properties

        public readonly Vector2I Cell;
        public readonly Color Color;

        #endregion
    }
}
