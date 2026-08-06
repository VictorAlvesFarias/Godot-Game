using Godot;

namespace Jogo25D.Biomes
{
    // Um par de terrain_set (indices reais do TileSet, nao BiomeType) que se conectam entre si
    // pra fins de autotile - editavel no Inspector do Godot, direto na lista "Connections" de um
    // TerrainLayer. Cada TerrainLayer guarda a propria lista - nao existe regra compartilhada
    // entre camadas diferentes, cada uma sabe apenas quais dos SEUS terrenos se fundem entre si.
    [GlobalClass]
    public partial class TerrainConnectionRule : Resource
    {
        [Export] public int TerrainSetA { get; set; }
        [Export] public int TerrainSetB { get; set; }
    }
}
