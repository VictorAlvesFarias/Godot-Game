using Godot;
using Jogo25D.Constants;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    // Nucleo do mapa de luz: recebe uma grade de custos e devolve a imagem de brilho. Nao e Node,
    // nao le singleton e nao conhece dimensao, camera nem cena - por isso o mesmo calculo roda no
    // editor e em jogo, sem [Tool] nenhum aqui dentro. Quem o usa e o LightMap2D, o no autorado
    // em cada cena de dimensao.
    //
    // Faz a luz do ceu, e a pergunta que ela responde e QUANTO DE CEU CADA CELULA ENXERGA.
    //
    // Isto ja foi uma inundacao em fila, e a pergunta era outra: a quantos passos do ceu a celula
    // esta. Um campo de distancia cobra por TODO passo, inclusive os que atravessam ar vazio -
    // entao o tronco de uma arvore de copa larga ficava preto por estar dez celulas de vao aberto
    // longe da clareira, mesmo enxergando meio horizonte de ceu limpo dos dois lados. Nao havia
    // termo nenhum no calculo que representasse "esta celula ve o ceu".
    //
    // Sao duas contas, e de proposito:
    //
    //   AR      lanca um leque de raios para cima e acumula o custo SO da materia que atravessa.
    //           O ar e de graca. E o quanto de ceu aquele ponto enxerga.
    //
    //   MATERIA nao lanca raio nenhum. Ela e semeada pelo ar encostado nela e escurece para dentro,
    //           celula a celula. E o quao FUNDO dentro da materia o ponto esta.
    //
    // A segunda ja foi um leque tambem, e ai a mesma forma sombreava diferente conforme o que
    // existia LONGE dela: um bloco com tronco pendurado ficava com o nucleo escuro empurrado para
    // baixo, porque os raios da face de baixo iam bater no chao doze celulas abaixo. Profundidade
    // dentro da materia nao pode depender de nada que esta fora dela - por isso ela virou uma
    // erosao da propria forma, que da nucleo centrado em qualquer bloco.
    //
    // Caverna continua preta porque o ar dela ja e preto e e ele que semeia; tronco acende porque
    // o ar ao lado dele ve ceu; miolo de copa escurece porque esta fundo; e o terreno escurece com
    // a profundidade pelo mesmo motivo.
    //
    // A SOMBRA PROJETADA nao mora mais aqui: ela e feita no shader, por fragmento. Esta classe so
    // entrega a textura de dados que ele consome. O plano e o registro dos defeitos ja encontrados
    // estao em Docs/LightMapShadowPlan.md.
    //
    // O estado (arrays e fila) e por instancia: cada no tem a sua calculadora, entao as janelas
    // podem ter tamanhos diferentes sem uma realocar a da outra a cada quadro.
    public sealed class LightMapComputer
    {
        #region Static properties

        // Quantos raios formam o leque, e quantos deles decidem o brilho. Ver VisaoDoCeu.
        private const int Amostras = 16;
        private const int Melhores = Amostras / 4;

        // Quantas passadas de media o ambiente leva no fim. Ver AlisarAmbiente.
        private const int PassadasDeAlisamento = 4;

        // Quantas celulas de ar o ambiente se espalha antes de acabar. E o alcance do rebote: luz
        // que bate no que esta iluminado em volta e volta. Ver EspalharAmbienteNoAr.
        // Quantas celulas de ar o ambiente se espalha antes de acabar. E o alcance do rebote: luz
        // que bate no que esta iluminado em volta e volta. Ver EspalharAmbienteNoAr.
        private const float AlcanceDoAmbiente = 40f;


        #endregion

        #region Dinamic properties

        // Desligado, o terreno fica todo em brilho cheio.
        public bool DiffuseEnabled { get; set; } = true;

        // A escala inteira em que a luz e contada. Junto com SolidCost decide ate onde ela chega.
        public int MaxLevel { get; set; } = LightMapConstants.MAX_LEVEL;

        // Custo de atravessar um bloco. Quanto maior, mais rapido escurece com a profundidade.
        public int SolidCost { get; set; } = LightMapConstants.COST_SOLID;

        // Piso de luminosidade: sem isso o fundo do mundo fica preto absoluto.
        public float MinBrightness { get; set; } = LightMapConstants.MIN_BRIGHTNESS;

        public int Largura { get; private set; }
        public int Altura { get; private set; }

        // A grade de custo, uma entrada por celula, em ordem de linha. Quem chama preenche - por
        // PreencherGrade, que le TileMapLayer, ou na mao.
        public float[] Custos => _custos;

        #endregion

        #region Static properties

        private readonly Queue<int> _fila = new();

        private float[] _niveis = System.Array.Empty<float>();

        // Quanto do ceu cada celula ve, de 0 a 1 - a MEDIA INTEIRA do leque, nao o melhor quarto.
        // E o canal B da textura, e o que o shader usa para afrouxar a sombra. Ver CalcularVisaoDoCeu.
        private float[] _ambiente = System.Array.Empty<float>();

        // Buffer das passadas que nao podem ler o que ja escreveram.
        private float[] _ambienteAlisado = System.Array.Empty<float>();


        private float[] _custos = System.Array.Empty<float>();


        // A silhueta do terreno, por coluna: a linha da materia mais ALTA dali. _topoEsq guarda o
        // menor topo de todas as colunas ate x, e _topoDir o mesmo olhando para a direita. Servem
        // para o raio desistir cedo - ver o comentario em Raio.
        private int[] _topo = System.Array.Empty<int>();
        private int[] _topoEsq = System.Array.Empty<int>();
        private int[] _topoDir = System.Array.Empty<int>();

        // As direcoes do leque, calculadas uma vez por tamanho de leque em vez de duas
        // trigonometricas por celula por raio.
        private float[] _leque = System.Array.Empty<float>();

        // As melhores transmissoes do leque, reaproveitado entre celulas - ver VisaoDoCeu.
        private readonly float[] _melhores = new float[Melhores];

        private Image _imagem;
        private byte[] _pixels = System.Array.Empty<byte>();

        #endregion

        #region Core - Entrada

        // Idempotente: so realoca quando o tamanho da janela muda de verdade.
        public void Redimensionar(int largura, int altura)
        {
            largura = Mathf.Max(1, largura);
            altura = Mathf.Max(1, altura);

            if (Largura == largura && Altura == altura)
            {
                return;
            }

            Largura = largura;
            Altura = altura;

            var total = largura * altura;

            _niveis = new float[total];
            _ambiente = new float[total];
            _ambienteAlisado = new float[total];
            _custos = new float[total];

            _topo = new int[largura];
            _topoEsq = new int[largura];
            _topoDir = new int[largura];

            _imagem = null;
        }

        // Para onde a luz do sol viaja. Rotacao 0 aponta para BAIXO, nao para cima: e onde o sol
        // esta acima do mundo. Com o arco montado em torno de 180 a luz saia da cena.
        public static Vector2 DirecaoDaLuz(float rotacaoEmRadianos)
        {
            return Vector2.Down.Rotated(rotacaoEmRadianos);
        }

        // Le as camadas de tile e monta a grade. A ordem da lista manda: a primeira que tiver
        // alguma coisa na celula decide. E o que faz uma camada de composicao acrescentar copa de
        // arvore sem apagar o terreno que esta embaixo.
        public void PreencherGrade(IReadOnlyList<TileMapLayer> camadas, Vector2I origem)
        {
            if (camadas == null || camadas.Count == 0)
            {
                return;
            }

            for (int y = 0; y < Altura; y++)
            {
                for (int x = 0; x < Largura; x++)
                {
                    var i = y * Largura + x;
                    var dados = DadosDaCelula(camadas, new Vector2I(origem.X + x, origem.Y + y));

                    if (dados == null)
                    {
                        _custos[i] = 0f;

                        continue;
                    }

                    // Todo tile e igual: se tem tile, e materia, e materia e opaca. Nao ha
                    // marca, categoria nem excecao - nem colisao, que ja foi tentada e neste
                    // tileset diz o contrario do esperado (copa TEM colisao, tronco e caixa NAO).
                    _custos[i] = SolidCost;
                }
            }
        }

        // Uma celula e ar quando ela nao tem tile - e ar nao custa nada.
        private bool EhAr(int celula)
        {
            return _custos[celula] <= 0f;
        }

        private static TileData DadosDaCelula(IReadOnlyList<TileMapLayer> camadas, Vector2I celula)
        {
            for (int i = 0; i < camadas.Count; i++)
            {
                var dados = camadas[i]?.GetCellTileData(celula);

                if (dados != null)
                {
                    return dados;
                }
            }

            return null;
        }

        #endregion

        #region Core - Calculo

        // Roda o mapa sobre a grade ja preenchida e devolve a imagem de brilho, um texel por
        // celula. A imagem e reaproveitada entre chamadas do mesmo tamanho, entao quem consome
        // deve usa-la no mesmo quadro (ImageTexture.Update) em vez de guardar.
        public Image Calcular()
        {
            if (Largura <= 0 || Altura <= 0)
            {
                return null;
            }

            if (DiffuseEnabled)
            {
                CalcularVisaoDoCeu();
            }

            return Desenhar();
        }

        // A materia, erodida a partir do ar encostado nela.
        //
        // Cada celula de materia com ar vizinho nasce com o nivel DESSE ar - sem pagar custo: ela e
        // a face que recebe a luz, e cobrar ja nela deixava a grama da superficie escura. Dali para
        // dentro cada celula custa o proprio custo, entao o nivel cai com a profundidade e o nucleo
        // de um bloco fica centrado, seja qual for a forma e independente do que exista em volta.
        //
        // Relaxacao em fila, nao largura simples: o custo varia por celula (folhagem e bloco tem
        // custos diferentes), entao uma celula pode melhorar depois de ja ter saido da fila.
        private void ErodirMateria()
        {
            _fila.Clear();

            for (int i = 0; i < _niveis.Length; i++)
            {
                if (EhAr(i))
                {
                    continue;
                }

                var x = i % Largura;
                var y = i / Largura;

                var nivel = 0f;

                if (x > 0 && EhAr(i - 1)) nivel = Mathf.Max(nivel, _niveis[i - 1]);
                if (x < Largura - 1 && EhAr(i + 1)) nivel = Mathf.Max(nivel, _niveis[i + 1]);
                if (y > 0 && EhAr(i - Largura)) nivel = Mathf.Max(nivel, _niveis[i - Largura]);
                if (y < Altura - 1 && EhAr(i + Largura)) nivel = Mathf.Max(nivel, _niveis[i + Largura]);

                if (nivel <= 0f)
                {
                    continue;
                }

                _niveis[i] = nivel;

                _fila.Enqueue(i);
            }

            while (_fila.Count > 0)
            {
                var i = _fila.Dequeue();

                var nivel = _niveis[i];

                var x = i % Largura;
                var y = i / Largura;

                if (x > 0) Escurecer(i - 1, nivel);
                if (x < Largura - 1) Escurecer(i + 1, nivel);
                if (y > 0) Escurecer(i - Largura, nivel);
                if (y < Altura - 1) Escurecer(i + Largura, nivel);
            }
        }

        private void Escurecer(int destino, float nivelOrigem)
        {
            if (EhAr(destino))
            {
                return;
            }

            var novo = nivelOrigem - _custos[destino];

            if (novo <= _niveis[destino])
            {
                return;
            }

            _niveis[destino] = novo;

            _fila.Enqueue(destino);
        }

        // A materia mais alta de cada coluna, e o acumulado dela para os dois lados.
        private void MontarSilhueta()
        {
            for (int x = 0; x < Largura; x++)
            {
                _topo[x] = Altura;

                for (int y = 0; y < Altura; y++)
                {
                    if (!EhAr(y * Largura + x))
                    {
                        _topo[x] = y;

                        break;
                    }
                }
            }

            var corrente = Altura;

            for (int x = 0; x < Largura; x++)
            {
                corrente = Mathf.Min(corrente, _topo[x]);
                _topoEsq[x] = corrente;
            }

            corrente = Altura;

            for (int x = Largura - 1; x >= 0; x--)
            {
                corrente = Mathf.Min(corrente, _topo[x]);
                _topoDir[x] = corrente;
            }
        }

        // Os angulos varrem de 0 a PI: do horizonte da direita ao horizonte da esquerda passando
        // pelo alto - so o semicirculo de CIMA, porque so o ar traca, e o que ele mede e ceu.
        // Raio para baixo sairia pelo fundo da janela e contaria como ceu o que e subsolo.
        private void MontarLeque()
        {
            if (_leque.Length == Amostras * 2)
            {
                return;
            }

            _leque = new float[Amostras * 2];

            for (int k = 0; k < Amostras; k++)
            {
                var angulo = Mathf.Pi * (k + 0.5f) / Amostras;

                _leque[k * 2] = Mathf.Cos(angulo);
                _leque[k * 2 + 1] = -Mathf.Sin(angulo);
            }
        }

        // O nivel de cada celula: o quanto de ceu ela enxerga, na escala de MaxLevel.
        private void CalcularVisaoDoCeu()
        {
            MontarSilhueta();
            MontarLeque();

            for (int y = 0; y < Altura; y++)
            {
                for (int x = 0; x < Largura; x++)
                {
                    // Acima da silhueta inteira - dos dois lados - nao ha o que tapar: o raio so
                    // sobe, entao todo raio deste ponto sai limpo. E ceu aberto, que e a maior
                    // parte da tela, e sai por um teste em vez do leque todo.
                    if (y < _topoEsq[x] && y < _topoDir[x])
                    {
                        _niveis[y * Largura + x] = MaxLevel;
                        _ambiente[y * Largura + x] = 1f;

                        continue;
                    }

                    var celula = y * Largura + x;

                    // Materia nao traca: ela e semeada depois, por ErodirMateria.
                    if (!EhAr(celula))
                    {
                        _niveis[celula] = 0f;
                        _ambiente[celula] = 0f;

                        continue;
                    }

                    _niveis[celula] = MaxLevel * VisaoDoCeu(x, y, out var fracao);
                    _ambiente[celula] = fracao;
                }
            }

            ErodirMateria();
            EspalharAmbienteNoAr();
            AlisarAmbiente();
            EspalharAmbiente();
        }

        // Devolve DUAS medidas do mesmo leque, porque sao duas perguntas diferentes:
        //
        //   retorno       a media dos MELHORES raios - o quanto a face mais exposta recebe.
        //   fracaoDoCeu   a media do leque INTEIRO   - que fracao do ceu este ponto ve.
        //
        // A primeira semeia a materia; a segunda e o ambiente, que o shader usa para afrouxar a
        // sombra. Usar a primeira nas duas quantizava o ambiente: media de quatro raios binarios so
        // pode dar 0, 0.25, 0.5, 0.75 ou 1, e a sombra ganhava manchas de borda seca.
        //
        // Por que a primeira nao serve de ambiente ja esta dito abaixo; por que a segunda nao serve
        // de semente, tambem: ela poe a largura da copa na conta do tronco.
        //
        // A media do circulo mede volume: ela nao distingue "enterrado no meio do bloco" de
        // "pendurado embaixo dele com o aberto dos dois lados" - so muda o grau. E pior, ela poe a
        // LARGURA do que esta em volta direto na conta: a copa tapa um cone de
        // 2*atan(metade da copa / altura), entao dobrar a copa dobrava o cone e escurecia o tronco,
        // que nao tem nada a ver com isso.
        //
        // Um ponto e iluminado pela face dele que esta mais exposta, e nao pela media de todas as
        // direcoes. O quarto melhor do leque e essa face:
        //
        //   miolo de um bloco     todo raio morre na materia          -> escuro
        //   face de um bloco      o quarto virado para fora sai limpo -> cheio, em qualquer face
        //   tronco sob copa       o quarto lateral sai limpo          -> cheio, em qualquer copa
        //   fresta de caverna     um raio so sai, num quarto de 6     -> fraco
        //   boca de caverna       varios saem                         -> claro
        private float VisaoDoCeu(int x, int y, out float fracaoDoCeu)
        {
            System.Array.Clear(_melhores, 0, Melhores);

            var total = 0f;

            for (int k = 0; k < Amostras; k++)
            {
                var t = Raio(x, y, _leque[k * 2], _leque[k * 2 + 1]);

                total += t;

                if (t <= _melhores[Melhores - 1])
                {
                    continue;
                }

                var i = Melhores - 1;

                while (i > 0 && _melhores[i - 1] < t)
                {
                    _melhores[i] = _melhores[i - 1];

                    i--;
                }

                _melhores[i] = t;
            }

            fracaoDoCeu = total / Amostras;

            var soma = 0f;

            for (int i = 0; i < Melhores; i++)
            {
                soma += _melhores[i];
            }

            return soma / Melhores;
        }

        // O rebote: a luz que bate no que esta iluminado em volta e volta.
        //
        // So "que fracao do ceu eu vejo DAQUI" nao basta. Debaixo de uma copa isso e quase zero
        // mesmo com a faixa iluminada a tres celulas de distancia, e duas sombras vizinhas com a
        // mesma luz em volta ficam com escuridao muito diferente conforme o que cada uma tem por
        // cima. Na sombra real quem preenche vem DOS LADOS.
        //
        // Entao o ambiente escorre de um ponto de ar para os vizinhos de ar, perdendo
        // 1 / AlcanceDoAmbiente por celula.
        //
        // Passa SO por ar: parede corta. E o que mantem o quarto lacrado preto - do contrario isto
        // desfaria o conserto do raio binario, vazando luz por dentro da parede.
        //
        // Relaxacao em fila, igual a erosao: uma celula pode melhorar depois de ja ter saido dela.
        private void EspalharAmbienteNoAr()
        {
            var perda = 1f / AlcanceDoAmbiente;

            _fila.Clear();

            for (int i = 0; i < _ambiente.Length; i++)
            {
                if (EhAr(i) && _ambiente[i] > perda)
                {
                    _fila.Enqueue(i);
                }
            }

            while (_fila.Count > 0)
            {
                var i = _fila.Dequeue();

                var nivel = _ambiente[i] - perda;

                if (nivel <= 0f)
                {
                    continue;
                }

                var x = i % Largura;
                var y = i / Largura;

                if (x > 0) Vazar(i - 1, nivel);
                if (x < Largura - 1) Vazar(i + 1, nivel);
                if (y > 0) Vazar(i - Largura, nivel);
                if (y < Altura - 1) Vazar(i + Largura, nivel);
            }
        }

        private void Vazar(int destino, float nivel)
        {
            if (!EhAr(destino) || nivel <= _ambiente[destino])
            {
                return;
            }

            _ambiente[destino] = nivel;

            _fila.Enqueue(destino);
        }

        // Media do ambiente com os vizinhos de AR, algumas vezes.
        //
        // O leque tem um numero fixo de raios, e os mesmos angulos em toda celula. Andar uma celula
        // faz a borda de uma copa cruzar a fronteira de um raio, e o ambiente pula 1/Amostras de
        // uma vez - as vezes para cima, as vezes para baixo. Medido subindo ao lado de um tronco:
        // 63, 63, 63, 47, 63, 47 - sobe e desce sem regra, e todos multiplos exatos de 1/16.
        //
        // Como os angulos sao os mesmos em todo lugar, esse erro fica coerente no espaco e vira
        // MANCHA, nao ruido. Triplicar os raios reduz o degrau mas nao o elimina, e custa o triplo.
        // A media resolve porque o erro e de alta frequencia e o campo de ambiente nao e.
        //
        // Conta so vizinho de ar, e por isso parede continua cortando: quarto lacrado segue em zero.
        private void AlisarAmbiente()
        {
            for (int passada = 0; passada < PassadasDeAlisamento; passada++)
            {
                for (int i = 0; i < _ambiente.Length; i++)
                {
                    if (!EhAr(i))
                    {
                        _ambienteAlisado[i] = _ambiente[i];

                        continue;
                    }

                    var x = i % Largura;
                    var y = i / Largura;

                    var soma = _ambiente[i];
                    var conta = 1;

                    if (x > 0 && EhAr(i - 1)) { soma += _ambiente[i - 1]; conta++; }
                    if (x < Largura - 1 && EhAr(i + 1)) { soma += _ambiente[i + 1]; conta++; }
                    if (y > 0 && EhAr(i - Largura)) { soma += _ambiente[i - Largura]; conta++; }
                    if (y < Altura - 1 && EhAr(i + Largura)) { soma += _ambiente[i + Largura]; conta++; }

                    _ambienteAlisado[i] = soma / conta;
                }

                (_ambiente, _ambienteAlisado) = (_ambienteAlisado, _ambiente);
            }
        }

        // A materia herda o ambiente do ar encostado nela: ela e opaca, entao a luz de ambiente que
        // banha a face dela e a que existe do lado de fora. Bloco sem vizinho de ar fica em zero.
        private void EspalharAmbiente()
        {
            for (int i = 0; i < _ambiente.Length; i++)
            {
                if (EhAr(i))
                {
                    continue;
                }

                var x = i % Largura;
                var y = i / Largura;

                var maior = 0f;

                if (x > 0 && EhAr(i - 1)) maior = Mathf.Max(maior, _ambiente[i - 1]);
                if (x < Largura - 1 && EhAr(i + 1)) maior = Mathf.Max(maior, _ambiente[i + 1]);
                if (y > 0 && EhAr(i - Largura)) maior = Mathf.Max(maior, _ambiente[i - Largura]);
                if (y < Altura - 1 && EhAr(i + Largura)) maior = Mathf.Max(maior, _ambiente[i + Largura]);

                _ambiente[i] = maior;
            }
        }

        // Se este raio chega ao ceu: 1 ou 0, sem meio termo.
        //
        // Materia e OPACA. Ja foi absorcao acumulada, e ai o alcance do raio - MaxLevel / custo -
        // valia como espessura de parede: com 14 e custo 4, o raio atravessava 3,5 celulas, e um
        // quarto lacrado por parede de UMA celula media 182 de 255 de ceu visivel. Luz passando
        // por parede.
        //
        // Aquele mesmo numero e o que diz quantos blocos a luz penetra no terreno. Sao duas
        // perguntas opostas - o degrade quer que ela va fundo, a vedacao quer que pare no primeiro
        // bloco - e nao cabem no mesmo valor. Por isso aqui e binario, e o degrade fica por conta
        // de ErodirMateria, que e outra passada e tem o custo por celula so dela.
        //
        // O degrade espacial nao se perde: o que varia de ponto para ponto e QUANTOS raios do
        // leque escapam, e isso muda suave.
        //
        // Anda por DDA, saltando de fronteira em fronteira de celula: cada celula cruzada e
        // visitada exatamente uma vez. Com passo fixo, um passo curto conta a mesma celula duas
        // vezes e um passo longo pula bloco fino na diagonal.
        //
        // A celula de ORIGEM nao e cobrada: a conta e do que tapa a vista dela, e nao dela mesma.
        // E o que deixa a face exposta de um bloco receber luz cheia sem precisar de remendo.
        private float Raio(int x, int y, float dx, float dy)
        {
            float px = x + 0.5f;
            float py = y + 0.5f;

            var cx = x;
            var cy = y;

            var avancoX = dx > 0f ? 1 : -1;
            var avancoY = dy > 0f ? 1 : -1;

            var absX = Mathf.Abs(dx);
            var absY = Mathf.Abs(dy);

            var deltaX = absX < 1e-6f ? 1e9f : 1f / absX;
            var deltaY = absY < 1e-6f ? 1e9f : 1f / absY;

            var proximoX = absX < 1e-6f ? 1e9f : (dx > 0f ? cx + 1f - px : px - cx) / absX;
            var proximoY = absY < 1e-6f ? 1e9f : (dy > 0f ? cy + 1f - py : py - cy) / absY;

            var limite = Largura + Altura;

            for (int passo = 0; passo < limite; passo++)
            {
                if (proximoX < proximoY)
                {
                    proximoX += deltaX;
                    cx += avancoX;
                }
                else
                {
                    proximoY += deltaY;
                    cy += avancoY;
                }

                // Saiu da janela: dali para fora e ceu aberto, nada mais tapa.
                if (cx < 0 || cy < 0 || cx >= Largura || cy >= Altura)
                {
                    return 1f;
                }

                // Ja passou por cima de tudo que ainda podia tapa-lo. O raio so sobe, entao daqui
                // em diante toda celula que ele visitar esta acima da materia mais alta que sobrou
                // na direcao em que ele anda - nao ha o que encontrar. Sem isto o raio caminha a
                // janela inteira por ar vazio so para descobrir que era ceu, e o ceu aberto, que e
                // a maior parte da tela, custa o maximo em vez do minimo.
                var silhueta = avancoX > 0 ? _topoDir[cx] : _topoEsq[cx];

                if (cy < silhueta)
                {
                    return 1f;
                }

                if (!EhAr(cy * Largura + cx))
                {
                    return 0f;
                }
            }

            return 0f;
        }

        // Quanta luz do ceu chega a uma celula, de 0 a 1. Vale para ar e para materia.
        private float NivelDifuso(int celula)
        {
            return DiffuseEnabled ? _niveis[celula] / MaxLevel : 1f;
        }

        // O brilho que a difusa da a uma celula de materia. E o que vai no canal G da textura.
        //
        // Vale o nivel da propria celula: a semeadura de ErodirMateria ja fez o papel da face,
        // dando a cada celula de superficie o nivel do ar encostado nela.
        private float BrilhoDaMateria(int celula)
        {
            return Mathf.Lerp(MinBrightness, 1f, NivelDifuso(celula));
        }

        // A textura de DADOS que o shader consome, um texel por celula:
        //
        //   R = 1 se ha materia ali, 0 se e ar
        //   G = o brilho que a difusa deu aquela celula (so a materia usa)
        //   B = quanta luz do ceu chega ali, de 0 a 1 - INCLUSIVE no ar
        //
        // A CPU nao desenha mais sombra nenhuma. O shader tracaa o raio por fragmento, entao a
        // borda ganha a resolucao da tela em vez da celula, e o custo sai daqui - onde ele era
        // proporcional a area vezes o numero de sub-celulas.
        private Image Desenhar()
        {
            var total = Largura * Altura * 3;

            if (_pixels.Length != total)
            {
                _pixels = new byte[total];
            }

            for (int i = 0; i < Largura * Altura; i++)
            {
                var noAr = EhAr(i);

                var p = i * 3;

                // Ar, folhagem e parede, e nao um bit de "tem materia". A folhagem precisa
                // aparecer separada porque ela BLOQUEIA O SOL e DEIXA PASSAR O AMBIENTE - copa de
                // arvore corta o raio direto e ainda assim tem ceu do outro lado dela. Com um bit
                // so, o bolsao embaixo de uma copa lia como quarto fechado e ficava preto.
                _pixels[p] = noAr ? (byte)0 : (byte)255;
                _pixels[p + 1] = (byte)Mathf.Clamp(BrilhoDaMateria(i) * 255f, 0f, 255f);

                // O ambiente: que fracao do ceu esta celula ve, ar incluido. E o que deixa o
                // shader saber se um ponto esta em lugar aberto ou fechado - sem isso a sombra no
                // ar tem a mesma forca dentro de uma caverna e a ceu aberto.
                _pixels[p + 2] = (byte)Mathf.Clamp(_ambiente[i] * 255f, 0f, 255f);
            }

            _imagem = Image.CreateFromData(Largura, Altura, false, Image.Format.Rgb8, _pixels);

            return _imagem;
        }

        #endregion
    }
}
