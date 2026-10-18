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

        // Multiplica o tempo de quebra da ferramenta (1 = normal).
        public float BreakTimeScale { get; init; } = 1f;

        // So pode existir com apoio (BlockSupport): sem ele nao e colocado, e cai quando o perde.
        public bool NeedsSupport { get; init; }

        // Nao pode ser colocado na agua, e a agua que chega na celula o quebra (com drop).
        public bool BreaksInWater { get; init; }

        // Nivel minimo da ferramenta para quebrar o bloco.
        public int RequiredTier { get; init; } = 1;

        #endregion
    }
}
