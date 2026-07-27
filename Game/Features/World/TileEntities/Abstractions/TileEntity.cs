using Godot;
using Jogo25D.Characters;

namespace Jogo25D.TileEntities
{
    public abstract class TileEntity
    {
        public string TypeId { get; }
        public Vector2I Cell { get; }
        public Node2D World { get; }

        public Vector2 CellPosition { get; }

        protected TileEntity(string typeId, Vector2I cell, Node2D world, Vector2 cellPosition)
        {
            TypeId = typeId;
            Cell = cell;
            World = world;
            CellPosition = cellPosition;
        }

        public virtual string InteractPrompt => null;

        public virtual void OnReady() { }
        public virtual void OnPlayerEnter(Player player) { }
        public virtual void OnPlayerExit(Player player) { }

        public virtual void OnPlayerInteract(Player player) { }
    }
}
