namespace Jogo25D.Constants
{
    // Mapa de luz por tile. Cada celula guarda um nivel de 0 a MAX_LEVEL; a luz do ceu entra por
    // cima e se propaga perdendo intensidade, mais ao atravessar bloco do que ar. E o que da o
    // degrade na boca da caverna e a escuridao que aumenta com a profundidade - coisas que a
    // sombra projetada nao faz, porque o oclusor do Godot e binario: bloqueia ou nao bloqueia.
    public static class LightMapConstants
    {
        public const int MAX_LEVEL = 24;

        // Quanto a luz perde ao atravessar uma celula. Ar quase nao consome; bloco solido consome
        // muito, entao poucos blocos de profundidade ja levam ao escuro. Folhagem fica no meio, e
        // por isso copa de arvore filtra a luz em vez de cortar.
        // Quanto a luz perde por celula de AR percorrida. Fracionario de proposito: com 1 e
        // orcamento 9, o ar come 1/9 da luz por celula, e a luz que entra por baixo de uma copa
        // larga morre antes de chegar no tronco - mesmo nao havendo nada entre ele e o ceu. Ar nao
        // deveria cobrar quase nada; quem cobra caro e bloco e folhagem.
        // O ar NAO absorve: quem escurece um ponto e a materia que tapa a vista dele para o
        // ceu, nao a distancia que a luz andou pelo vazio.
        public const int COST_FOLIAGE = 2;
        public const int COST_SOLID = 2;

        // Piso de luminosidade. Sem isso o fundo do mundo fica preto absoluto e nada e legivel.
        public const float MIN_BRIGHTNESS = 0.12f;

        // --- Sombra projetada ---

        // Influencia do ambiente na sombra, de 0 a 1. Ver a propriedade AmbientInfluence.
        public const float AMBIENT_INFLUENCE = 0.5f;

        // Opacidade da sombra no ar: 1 e preto absoluto, 0 e invisivel. Vira o piso do feixe no
        // shader - o valor do pixel onde a transmissao e zero.
        public const float AIR_SHADOW_OPACITY = 0.6f;

        // Teto da meia-abertura do cone, em graus. Acima disto a sombra deixa de parecer com o
        // objeto: com 10 graus, a sombra de uma copa de 16 celulas vira 26 de largura a trinta
        // celulas de distancia. Referencia: o sol de verdade tem raio angular de 0.27 grau.
        public const float PENUMBRA_MAX_DEGREES = 10f;

        // Distancia da fonte de luz, de 0 a 1. Ver a propriedade Penumbra.
        public const float PENUMBRA = 0.95f;

        // O shader que desenha a sombra projetada, por fragmento.
        public const string SHADER_PATH = "res://Assets/Shaders/light_map.gdshader";

        public const string OVERLAY_NODE_NAME = "LightMapOverlay";

        // Acima da cena e abaixo da UI, que vive em CanvasLayer proprio.
        public const int OVERLAY_Z_INDEX = 900;
    }
}
