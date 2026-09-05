using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Light
{
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

        [ExportGroup("Sol")]
        /// <summary>
        /// O angulo do sol em graus, volta inteira. ZERO APONTA PARA BAIXO - sol a pino, sombra
        /// reta. Perto de zero a sombra cai dentro do proprio bloco: nesse angulo nao ha projecao
        /// para ver, e isso nao e defeito.
        ///
        ///     0    sol a pino, sombra reta para baixo
        ///   +-90   sol deitado no horizonte, sombra deitada
        ///   +-180  sol POR BAIXO do mundo, sombra sobe
        ///
        /// Ja foi limitado a +-89 porque a direcao era achatada numa inclinacao X/Y com Y fixo em
        /// 1, que so representa luz indo para baixo e explode quando Y tende a zero. Hoje o vetor
        /// vai cru para o shader, e o DDA dele anda em qualquer direcao.
        /// </summary>
        [Export(PropertyHint.Range, "-180,180,0.5")] public float SunAngleDegrees { get; set; } = -35f;

        #endregion

        #region Sombra projetada no ar

        /// <summary>
        /// Quanto de luz uma celula de materia ABSORVE, como fracao do que chegou nela. O que
        /// sobra segue em frente, entao a sombra escurece a cada celula atravessada sem nunca
        /// travar - e isso que faz a sombra acompanhar a densidade da copa trecho a trecho.
        /// </summary>
        [ExportGroup("Sombra projetada no ar")]
        [Export(PropertyHint.Range, "0,1,0.01")] public float SunBlockStrength { get; set; } = LightMapConstants.SUN_BLOCK_STRENGTH;

        /// <summary>
        /// A DISTANCIA da fonte de luz, de 0 a 1. E o que abre a faixa de penumbra.
        ///
        ///     0.00  fonte encostada no objeto  -> faixa maxima, a sombra derrete
        ///     0.50  meio caminho
        ///     0.95  fonte quase no infinito    -> faixa de meia celula a trinta de distancia
        ///     1.00  fonte no infinito          -> borda seca, sem faixa
        ///
        /// Repare que 0 e o MAXIMO de penumbra e 1 e nenhuma - e assim porque e a distancia da
        /// fonte, nao a quantidade de efeito. Fonte perto ve um angulo grande e derrete a borda;
        /// fonte longe ve um angulo minimo e a borda fica seca. O sol e praticamente infinito:
        /// raio angular de 0.27 grau, e por isso sombra de sol tem borda quase nitida.
        ///
        /// A conta atras disso e geometria: largura = 2 * distancia_do_oclusor * tan(angulo), com
        /// tan(angulo) = tan(PENUMBRA_MAX_DEGREES) * (1 - este valor). Linear na largura, que e o
        /// que o olho ve.
        ///
        /// Era a meia-abertura em graus, de 0 a 20. Virou isto porque grau nao diz nada sobre o
        /// resultado, e metade da faixa antiga - acima de 10 graus - so produzia borrao.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float Penumbra { get; set; } = LightMapConstants.PENUMBRA;

        [Export] public bool AirShadowEnabled { get; set; } = true;

        /// <summary>
        /// A OPACIDADE da sombra: o quanto ela escurece onde o sol nao chega.
        ///
        ///     1.0  sombra opaca, preto absoluto
        ///     0.5  sombra pela metade
        ///     0.0  sombra invisivel, o mesmo que desligar
        ///
        /// No shader ela vira o piso do feixe - o valor que o pixel assume quando a transmissao e
        /// zero. Onde o sol chega o brilho e 1 e este numero nao tem efeito nenhum; ele so pinta o
        /// que ja esta na sombra.
        ///
        /// Lembrando que o overlay multiplica TUDO que estiver embaixo dele, inclusive o fundo do
        /// ceu: opacidade alta escurece a nuvem atras do feixe junto.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float AirShadowOpacity { get; set; } = LightMapConstants.AIR_SHADOW_OPACITY;

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
