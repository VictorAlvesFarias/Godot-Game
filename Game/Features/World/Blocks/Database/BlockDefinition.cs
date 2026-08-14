using Godot;

namespace Jogo25D.Blocks
{
    public class BlockDefinition
    {
        #region Dinamic properties

        public string Id { get; init; }
        public string DropItemId { get; init; }
        public int SourceId { get; init; }
        public Vector2I AtlasCoord { get; init; }

        public int? TerrainSet { get; init; }

        #endregion
    }
}
