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

        // Celulas a mais de cada lado da area visivel. Serve para a luz que vem de fora da tela
        // ja chegar propagada na borda, em vez de a borda inteira nascer escura.
        public const int WINDOW_MARGIN = 12;

        // --- Sombra projetada ---

        // Quanto de luz uma celula de materia ABSORVE, como fracao do que chegou nela. O raio e
        // multiplicado, nao descontado: com desconto fixo ele trava em zero e, a partir de umas
        // poucas celulas, copa grossa e copa fina projetam a mesma sombra.
        public const float SUN_BLOCK_STRENGTH = 0.92f;

        // Teto da inclinacao, em celulas por linha: o comprimento maximo da sombra deitada.
        public const float MAX_SUN_SLOPE = 1.6f;

        // Altura minima do sol, como componente Y da direcao. Abaixo disso a sombra cai reta.
        public const float MIN_SUN_HEIGHT = 0.2f;

        // Quanto a sombra no terreno escurece, no maximo.
        public const float SHADOW_FLOOR = 0.35f;

        // Quantas celulas ela penetra no terreno.
        public const int SHADOW_DEPTH = 6;

        // Opacidade da sombra no ar: 1 e preto absoluto, 0 e invisivel. Vira o piso do feixe no
        // shader - o valor do pixel onde a transmissao e zero.
        public const float AIR_SHADOW_OPACITY = 0.6f;

        // Sub-colunas por celula na varredura do sol, e o teto delas.
        public const int SOLAR_SUBSAMPLES = 4;
        public const int SOLAR_SUBSAMPLES_MAX = 32;

        // Abaixo desta transmissao o raio para: o que sobra nao muda mais o pixel.
        public const float RAY_CUTOFF = 0.02f;

        // De quanto a inclinacao do sol precisa mudar para o mapa ser refeito.
        public const float SUN_SLOPE_STEP = 0.03f;

        // Teto da meia-abertura do cone, em graus. Acima disto a sombra deixa de parecer com o
        // objeto: com 10 graus, a sombra de uma copa de 16 celulas vira 26 de largura a trinta
        // celulas de distancia. Referencia: o sol de verdade tem raio angular de 0.27 grau.
        public const float PENUMBRA_MAX_DEGREES = 10f;

        // Distancia da fonte de luz, de 0 a 1. Ver a propriedade Penumbra.
        public const float PENUMBRA = 0.95f;

        // Quanto a luz do ambiente enfraquece a sombra. Lugar aberto tem muita luz vinda de todo
        // lado, que preenche a sombra; lugar fechado nao tem, e a sombra fica cheia.
        public const float AMBIENT_INFLUENCE = 0.5f;

        // O shader que desenha a sombra projetada, por fragmento.
        public const string SHADER_PATH = "res://Assets/Shaders/light_map.gdshader";

        public const string OVERLAY_NODE_NAME = "LightMapOverlay";

        // Acima da cena e abaixo da UI, que vive em CanvasLayer proprio.
        public const int OVERLAY_Z_INDEX = 900;
    }
}
