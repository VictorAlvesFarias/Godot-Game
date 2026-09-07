namespace Jogo25D.Constants
{
    public static class LightMapConstants
    {
        public const float AMBIENT_INFLUENCE = 0.65f;
        public const float AIR_SHADOW_OPACITY = 0.85f;
        public const float PENUMBRA = 0.92f;
        // Meia abertura angular da fonte quando `Penumbra` esta em 0. E o unico controle fisico da
        // largura da penumbra: com 8 graus a rampa ficava mais estreita que um tile perto do
        // oclusor, e o degrau quadrado do tile aparecia inteiro.
        public const float PENUMBRA_MAX_DEGREES = 25f;
        // Ate onde a oclusao e considerada, em celulas. Nao e regulagem de aparencia: e o
        // orcamento de busca do shader.
        public const int SUN_RANGE_CELLS = 96;
        public const int SKY_RANGE_CELLS = 24;

        // Resolucao interna do passe por celula em cada eixo. E o que define se o contorno da
        // sombra pode ser uma reta ou so uma escada de tiles.
        public const int SUBDIVISIONS = 3;

        public const string SHADER_PATH = "res://Assets/Shaders/light_map.gdshader";
        public const string PRESENT_SHADER_PATH = "res://Assets/Shaders/light_map_present.gdshader";

        public const string VIEWPORT_NODE_NAME = "LightMapGpuPass";
        public const string PASS_NODE_NAME = "LightMapPass";
        public const string OVERLAY_NODE_NAME = "LightMapOverlay";

        public const int OVERLAY_Z_INDEX = 900;
    }
}
