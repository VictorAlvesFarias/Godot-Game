namespace Jogo25D.Constants
{
    public static class LightingConstants
    {
        public const float SOLID_FALLOFF = 0.5f;
        public const float AIR_LIGHT_LOSS = 1f / 24f;
        public const float MIN_LIGHT_THRESHOLD = 0.02f;
        public const float AMBIENT_MIN = 0.04f;
        public const int CHUNK_PADDING = 24;
        public const int MAX_CHUNK_REBUILDS_PER_FRAME = 2;
        public const float LIGHT_RADIUS_REFERENCE = 15f;
        public const ulong EDITOR_TILE_POLL_INTERVAL_MSEC = 200;
    }
}
