using Godot;

namespace Jogo25D.Biomes
{
    [GlobalClass]
    public partial class TileDirectionRelation : Resource
    {
        [Export] 
        public int TerrainSet { get; set; }
        
        [Export] 
        public Vector2I AtlasCoord { get; set; }

        [Export(PropertyHint.Enum)]
        public Godot.Collections.Array<TileSet.CellNeighbor> Directions { get; set; } = new();
    }
}
