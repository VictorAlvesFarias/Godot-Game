using Godot;

namespace Jogo25D.Biomes
{
    internal readonly struct TerrainTileMatch
    {
        public readonly int SourceId;
        public readonly Vector2I AtlasCoord;
        public readonly int AlternativeId;

        public TerrainTileMatch(int sourceId, Vector2I atlasCoord, int alternativeId)
        {
            SourceId = sourceId;
            AtlasCoord = atlasCoord;
            AlternativeId = alternativeId;
        }
    }
}
