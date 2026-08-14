namespace Jogo25D.Constants
{
    public static class ChunkStreamingConstants
    {
        public const int CHUNK_SIZE = 32;
        public const int REFERENCE_TILE_SIZE = 32;
        public const int LOAD_RADIUS_CHUNKS = 2;
        public const int UNLOAD_RADIUS_CHUNKS = 4;
        public const int MAX_CHUNK_LOADS_PER_TICK = 2;
        public const float EVALUATE_INTERVAL_SECONDS = 0.75f;
        public const string OVERWORLD_ID = "overworld";
        public const string UPSIDEDOWN_ID = "upsidedown";
        public const string PROCEDURAL_LAYER_NAME = "Compose";
        public const string PROCEDURAL_BASE_LAYER_NAME = "Base";
    }
}
