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
    // mapa. Ao meio-dia (angulo perto de 0) a inclinacao e quase zero e a sombra cai reta,
    // aterrissando no bloco logo abaixo: nesse horario nao ha projecao para ver.
    //
    // O calculo mora no LightMapComputer; este no cuida so das bordas - de onde vem a grade (as
    // camadas), onde fica a janela (a camera) e para onde vai a imagem (o overlay).
    //
    // A imagem vai num Sprite2D filho com TopLevel ligado, e nao no _Draw deste no: com
    // AngleSource = NodeRotation o no gira, e um desenho preso a ele giraria junto, saindo de
    // cima do terreno.
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
        private float _ultimaInclinacao;
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

            _copia.DiffuseEnabled = LerBool(bruto, nameof(LightMapData.DiffuseEnabled), padrao.DiffuseEnabled);
            _copia.SolidCost = LerInt(bruto, nameof(LightMapData.SolidCost), padrao.SolidCost);
            _copia.MaxLightLevel = LerInt(bruto, nameof(LightMapData.MaxLightLevel), padrao.MaxLightLevel);
            _copia.MinBrightness = LerFloat(bruto, nameof(LightMapData.MinBrightness), padrao.MinBrightness);
            _copia.ShowRawMap = LerBool(bruto, nameof(LightMapData.ShowRawMap), padrao.ShowRawMap);
            _copia.AmbientInfluence = LerFloat(bruto, nameof(LightMapData.AmbientInfluence), padrao.AmbientInfluence);
            _copia.ShadowSoftness = LerFloat(bruto, nameof(LightMapData.ShadowSoftness), padrao.ShadowSoftness);

            _copia.AngleSource = (SunAngleSource)LerInt(bruto, nameof(LightMapData.AngleSource), (int)padrao.AngleSource);
            _copia.SunAngleDegrees = LerFloat(bruto, nameof(LightMapData.SunAngleDegrees), padrao.SunAngleDegrees);
            _copia.MaxSunSlope = LerFloat(bruto, nameof(LightMapData.MaxSunSlope), padrao.MaxSunSlope);
            _copia.MinSunHeight = LerFloat(bruto, nameof(LightMapData.MinSunHeight), padrao.MinSunHeight);

            _copia.SunBlockStrength = LerFloat(bruto, nameof(LightMapData.SunBlockStrength), padrao.SunBlockStrength);
            _copia.TerrainShadowEnabled = LerBool(bruto, nameof(LightMapData.TerrainShadowEnabled), padrao.TerrainShadowEnabled);
            _copia.ShadowFloor = LerFloat(bruto, nameof(LightMapData.ShadowFloor), padrao.ShadowFloor);
            _copia.ShadowDepth = LerInt(bruto, nameof(LightMapData.ShadowDepth), padrao.ShadowDepth);
            _copia.AirShadowEnabled = LerBool(bruto, nameof(LightMapData.AirShadowEnabled), padrao.AirShadowEnabled);
            _copia.AirShadowStrength = LerFloat(bruto, nameof(LightMapData.AirShadowStrength), padrao.AirShadowStrength);

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

        // O angulo do sol, em radianos. Vem do numero do inspetor ou da rotacao do proprio no.
        private float RotacaoDoSol(LightMapData ajustes)
        {
            return ajustes.AngleSource == SunAngleSource.FixedAngle
                ? Mathf.DegToRad(ajustes.SunAngleDegrees)
                : GlobalRotation;
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

            DimensionarJanela(celula, out var largura, out var altura);

            var centro = grid.LocalToMap(grid.ToLocal(PosicaoDoCentro()));
            var origem = new Vector2I(centro.X - largura / 2, centro.Y - altura / 2);

            var ajustes = Ajustes;

            _calculadora.MaxSlope = ajustes.MaxSunSlope;
            _calculadora.MinSunHeight = ajustes.MinSunHeight;

            var inclinacao = _calculadora.InclinacaoPorLinha(RotacaoDoSol(ajustes));

            // No editor refaz sempre: nada avisa quando um tile e pintado, e um preview parado
            // enquanto se desenha o mapa nao serve para calibrar. Em jogo so refaz quando a
            // janela anda, o sol gira o bastante ou um bloco muda.
            var precisa = Engine.IsEditorHint()
                || _sujo
                || !_temResultado
                || origem != _ultimaOrigem
                || Mathf.Abs(inclinacao - _ultimaInclinacao) >= LightMapConstants.SUN_SLOPE_STEP;

            if (!precisa)
            {
                return;
            }

            _ultimaOrigem = origem;
            _ultimaInclinacao = inclinacao;
            _sujo = false;

            _calculadora.DiffuseEnabled = ajustes.DiffuseEnabled;
            _calculadora.MaxLevel = Mathf.Max(1, ajustes.MaxLightLevel);
            _calculadora.MinBrightness = ajustes.MinBrightness;
            _calculadora.SolidCost = ajustes.SolidCost;

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

            AlimentarShader(overlay, ajustes, inclinacao, largura, altura);

            // MapToLocal devolve o centro da celula; o sprite comeca no canto dela. Global porque
            // o overlay e TopLevel, e portanto nao herda a transformada deste no.
            overlay.GlobalPosition = grid.ToGlobal(grid.MapToLocal(origem) - (Vector2)celula / 2f);
            overlay.Scale = celula;

            _temResultado = true;
        }

        // Os uniformes que o shader consome. A CPU nao calcula sombra: ela entrega a grade e os
        // ajustes, e o raio e tracado por fragmento.
        private void AlimentarShader(Sprite2D overlay, LightMapData ajustes, float inclinacao, int largura, int altura)
        {
            if (overlay.Material is not ShaderMaterial material)
            {
                return;
            }

            // Direcao em que a luz VIAJA. O shader anda contra ela, rumo ao sol.
            var direcao = new Vector2(inclinacao, 1f).Normalized();

            material.SetShaderParameter("dados_mapa", overlay.Texture);

            // A mesma textura, para o shader ler o ambiente com filtro linear.
            material.SetShaderParameter("dados_suaves", overlay.Texture);

            material.SetShaderParameter("grade", new Vector2(largura, altura));
            material.SetShaderParameter("direcao", direcao);
            material.SetShaderParameter("passa", 1f - Mathf.Clamp(ajustes.SunBlockStrength, 0f, 1f));
            material.SetShaderParameter("piso_terreno", ajustes.ShadowFloor);
            material.SetShaderParameter("piso_ar", 1f - Mathf.Clamp(ajustes.AirShadowStrength, 0f, 1f));
            material.SetShaderParameter("profundidade", (float)ajustes.ShadowDepth);
            material.SetShaderParameter("terreno_ligado", ajustes.TerrainShadowEnabled);
            material.SetShaderParameter("ar_ligado", ajustes.AirShadowEnabled);
            material.SetShaderParameter("suavidade", Mathf.DegToRad(ajustes.ShadowSoftness));
            material.SetShaderParameter("influencia_ambiente", ajustes.AmbientInfluence);
            material.SetShaderParameter("corte", LightMapConstants.RAY_CUTOFF);
        }

        // Em jogo a janela cobre o que a camera enxerga, com margem para a luz de fora da tela ja
        // chegar propagada na borda. No editor nao ha camera ativa, e o tamanho vem do inspetor.
        private void DimensionarJanela(Vector2I celula, out int largura, out int altura)
        {
            if (!Engine.IsEditorHint() && _camera is Camera2D camera)
            {
                var vista = camera.GetViewportRect().Size / camera.Zoom;

                largura = Mathf.CeilToInt(vista.X / celula.X) + LightMapConstants.WINDOW_MARGIN * 2;
                altura = Mathf.CeilToInt(vista.Y / celula.Y) + LightMapConstants.WINDOW_MARGIN * 2;

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
