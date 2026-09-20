using Godot;

namespace Jogo25D.Blocks
{
    public class BlockDefinition
    {
        #region Dinamic properties

        public bool IsBackground { get; init; }
        public string Id { get; init; }
        public string DropItemId { get; init; }
        public int SourceId { get; init; }
        public Vector2I AtlasCoord { get; init; }
        public int? TerrainSet { get; init; }
        public int LightRadius { get; init; }
        public Color LightColor { get; init; } = Colors.White;

        #endregion
    }
}
