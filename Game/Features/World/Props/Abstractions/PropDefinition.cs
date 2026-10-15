using Godot;

namespace Jogo25D.Props
{
    public class PropDefinition
    {
        #region Dinamic properties

        public string Id { get; init; }
        public string ScenePath { get; init; }

        // Item que coloca o prop e que cai quando ele e quebrado.
        public string ItemId { get; init; }

        // Estacao de criacao que o prop oferece (receitas com essa estacao), ou null.
        public string Station { get; init; }

        #endregion

        #region Core - Spawn

        public virtual Node2D Spawn(Node2D parent, Vector2 position)
        {
            if (parent == null)
            {
                return null;
            }

            var scene = GD.Load<PackedScene>(ScenePath);

            if (scene == null)
            {
                return null;
            }

            var instance = scene.Instantiate<Node2D>();

            instance.Position = position;

            parent.AddChild(instance);

            return instance;
        }

        #endregion
    }
}
