namespace Jogo25D.Constants
{
    public static class LightingConstants
    {
        public const float SOLID_FALLOFF = 0.5f;
        public const float AIR_FALLOFF = 0.85f;
        public const float MIN_LIGHT_THRESHOLD = 0.02f;
        public const float AMBIENT_MIN = 0.04f;

        public const int CHUNK_PADDING = 24;
        public const int MAX_CHUNK_REBUILDS_PER_FRAME = 2;
    }
}
