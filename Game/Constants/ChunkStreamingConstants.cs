namespace Jogo25D.Constants
{
    public static class ChunkStreamingConstants
    {
        public const int CHUNK_SIZE = 32;
        public const int LOAD_RADIUS_CHUNKS = 2;
        public const int UNLOAD_RADIUS_CHUNKS = 4;
        public const int MAX_CHUNK_LOADS_PER_TICK = 2;
        public const float EVALUATE_INTERVAL_SECONDS = 0.75f;
        public const string OVERWORLD_ID = "overworld";
        public const string UPSIDEDOWN_ID = "upsidedown";
        public const string PROCEDURAL_LAYER_NAME = "ProceduralTiles";
        public const string PROCEDURAL_EDGE_FILL_RIGHT_LAYER_NAME = "ProceduralEdgeFillRight";
        public const string PROCEDURAL_EDGE_FILL_LEFT_LAYER_NAME = "ProceduralEdgeFillLeft";
        public const string PROCEDURAL_EDGE_FILL_TOP_LAYER_NAME = "ProceduralEdgeFillTop";
        public const string PROCEDURAL_EDGE_FILL_BOTTOM_LAYER_NAME = "ProceduralEdgeFillBottom";
        public const string PROCEDURAL_BORDER_CAP_LAYER_NAME = "ProceduralBorderCap";
    }
}
