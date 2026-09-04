using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Light
{
    /// <summary>De onde o mapa de luz tira o angulo do sol.</summary>
    public enum SunAngleSource
    {
        /// <summary>O angulo fixo do inspetor.</summary>
        FixedAngle,

        /// <summary>A rotacao do proprio no, girada pela alca do editor.</summary>
        NodeRotation,
    }

    // Os ajustes do mapa de luz, num arquivo so. O no LightMap2D nao guarda ajuste nenhum: ele
    // aponta para um destes. Assim as duas dimensoes leem o mesmo .tres e calibrar e mexer num
    // lugar, em vez de repetir valor em cada cena e elas divergirem sem ninguem notar.
    //
    // Duplicar o .tres e apontar uma dimensao para a copia e o jeito de ter ajuste proprio numa
    // delas - subterraneo com outra luz, por exemplo.
    //
    // Cada grupo e um efeito independente, com o proprio interruptor. Desligar um efeito nao so
    // some com ele na tela: o passo dele deixa de rodar, entao tambem sai do custo.
    [GlobalClass]
    public partial class LightMapData : Resource
    {
        #region Sol

        /// <summary>
        /// Se o angulo do sol vem do numero abaixo ou da rotacao do proprio no. Rege a direcao das
        /// duas sombras projetadas; sem nenhuma delas ligada, nao tem efeito.
        /// </summary>
        [ExportGroup("Sol")]
        [Export] public SunAngleSource AngleSource { get; set; } = SunAngleSource.FixedAngle;

        /// <summary>
        /// O angulo do sol em graus. ZERO APONTA PARA BAIXO - sol a pino, sombra reta. Negativo
        /// deita a sombra para um lado, positivo para o outro. Perto de zero a inclinacao e quase
        /// nula e a sombra cai dentro do proprio bloco: nesse angulo nao ha projecao para ver, e
        /// isso nao e defeito.
        /// </summary>
        [Export(PropertyHint.Range, "-89,89,0.5")] public float SunAngleDegrees { get; set; } = -35f;

        /// <summary>
        /// Teto da inclinacao, em celulas que a sombra anda de lado a cada linha que desce. Na
        /// pratica, o comprimento maximo da sombra deitada. Sol rasante deitaria a sombra ate o
        /// outro lado do mundo, e a janela de calculo nao tem esse alcance.
        /// </summary>
        [Export(PropertyHint.Range, "0,8,0.05")] public float MaxSunSlope { get; set; } = LightMapConstants.MAX_SUN_SLOPE;

        /// <summary>
        /// Altura minima do sol para a sombra deitar, medida como o componente Y da direcao da luz
        /// (0 a 1). Abaixo disso a sombra volta a cair reta, porque uma sombra rasante nao caberia
        /// na janela.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float MinSunHeight { get; set; } = LightMapConstants.MIN_SUN_HEIGHT;

        #endregion

        #region Luz do ceu (difusa)

        /// <summary>
        /// O funcionamento base: a luz entra pelo topo de cada coluna e se espalha perdendo
        /// intensidade a cada celula. E o que da caverna escura, degrade na boca da caverna e
        /// escuridao que cresce com a profundidade. Desligado, o terreno fica todo em brilho cheio
        /// e so as sombras projetadas aparecem.
        ///
        /// Ela nunca escurece o AR - so o que tem materia. Escurecendo o ar, a celula embaixo da
        /// copa perderia luz por nao enxergar o ceu, e o resultado seria uma coluna escura reta
        /// descendo da arvore, que nao e sombra de nada: a difusa nao tem direcao.
        /// </summary>
        [ExportGroup("Luz do ceu (difusa)")]
        [Export] public bool DiffuseEnabled { get; set; } = true;

        /// <summary>
        /// Custo de atravessar um bloco. Quanto maior, mais rapido o terreno escurece com a
        /// profundidade: com custo 2 e nivel maximo 24, a luz morre 12 blocos abaixo da
        /// superficie. A folhagem tem custo proprio e menor, e por isso copa de arvore filtra a
        /// luz em vez de cortar.
        /// </summary>
        [Export(PropertyHint.Range, "1,12,1")] public int SolidCost { get; set; } = LightMapConstants.COST_SOLID;

        /// <summary>
        /// Piso de luminosidade: o brilho de uma celula que nao recebe luz nenhuma. Sem ele o
        /// fundo do mundo fica preto absoluto e nada e legivel.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float MinBrightness { get; set; } = LightMapConstants.MIN_BRIGHTNESS;

        /// <summary>
        /// A escala inteira em que a luz e contada. Junto com o custo do bloco decide ate onde ela
        /// chega. Mexer aqui reescala todos os custos de uma vez - em geral e melhor ajustar o
        /// custo do bloco.
        /// </summary>
        [Export(PropertyHint.Range, "4,64,1")] public int MaxLightLevel { get; set; } = LightMapConstants.MAX_LEVEL;

        #endregion

        #region Sombra projetada

        /// <summary>
        /// Quanto de luz uma celula de materia ABSORVE, como fracao do que chegou nela. O que
        /// sobra segue em frente, entao a sombra escurece a cada celula atravessada sem nunca
        /// travar - e isso que faz a sombra acompanhar a densidade da copa trecho a trecho.
        /// </summary>
        [ExportGroup("Sombra projetada")]
        [Export(PropertyHint.Range, "0,1,0.01")] public float SunBlockStrength { get; set; } = LightMapConstants.SUN_BLOCK_STRENGTH;

        /// <summary>
        /// Abertura do cone de raios, em graus - na pratica, quao macia e a borda da sombra.
        ///
        /// Com 0 e um raio so, e a borda fica binaria: o pixel bate ou nao bate no oclusor, e a
        /// transicao acontece em um pixel, seguindo o degrau do bloco. Abrindo o cone, os raios
        /// divergem com a distancia - perto do oclusor concordam e a borda fica nitida, longe
        /// discordam e ela abre sozinha, que e o que uma fonte de tamanho real faz.
        ///
        /// Custa proporcionalmente: sao cinco raios em vez de um sempre que passa de 0.
        /// </summary>
        [Export(PropertyHint.Range, "0,20,0.5")] public float ShadowSoftness { get; set; } = LightMapConstants.SHADOW_SOFTNESS_DEGREES;

        /// <summary>
        /// Quanto a luz do ambiente enfraquece a sombra.
        ///
        /// Sombra e a razao entre o sol direto e a luz que chega de todo lado. Num lugar ABERTO ha
        /// muita luz do ceu preenchendo a sombra, e ela fica fraca; num lugar FECHADO - dentro de
        /// uma caverna, sob a copa - sobra pouca luz para preencher, e ela fica cheia.
        ///
        /// 0 ignora o ambiente: a sombra tem a mesma forca em qualquer lugar. 1 apaga a sombra por
        /// completo a ceu aberto.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float AmbientInfluence { get; set; } = LightMapConstants.AMBIENT_INFLUENCE;

        /// <summary>
        /// A sombra que a arvore ou a construcao deixa no chao e nos blocos.
        /// </summary>
        [ExportSubgroup("No terreno")]
        [Export] public bool TerrainShadowEnabled { get; set; } = true;

        /// <summary>
        /// Quanto ela escurece, no maximo: 1 nao escurece nada, perto de 0 vira um borrao preto.
        /// IGNORADO quando a dureza da iluminacao do objeto esta ligada.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float ShadowFloor { get; set; } = LightMapConstants.SHADOW_FLOOR;

        /// <summary>
        /// Quantas celulas ela penetra no terreno, desvanecendo ate sumir. O raio do sol morre no
        /// primeiro bloco, entao abaixo da superficie tudo fica em zero e a coluna sombreada vale
        /// o mesmo que a iluminada - sem diferenca nao ha sombra para ver. Medido numa torre, tres
        /// linhas abaixo da superficie o contraste era 0,00; com 6 celulas vai para 0,19 e a faixa
        /// passa de 1 para 5 celulas. Zero desliga o arrasto.
        ///
        /// So a SOMBRA e levada para baixo: uma coluna em pleno sol continua intacta em qualquer
        /// profundidade. Quem escurece por profundidade e a difusa, nao esta sombra.
        ///
        /// A faixa segue o angulo do sol, e nao a coluna - ela e a continuacao da sombra do ar
        /// para dentro do chao, no mesmo desenho.
        /// </summary>
        [Export(PropertyHint.Range, "0,32,1")] public int ShadowDepth { get; set; } = LightMapConstants.SHADOW_DEPTH;

        /// <summary>
        /// O volume de sombra: a regiao de AR que o sol nao alcanca. E o que faz uma entidade
        /// parada debaixo da arvore escurecer junto - o overlay multiplica tudo que estiver
        /// embaixo dele, player incluso, sem o player precisar saber que o sistema existe.
        ///
        /// O preco de ligar e que o fundo do ceu tambem escurece nessa faixa, porque o overlay nao
        /// distingue o que esta atras dele.
        /// </summary>
        [ExportSubgroup("No ar")]
        [Export] public bool AirShadowEnabled { get; set; } = true;

        /// <summary>
        /// Peso da sombra no ar. O ar mostra a luz que chega nele, direto: 1 mostra a transmissao
        /// pura e valores menores clareiam tudo proporcionalmente. IGNORADO quando a dureza da
        /// iluminacao do objeto esta ligada.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float AirShadowStrength { get; set; } = LightMapConstants.AIR_SHADOW_STRENGTH;

        #endregion

        #region Depuracao

        /// <summary>
        /// Mostra o mapa cru em tons de cinza por cima da cena, bom para ler valor. Desligado, ele
        /// multiplica a cena, que e como fica em jogo.
        /// </summary>
        [ExportGroup("Depuracao")]
        [Export] public bool ShowRawMap { get; set; } = false;

        #endregion
    }
}
