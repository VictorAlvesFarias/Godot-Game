using Godot;

namespace Jogo25D.Biomes
{
    // Opcoes de autotile de um terreno da camada. ConnectsToItself: tiles do proprio terreno contam como vizinhos ligados
    // (a tocha nao se apoia em outra tocha). SidePreference: sem tile para a combinacao exata de vizinhos, os lados sao
    // descartados do ultimo para o primeiro desta lista ate existir um tile.
    [Tool, GlobalClass]
    public partial class TerrainMatchOptions : Resource
    {
        [Export] public int TerrainSet { get; set; }
        [Export] public bool ConnectsToItself { get; set; } = true;

        [Export(PropertyHint.Enum)]
        public Godot.Collections.Array<TileSet.CellNeighbor> SidePreference { get; set; } = new();
    }
}
