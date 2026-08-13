using Godot;

namespace Jogo25D.Props
{

    public class PropDefinition
    {
        #region Dinamic properties

        public string Id { get; init; }
        public string ScenePath { get; init; }

        #endregion

        #region Core - Spawn

        private PackedScene _scene;

        public virtual Node2D Spawn(Node2D parent, Vector2 position)
        {
            if (parent == null)
            {
                return null;
            }

            _scene ??= GD.Load<PackedScene>(ScenePath);

            if (_scene == null)
            {
                return null;
            }

            var instance = _scene.Instantiate<Node2D>();

            instance.Position = position;

            parent.AddChild(instance);

            return instance;
        }

        #endregion
    }
}
