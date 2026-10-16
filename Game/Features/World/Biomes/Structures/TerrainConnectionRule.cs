using Godot;

namespace Jogo25D.Biomes
{
    // Regra de conexao entre dois terrenos da mesma camada, em cada sentido: se os tiles de A se encaixam nos vizinhos de
    // B, e se os de B se encaixam nos de A. Directions limita os lados (vistos a partir de A; para B valem os opostos) em
    // que a regra vale: fora deles o vizinho conta como nao conectado. -1 num terreno = qualquer terreno.
    [Tool, GlobalClass]
    public partial class TerrainConnectionRule : Resource
    {
        public const int AnyTerrain = -1;

        [Export] public int TerrainSetA { get; set; }
        [Export] public int TerrainSetB { get; set; }
        [Export] public bool AConnectsToB { get; set; } = true;
        [Export] public bool BConnectsToA { get; set; } = true;

        [Export(PropertyHint.Enum)]
        public Godot.Collections.Array<TileSet.CellNeighbor> Directions { get; set; } = new();
    }
}
