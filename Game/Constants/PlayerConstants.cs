namespace Jogo25D.Constants
{
    public static class PlayerConstants
    {
        public const float STEP_UP_PROBE = 2f;
        public const float STEP_UP_MIN_REACH = 4f;

        // Nado: com pelo menos SWIM_IMMERSION do corpo na agua (celulas com altura >= SWIM_MIN_HEIGHT de 8).
        public const float SWIM_IMMERSION = 0.5f;
        public const int SWIM_MIN_HEIGHT = 4;
        public const float SWIM_GRAVITY_FACTOR = 0.25f;
        public const float SWIM_MAX_FALL_SPEED = 120f;
        public const float SWIM_SPEED_FACTOR = 0.6f;
        public const float SWIM_RISE_SPEED = 200f;
    }
}
