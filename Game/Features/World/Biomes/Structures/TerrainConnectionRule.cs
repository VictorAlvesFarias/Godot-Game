using Godot;

namespace Jogo25D.Biomes
{

    [GlobalClass]
    public partial class TerrainConnectionRule : Resource
    {
        [Export] public int TerrainSetA { get; set; }
        [Export] public int TerrainSetB { get; set; }
    }
}
