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

        // Se setado, o bloco nao usa o terrain_set do bioma de chao (comportamento padrao, ex:
        // "grass") - usa ESSE terrain_set fixo, na layer Base, em vez de virar chao do bioma na
        // Texture (ex: tronco/copa de arvore, terrain_set 6/7).
        public int? TerrainSet { get; init; }

        #endregion
    }
}
