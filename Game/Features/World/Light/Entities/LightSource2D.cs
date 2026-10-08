using Godot;
using Jogo25D.Core;
using Jogo25D.Dimensions;

namespace Jogo25D.Light
{
    [Tool, GlobalClass]
    public partial class LightSource2D : Node2D
    {
        #region Dinamic properties

        [Export]
        public bool Enabled { get; set; } = true;

        [Export]
        public Color LightColor { get; set; } = new(1f, .65f, .3f);

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float Energy { get; set; } = 1;

        private Node _level;
        private TileMapLayer _grid;
        private Vector2I _cell;
        private bool _registered;

        #endregion

        #region Godot implementation

        public override void _Process(double delta)
        {
            var level = FindLevel();

            if (level != _level)
            {
                Unregister();

                _level = level;
                _grid = null;
            }

            if (level == null)
            {
                return;
            }

            _grid ??= level.GetNodeOrNull<TileMapLayer>("Compose") ?? level.GetNodeOrNull<TileMapLayer>("Base");

            if (!IsInstanceValid(_grid))
            {
                _grid = null;

                return;
            }

            if (!Enabled || Energy <= 0 || !IsVisibleInTree())
            {
                Unregister();

                return;
            }

            Register(level);
        }

        public override void _ExitTree()
        {
            Unregister();

            _level = null;
            _grid = null;
        }

        #endregion

        #region Core - Registro

        private void Register(Node level)
        {
            var cell = _grid.LocalToMap(_grid.ToLocal(GlobalPosition));
            var color = new Color(
                Mathf.Clamp(LightColor.R * Energy, 0, 1),
                Mathf.Clamp(LightColor.G * Energy, 0, 1),
                Mathf.Clamp(LightColor.B * Energy, 0, 1),
                1);

            if (!SceneLightSources.Set(level, GetInstanceId(), cell, color))
            {
                return;
            }

            if (_registered)
            {
                Notify(_cell);
            }

            _cell = cell;
            _registered = true;

            Notify(cell);
        }

        private void Unregister()
        {
            if (_registered && SceneLightSources.Remove(_level, GetInstanceId()))
            {
                Notify(_cell);
            }

            _registered = false;
        }

        private void Notify(Vector2I cell)
        {
            if (!Engine.IsEditorHint())
            {
                _level?.GetNodeOrNull<LightMap2D>("LightMap")?.OnCellChanged(cell);
            }
        }

        #endregion

        #region Utils

        private Node FindLevel()
        {
            for (var parent = GetParent(); parent != null; parent = parent.GetParent())
            {
                if (parent.GetNodeOrNull<LightMap2D>("LightMap") != null || parent is Dimension)
                {
                    return parent;
                }
            }

            return null;
        }

        #endregion
    }
}
