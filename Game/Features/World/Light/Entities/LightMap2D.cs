using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Light
{
    // O mapa de luz como no de cena: ponha-o junto das camadas de tile e ele desenha, ao vivo no
    // editor e em jogo, quanta luz do ceu chega a cada celula. A luz entra por cima e perde
    // intensidade a cada bloco atravessado, entao caverna nasce escura, boca de caverna vira
    // degrade em vez de corte, e construir para cima escurece o que fica embaixo.
    //
    // A luz nao cai reta: ela desce inclinada segundo o angulo do sol, e cada linha amostra a de
    // cima deslocada. E dai que sai a sombra projetada de uma torre ou de uma copa de arvore -
    // sem oclusor, sem atlas de sombra e sem borda dura, porque e o mesmo mecanismo do resto do
    // mapa. Ao meio-dia (angulo perto de 0) a direcao e quase reta para baixo e a sombra cai reta,
    // aterrissando no bloco logo abaixo: nesse horario nao ha projecao para ver.
    //
    // O calculo mora no LightMapComputer; este no cuida so das bordas - de onde vem a grade (as
    // camadas), onde fica a janela (a camera) e para onde vai a imagem (o overlay).
    //
    // A imagem vai num Sprite2D filho com TopLevel ligado, e nao no _Draw deste no: assim ela
    // fica presa ao mundo e nao a transformada deste no.
    [Tool]
    public partial class LightMap2D : Node2D
    {
        #region Exports

        /// <summary>
        /// Liga e desliga o no por inteiro. Desligado, o overlay some e a cena aparece sem
        /// escurecimento nenhum, no editor e em jogo.
        /// </summary>
        [ExportCategory("Light Map")]
        [Export] public bool LightMapEnabled { get; set; } = true;

        /// <summary>
        /// Se o mapa e desenhado dentro do editor. Ligado, ele e refeito A CADA QUADRO - de
        /// proposito: nada avisa quando voce pinta um tile, e um preview parado enquanto se
        /// desenha o mapa nao serve para calibrar. Em jogo o recalculo e sob demanda, nao por
        /// quadro. Desligue se o editor pesar numa cena grande.
        /// </summary>
        [Export] public bool PreviewInEditor { get; set; } = true;

        /// <summary>
        /// Tamanho da janela de calculo em celulas, usado SO DENTRO DO EDITOR. Em jogo isto e
        /// ignorado: a janela sai da area visivel da camera mais a margem, que e o que precisa
        /// estar coberto. Aumentar aqui so custa no preview.
        /// </summary>
        [Export] public Vector2I PreviewSize { get; set; } = new Vector2I(120, 80);

        /// <summary>
        /// As camadas de tile que formam o terreno, NA ORDEM DE PRIORIDADE: a primeira que tiver
        /// alguma coisa na celula decide. E isso que deixa uma camada de composicao acrescentar
        /// copa de arvore sem apagar o terreno de baixo.
        ///
        /// Em branco, o no usa todas as TileMapLayer irmas na ordem da cena - que nas cenas de
        /// dimensao da Base e depois Compose. A primeira da lista tambem e a referencia de grid:
        /// tamanho de celula e conversao mundo/celula saem dela.
        /// </summary>
        [Export] public Godot.Collections.Array<NodePath> Layers { get; set; } = new();

        /// <summary>
        /// A camera que a janela de calculo segue: ela da o CENTRO da janela e, em jogo, o
        /// TAMANHO (area visivel dividida pelo zoom, mais a margem). Em branco, o no usa o
        /// Camera2D irmao, que e como a cena de dimensao ja e feita.
        ///
        /// O centro nunca e a posicao do proprio no: em jogo quem diz o que precisa estar
        /// iluminado e a camera. Se o no apontado nao for um Camera2D, o tamanho cai para o
        /// PreviewSize.
        /// </summary>
        [Export] public NodePath Camera { get; set; } = new NodePath("");

        // Tipado como Resource com dica de tipo, e nao como LightMapData direto, de proposito: o
        // editor carrega a cena antes de ligar o assembly C# e entrega o .tres como Resource cru,
        // sem instancia gerenciada. Com o tipo forte isso vira InvalidCastException toda vez que a
        // cena abre. A dica mantem o filtro do inspetor; a leitura por nome resolve o resto.
        //
        /// <summary>
        /// O recurso com TODOS os ajustes do mapa - um LightMapData, em Assets/Data. O no nao
        /// guarda ajuste nenhum. Duas dimensoes apontando para o mesmo arquivo se calibram juntas;
        /// para uma ter ajuste proprio, duplique o .tres e aponte so ela para a copia. Vazio, o no
        /// usa os padroes do codigo.
        /// </summary>
        [Export(PropertyHint.ResourceType, "LightMapData")] public Resource Settings { get; set; }

        #endregion

        #region Static properties

        private readonly LightMapComputer _calculadora = new();

        // Usado quando nenhum LightMapData foi apontado: mantem o no funcionando com os padroes.
        private LightMapData _padroes;

        // Copia preenchida a partir de um Resource cru, quando o editor entrega o .tres sem tipo.
        private LightMapData _copia;

        // A primeira da lista e a referencia de grid: tamanho de celula e conversao mundo/celula
        // saem dela.
        private readonly System.Collections.Generic.List<TileMapLayer> _camadas = new();

        private Node2D _camera;
        private Sprite2D _overlay;

        private Vector2I _ultimaOrigem;
        private bool _temResultado;
        private bool _sujo = true;

        #endregion

        #region Godot implementation

        public override void _Process(double delta)
        {
            if (!LightMapEnabled || (Engine.IsEditorHint() && !PreviewInEditor))
            {
                EsconderOverlay();

                return;
            }

            ResolverReferencias();
            Atualizar();
        }

        #endregion

        #region Core - Setup

        /// <summary>
        /// Marca a janela como suja: o proximo quadro refaz o mapa. Chamado quando um bloco nasce
        /// ou some. Nao recalcula na hora, entao mil blocos seguidos custam um recalculo so.
        /// </summary>
        public void Invalidate()
        {
            _sujo = true;
        }

        private LightMapData Ajustes
        {
            get
            {
                if (Settings is LightMapData tipado)
                {
                    return tipado;
                }

                if (Settings != null)
                {
                    return Copiar(Settings);
                }

                return _padroes ??= new LightMapData();
            }
        }

        // Le o recurso por nome de propriedade. Funciona mesmo quando ele chega como Resource
        // cru, porque os valores estao gravados no objeto de qualquer jeito - o que falta e so a
        // instancia C# por tras. Uma dezena de leituras por recalculo nao pesa perto do mapa.
        private LightMapData Copiar(Resource bruto)
        {
            _copia ??= new LightMapData();

            var padrao = _padroes ??= new LightMapData();

            _copia.ShowRawMap = LerBool(bruto, nameof(LightMapData.ShowRawMap), padrao.ShowRawMap);
            _copia.Penumbra = LerFloat(bruto, nameof(LightMapData.Penumbra), padrao.Penumbra);

            _copia.SunAngleDegrees = LerFloat(bruto, nameof(LightMapData.SunAngleDegrees), padrao.SunAngleDegrees);

            _copia.AirShadowEnabled = LerBool(bruto, nameof(LightMapData.AirShadowEnabled), padrao.AirShadowEnabled);
            _copia.AmbientInfluence = LerFloat(bruto, nameof(LightMapData.AmbientInfluence), padrao.AmbientInfluence);
            _copia.AirShadowOpacity = LerFloat(bruto, nameof(LightMapData.AirShadowOpacity), padrao.AirShadowOpacity);

            return _copia;
        }

        private static float LerFloat(Resource bruto, string nome, float padrao)
        {
            var valor = bruto.Get(nome);

            return valor.VariantType == Variant.Type.Nil ? padrao : valor.AsSingle();
        }

        private static int LerInt(Resource bruto, string nome, int padrao)
        {
            var valor = bruto.Get(nome);

            return valor.VariantType == Variant.Type.Nil ? padrao : valor.AsInt32();
        }

        private static bool LerBool(Resource bruto, string nome, bool padrao)
        {
            var valor = bruto.Get(nome);

            return valor.VariantType == Variant.Type.Nil ? padrao : valor.AsBool();
        }

        // O angulo do sol, em radianos.
        private static float RotacaoDoSol(LightMapData ajustes)
        {
            return Mathf.DegToRad(ajustes.SunAngleDegrees);
        }

        private void ResolverReferencias()
        {
            if (_camadas.Count == 0 || !IsInstanceValid(_camadas[0]))
            {
                ResolverCamadas();
            }

            if (_camera == null || !IsInstanceValid(_camera))
            {
                _camera = ResolverCamera();
            }
        }

        private void ResolverCamadas()
        {
            _camadas.Clear();

            foreach (var caminho in Layers)
            {
                if (caminho == null || caminho.IsEmpty)
                {
                    continue;
                }

                var apontada = GetNodeOrNull<TileMapLayer>(caminho);

                if (apontada != null)
                {
                    _camadas.Add(apontada);
                }
            }

            if (_camadas.Count > 0)
            {
                return;
            }

            // Sem lista, todas as TileMapLayer irmas, na ordem da cena.
            if (GetParent() is not Node parent)
            {
                return;
            }

            foreach (var filho in parent.GetChildren())
            {
                if (filho is TileMapLayer camada)
                {
                    _camadas.Add(camada);
                }
            }
        }

        private Node2D ResolverCamera()
        {
            if (Camera != null && !Camera.IsEmpty)
            {
                var apontada = GetNodeOrNull<Node2D>(Camera);

                if (apontada != null)
                {
                    return apontada;
                }
            }

            return GetParent()?.GetNodeOrNull<Camera2D>("Camera2D");
        }

        // Filho, com TopLevel: assim ele ignora a transformada deste no e nao gira junto quando o
        // angulo vem da rotacao. Nao recebe Owner, entao nao e gravado na cena.
        private Sprite2D ResolverOverlay()
        {
            if (_overlay == null || !IsInstanceValid(_overlay))
            {
                _overlay = GetNodeOrNull<Sprite2D>(LightMapConstants.OVERLAY_NODE_NAME);
            }

            if (_overlay == null)
            {
                _overlay = new Sprite2D
                {
                    Name = LightMapConstants.OVERLAY_NODE_NAME,
                    Centered = false,
                    TopLevel = true,
                    ZIndex = LightMapConstants.OVERLAY_Z_INDEX,
                    ZAsRelative = false,

                    // Filtro NEAREST de proposito: quem le a textura e o shader, por texelFetch, e
                    // ele precisa do valor cru da celula. A suavidade vem do raio por pixel, nao
                    // de interpolar dados.
                    TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                };

                AddChild(_overlay);
            }

            if (_overlay.Material is not ShaderMaterial)
            {
                var shader = GD.Load<Shader>(LightMapConstants.SHADER_PATH);

                _overlay.Material = shader == null ? null : new ShaderMaterial { Shader = shader };
            }

            return _overlay;
        }

        private void EsconderOverlay()
        {
            if (_overlay != null && IsInstanceValid(_overlay))
            {
                _overlay.Visible = false;
            }
        }

        #endregion

        #region Core - Atualizacao

        private void Atualizar()
        {
            if (_camadas.Count == 0 || !IsInstanceValid(_camadas[0]) || _camadas[0].TileSet == null)
            {
                EsconderOverlay();

                return;
            }

            var overlay = ResolverOverlay();

            overlay.Visible = true;

            var grid = _camadas[0];
            var celula = grid.TileSet.TileSize;

            var ajustes = Ajustes;

            var direcaoDoSol = LightMapComputer.DirecaoDaLuz(RotacaoDoSol(ajustes));

            DimensionarJanela(celula, direcaoDoSol, out var largura, out var altura);

            var centro = grid.LocalToMap(grid.ToLocal(PosicaoDoCentro()));
            var origem = new Vector2I(centro.X - largura / 2, centro.Y - altura / 2);

            // A direcao do sol NAO entra no gatilho abaixo. A grade que a CPU monta e materia e
            // difusa, e nada nela olha o angulo - quem usa a direcao e so um SetShaderParameter.
            // Enquanto esta chamada ficou dentro do bloco de recalculo, girar o sol disparava o
            // recalculo inteiro da grade para atualizar um vec2, e existia uma constante so para
            // evitar esse desperdicio: o remendo de um acoplamento que nao precisava existir.
            // Separados, o sol gira liso e sem degrau.
            AlimentarShader(overlay, ajustes, direcaoDoSol, largura, altura);

            // No editor refaz sempre: nada avisa quando um tile e pintado, e um preview parado
            // enquanto se desenha o mapa nao serve para calibrar. Em jogo so refaz quando a
            // janela anda ou um bloco muda.
            var precisa = Engine.IsEditorHint() || _sujo || !_temResultado || origem != _ultimaOrigem;

            if (!precisa)
            {
                return;
            }

            _ultimaOrigem = origem;
            _sujo = false;


            _calculadora.Redimensionar(largura, altura);
            _calculadora.PreencherGrade(_camadas, origem);

            var imagem = _calculadora.Calcular();

            if (imagem == null)
            {
                _temResultado = false;

                return;
            }

            if (overlay.Texture is ImageTexture textura && textura.GetSize() == new Vector2(largura, altura))
            {
                textura.Update(imagem);
            }
            else
            {
                overlay.Texture = ImageTexture.CreateFromImage(imagem);
            }

            // MapToLocal devolve o centro da celula; o sprite comeca no canto dela. Global porque
            // o overlay e TopLevel, e portanto nao herda a transformada deste no.
            overlay.GlobalPosition = grid.ToGlobal(grid.MapToLocal(origem) - (Vector2)celula / 2f);
            overlay.Scale = celula;

            _temResultado = true;
        }

        // Os uniformes que o shader consome. A CPU nao calcula sombra: ela entrega a grade e os
        // ajustes, e o raio e tracado por fragmento.
        private void AlimentarShader(Sprite2D overlay, LightMapData ajustes, Vector2 direcao, int largura, int altura)
        {
            if (overlay.Material is not ShaderMaterial material)
            {
                return;
            }

            // Direcao em que a luz VIAJA, crua. O shader anda contra ela, rumo ao sol.
            //
            // Ja veio achatada num unico numero - a inclinacao X/Y - e remontada aqui com Y fixo em
            // 1, o que obrigava a luz a sempre viajar para BAIXO. Aquele numero explode quando Y
            // tende a zero, e era so por causa disso que existiam um teto de inclinacao
            // (MaxSunSlope) e uma altura minima do sol (MinSunHeight). Sem ele, o sol da a volta
            // inteira: o DDA do shader anda em qualquer direcao.

            material.SetShaderParameter("dados_mapa", overlay.Texture);

            // A mesma textura, para o shader ler o ambiente com filtro linear.

            material.SetShaderParameter("grade", new Vector2(largura, altura));
            material.SetShaderParameter("direcao", direcao);
            material.SetShaderParameter("piso_ar", 1f - Mathf.Clamp(ajustes.AirShadowOpacity, 0f, 1f));
            material.SetShaderParameter("ar_ligado", ajustes.AirShadowEnabled);
            // A propriedade e a DISTANCIA da fonte, de 0 a 1; o shader quer a meia-abertura do
            // cone em radianos. Fonte encostada ve o angulo maximo, fonte no infinito ve zero.
            // A conversao e linear na TANGENTE, e nao no angulo, porque a largura da faixa e
            // proporcional a tangente - assim o slider anda linear no que se ve.
            var aberturaMaxima = Mathf.Tan(Mathf.DegToRad(LightMapConstants.PENUMBRA_MAX_DEGREES));
            var distancia = Mathf.Clamp(ajustes.Penumbra, 0f, 1f);

            // O alcance vai de 0 a 1 e vira a abertura do leque, ate o semicirculo.
            material.SetShaderParameter("influencia_ambiente", Mathf.Clamp(ajustes.AmbientInfluence, 0f, 1f));

            material.SetShaderParameter("penumbra", Mathf.Atan(aberturaMaxima * (1f - distancia)));
        }

        // Em jogo a janela cobre o que a camera enxerga, com margem para o oclusor de fora da tela
        // ainda projetar sombra dentro dela. No editor nao ha camera ativa, e o tamanho vem do
        // inspetor.
        //
        // A margem sai da INCLINACAO DO SOL, e nao de um numero fixo. O raio anda
        // inclinacao celulas de lado por celula de altura, entao para uma sombra atravessar a area
        // visivel inteira quem a projeta pode estar ate inclinacao * altura_visivel colunas fora da
        // tela. Com 12 fixo, sol a -45 graus precisaria de 27 e a sombra cortava na borda.
        //
        // Teto na propria altura visivel: acima disso a janela vira mais margem que conteudo, e o
        // recalculo cresce sem a sombra ficar melhor.
        private void DimensionarJanela(Vector2I celula, Vector2 direcaoDoSol, out int largura, out int altura)
        {
            if (!Engine.IsEditorHint() && _camera is Camera2D camera)
            {
                var vista = camera.GetViewportRect().Size / camera.Zoom;

                var visivelX = Mathf.CeilToInt(vista.X / celula.X);
                var visivelY = Mathf.CeilToInt(vista.Y / celula.Y);

                var inclinacao = Mathf.Abs(direcaoDoSol.Y) < 1e-3f
                    ? float.MaxValue
                    : Mathf.Abs(direcaoDoSol.X / direcaoDoSol.Y);

                var margem = Mathf.Clamp(Mathf.CeilToInt(inclinacao * visivelY), 2, visivelY);

                largura = visivelX + margem * 2;
                altura = visivelY + margem * 2;

                return;
            }

            largura = Mathf.Max(1, PreviewSize.X);
            altura = Mathf.Max(1, PreviewSize.Y);
        }

        // Nunca a posicao deste no: em jogo quem diz o que precisa estar iluminado e a camera.
        private Vector2 PosicaoDoCentro()
        {
            if (_camera != null && IsInstanceValid(_camera))
            {
                return _camera.GlobalPosition;
            }

            return GlobalPosition;
        }

        #endregion
    }
}
