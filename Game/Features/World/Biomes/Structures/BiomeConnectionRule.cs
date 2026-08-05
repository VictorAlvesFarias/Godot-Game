using Godot;

namespace Jogo25D.Biomes
{
    // Um par de biomas que se conectam entre si pra fins de autotile (sem fronteira crua entre
    // eles) - editavel no Inspector do Godot, dentro de BiomeConnectionGraph.Connections. Mesmo
    // padrao do Tilesetter4Free: cada terreno guarda uma lista de outros terrenos com quem se
    // funde.
    [GlobalClass]
    public partial class BiomeConnectionRule : Resource
    {
        [Export] public BiomeType LayerA { get; set; }
        [Export] public BiomeType LayerB { get; set; }
    }
}
