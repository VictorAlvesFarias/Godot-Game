namespace Jogo25D.Constants
{
    public static class UiConstants
    {
        public const string CURSOR_PATH = "res://Assets/Textures/Ui/cursor.png";
        public const string CROSSHAIR_PATH = "res://Assets/Textures/Ui/crosshair.png";
        public const string POINTER_PATH = "res://Assets/Textures/Ui/pointer.png";

        // O hotspot do ponteiro e a ponta do indicador, no topo, e nao o canto da imagem.
        public const float POINTER_HOTSPOT_X = 9f;

        // A barra cresce com a vida maxima, mas a partir de uma base: sem isso um personagem
        // de 50 de vida ja nasce com uma barra atravessando a tela.
        public const float HEALTH_BAR_BASE_WIDTH = 160f;
        public const float HEALTH_BAR_PX_PER_HEALTH = 2.8f;

        // O retangulo de cargas tem largura fixa para tres digitos, entao acima disso
        // o valor nao e contabilizado no HUD.
        public const int MAX_CHARGES_SHOWN = 999;
    }
}
