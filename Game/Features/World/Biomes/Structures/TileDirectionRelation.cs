using Godot;

namespace Jogo25D.Biomes
{
    [GlobalClass]
    public partial class TileDirectionRelation : Resource
    {
        private const string DirectionHint =
            nameof(TileSet.CellNeighbor.RightSide) + ":0," +
            nameof(TileSet.CellNeighbor.RightCorner) + ":1," +
            nameof(TileSet.CellNeighbor.BottomRightSide) + ":2," +
            nameof(TileSet.CellNeighbor.BottomRightCorner) + ":3," +
            nameof(TileSet.CellNeighbor.BottomSide) + ":4," +
            nameof(TileSet.CellNeighbor.BottomCorner) + ":5," +
            nameof(TileSet.CellNeighbor.BottomLeftSide) + ":6," +
            nameof(TileSet.CellNeighbor.BottomLeftCorner) + ":7," +
            nameof(TileSet.CellNeighbor.LeftSide) + ":8," +
            nameof(TileSet.CellNeighbor.LeftCorner) + ":9," +
            nameof(TileSet.CellNeighbor.TopLeftSide) + ":10," +
            nameof(TileSet.CellNeighbor.TopLeftCorner) + ":11," +
            nameof(TileSet.CellNeighbor.TopSide) + ":12," +
            nameof(TileSet.CellNeighbor.TopCorner) + ":13," +
            nameof(TileSet.CellNeighbor.TopRightSide) + ":14," +
            nameof(TileSet.CellNeighbor.TopRightCorner) + ":15";

        [Export] public int TerrainSet { get; set; }
        [Export] public Vector2I AtlasCoord { get; set; }

        [Export(PropertyHint.Enum, DirectionHint)]
        public Godot.Collections.Array<TileSet.CellNeighbor> Directions { get; set; } = new();
    }
}
