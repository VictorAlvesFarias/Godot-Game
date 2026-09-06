using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Light
{
    [Tool]
    public partial class LightMap2D : Node2D
    {
        [ExportCategory("Light Map")]
        [Export] public bool LightMapEnabled { get; set; } = true;
        [Export] public bool PreviewInEditor { get; set; } = true;
        [Export] public Vector2I PreviewSize { get; set; } = new(120, 80);
        [Export] public Godot.Collections.Array<NodePath> Layers { get; set; } = new();
        [Export] public NodePath Camera { get; set; } = new("");
        [Export(PropertyHint.ResourceType, "LightMapData")] public Resource Settings { get; set; }

        private readonly LightMapComputer _geometria = new();
        private readonly System.Collections.Generic.List<TileMapLayer> _camadas = new();

        private LightMapData _padroes;
        private LightMapData _copia;
        private Node2D _camera;
        private Sprite2D _overlay;
        private SubViewport _gpuPass;
        private ColorRect _passRect;
        private ImageTexture _dadosTextura;

        private Vector2I _ultimaOrigem;
        private bool _temDados;
        private bool _sujo = true;

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

        private LightMapData Copiar(Resource bruto)
        {
            _copia ??= new LightMapData();
            LightMapData padrao = _padroes ??= new LightMapData();

            _copia.ShowRawMap = LerBool(bruto, nameof(LightMapData.ShowRawMap), padrao.ShowRawMap);
            _copia.SunAngleDegrees = LerFloat(bruto, nameof(LightMapData.SunAngleDegrees), padrao.SunAngleDegrees);
            _copia.SunLightColor = LerColor(bruto, nameof(LightMapData.SunLightColor), padrao.SunLightColor);
            _copia.SunIntensity = LerFloat(bruto, nameof(LightMapData.SunIntensity), padrao.SunIntensity);
            _copia.Penumbra = LerFloat(bruto, nameof(LightMapData.Penumbra), padrao.Penumbra);
            _copia.SkyLightColor = LerColor(bruto, nameof(LightMapData.SkyLightColor), padrao.SkyLightColor);
            _copia.AmbientInfluence = LerFloat(bruto, nameof(LightMapData.AmbientInfluence), padrao.AmbientInfluence);
            _copia.AirShadowEnabled = LerBool(bruto, nameof(LightMapData.AirShadowEnabled), padrao.AirShadowEnabled);
            _copia.SolidShadowEnabled = LerBool(bruto, nameof(LightMapData.SolidShadowEnabled), padrao.SolidShadowEnabled);
            _copia.AirShadowOpacity = LerFloat(bruto, nameof(LightMapData.AirShadowOpacity), padrao.AirShadowOpacity);
            _copia.SolidShadowOpacity = LerFloat(bruto, nameof(LightMapData.SolidShadowOpacity), padrao.SolidShadowOpacity);
            _copia.MaterialAbsorption = LerFloat(bruto, nameof(LightMapData.MaterialAbsorption), padrao.MaterialAbsorption);

            return _copia;
        }

        private static float LerFloat(Resource bruto, string nome, float padrao)
        {
            Variant valor = bruto.Get(nome);
            return valor.VariantType == Variant.Type.Nil ? padrao : valor.AsSingle();
        }

        private static bool LerBool(Resource bruto, string nome, bool padrao)
        {
            Variant valor = bruto.Get(nome);
            return valor.VariantType == Variant.Type.Nil ? padrao : valor.AsBool();
        }

        private static Color LerColor(Resource bruto, string nome, Color padrao)
        {
            Variant valor = bruto.Get(nome);
            return valor.VariantType == Variant.Type.Nil ? padrao : valor.AsColor();
        }

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

            foreach (NodePath caminho in Layers)
            {
                if (caminho == null || caminho.IsEmpty)
                {
                    continue;
                }

                TileMapLayer apontada = GetNodeOrNull<TileMapLayer>(caminho);

                if (apontada != null)
                {
                    _camadas.Add(apontada);
                }
            }

            if (_camadas.Count > 0 || GetParent() is not Node parent)
            {
                return;
            }

            foreach (Node filho in parent.GetChildren())
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
                Node2D apontada = GetNodeOrNull<Node2D>(Camera);

                if (apontada != null)
                {
                    return apontada;
                }
            }

            return GetParent()?.GetNodeOrNull<Camera2D>("Camera2D");
        }

        private void ResolverPipeline()
        {
            if (_gpuPass == null || !IsInstanceValid(_gpuPass))
            {
                _gpuPass = GetNodeOrNull<SubViewport>(LightMapConstants.VIEWPORT_NODE_NAME);
            }

            if (_gpuPass == null)
            {
                _gpuPass = new SubViewport
                {
                    Name = LightMapConstants.VIEWPORT_NODE_NAME,
                };

                AddChild(_gpuPass);
            }

            _gpuPass.Disable3D = true;
            _gpuPass.TransparentBg = true;
            _gpuPass.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;

            if (_passRect == null || !IsInstanceValid(_passRect))
            {
                _passRect = _gpuPass.GetNodeOrNull<ColorRect>(LightMapConstants.PASS_NODE_NAME);
            }

            if (_passRect == null)
            {
                _passRect = new ColorRect
                {
                    Name = LightMapConstants.PASS_NODE_NAME,
                };

                _gpuPass.AddChild(_passRect);
            }

            _passRect.Position = Vector2.Zero;
            _passRect.Color = Colors.White;

            AplicarShader(ref _passRect, LightMapConstants.SHADER_PATH);

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
                    TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                };

                AddChild(_overlay);
            }

            AplicarShader(ref _overlay, LightMapConstants.PRESENT_SHADER_PATH);

            _overlay.Texture = _gpuPass.GetTexture();
            _overlay.Visible = _overlay.Material is ShaderMaterial;
        }

        private static void AplicarShader<T>(ref T item, string caminho) where T : CanvasItem
        {
            Shader shader = GD.Load<Shader>(caminho);

            if (shader == null)
            {
                item.Material = null;
                return;
            }

            if (item.Material is ShaderMaterial material && material.Shader == shader)
            {
                return;
            }

            item.Material = new ShaderMaterial { Shader = shader };
        }

        private void EsconderOverlay()
        {
            if (_overlay != null && IsInstanceValid(_overlay))
            {
                _overlay.Visible = false;
            }
        }

        private void Atualizar()
        {
            if (_camadas.Count == 0 || !IsInstanceValid(_camadas[0]) || _camadas[0].TileSet == null)
            {
                EsconderOverlay();
                return;
            }

            ResolverPipeline();

            if (_overlay?.Material is not ShaderMaterial)
            {
                EsconderOverlay();
                return;
            }

            _overlay.Visible = true;

            TileMapLayer grid = _camadas[0];
            Vector2I celula = grid.TileSet.TileSize;
            LightMapData ajustes = Ajustes;
            Vector2 direcaoDoSol = LightMapComputer.DirecaoDaLuz(RotacaoDoSol(ajustes)).Normalized();

            DimensionarJanela(celula, direcaoDoSol, out int largura, out int altura);

            Vector2I centro = grid.LocalToMap(grid.ToLocal(PosicaoDoCentro()));
            Vector2I origem = new(centro.X - largura / 2, centro.Y - altura / 2);

            Vector2I resolucao = new Vector2I(largura, altura) * LightMapConstants.SUBDIVISIONS;

            if (_gpuPass.Size != resolucao)
            {
                _gpuPass.Size = resolucao;
                _passRect.Size = resolucao;
                _temDados = false;
            }

            bool precisaDados = Engine.IsEditorHint() || _sujo || !_temDados || origem != _ultimaOrigem;

            if (precisaDados)
            {
                AtualizarDados(grid, origem, largura, altura);
            }

            if (!_temDados)
            {
                EsconderOverlay();
                return;
            }

            AlimentarShader(ajustes, direcaoDoSol, largura, altura);

            _overlay.GlobalPosition = grid.ToGlobal(grid.MapToLocal(origem) - (Vector2)celula / 2f);
            _overlay.Scale = (Vector2)celula / LightMapConstants.SUBDIVISIONS;
            _gpuPass.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        }

        private void AtualizarDados(TileMapLayer grid, Vector2I origem, int largura, int altura)
        {
            _ultimaOrigem = origem;
            _sujo = false;

            _geometria.Redimensionar(largura, altura);
            _geometria.PreencherGrade(_camadas, origem);

            Image imagem = _geometria.Calcular();

            if (imagem == null)
            {
                _temDados = false;
                return;
            }

            if (_dadosTextura != null && _dadosTextura.GetSize() == new Vector2(largura, altura))
            {
                _dadosTextura.Update(imagem);
            }
            else
            {
                _dadosTextura = ImageTexture.CreateFromImage(imagem);
            }

            _temDados = true;
        }

        private void AlimentarShader(LightMapData ajustes, Vector2 direcao, int largura, int altura)
        {
            if (_dadosTextura == null || _passRect.Material is not ShaderMaterial passe || _overlay.Material is not ShaderMaterial apresentacao)
            {
                return;
            }

            float aberturaMaxima = Mathf.Tan(Mathf.DegToRad(LightMapConstants.PENUMBRA_MAX_DEGREES));
            float distancia = Mathf.Clamp(ajustes.Penumbra, 0f, 1f);
            Vector2 grade = new(largura, altura);

            passe.SetShaderParameter("dados_mapa", _dadosTextura);
            passe.SetShaderParameter("grade", grade);
            passe.SetShaderParameter("direcao_sol", direcao);
            passe.SetShaderParameter("abertura_sol", Mathf.Atan(aberturaMaxima * (1f - distancia)));
            passe.SetShaderParameter("cor_sol", ajustes.SunLightColor);
            passe.SetShaderParameter("cor_ceu", ajustes.SkyLightColor);
            passe.SetShaderParameter("intensidade_sol", Mathf.Max(0f, ajustes.SunIntensity));
            passe.SetShaderParameter("influencia_ambiente", Mathf.Clamp(ajustes.AmbientInfluence, 0f, 1f));
            passe.SetShaderParameter("ar_ligado", ajustes.AirShadowEnabled);
            passe.SetShaderParameter("materia_ligada", ajustes.SolidShadowEnabled);
            passe.SetShaderParameter("absorcao_materia", Mathf.Max(0f, ajustes.MaterialAbsorption));
            passe.SetShaderParameter("alcance_sol", LightMapConstants.SUN_RANGE_CELLS);
            passe.SetShaderParameter("alcance_ceu", LightMapConstants.SKY_RANGE_CELLS);
            passe.SetShaderParameter("mostrar_mapa_cru", ajustes.ShowRawMap);
            // O piso da sombra fica na apresentacao para acompanhar a borda real do tile.
            apresentacao.SetShaderParameter("dados_mapa", _dadosTextura);
            apresentacao.SetShaderParameter("grade", grade);
            apresentacao.SetShaderParameter("piso_ar", 1f - Mathf.Clamp(ajustes.AirShadowOpacity, 0f, 1f));
            apresentacao.SetShaderParameter("piso_materia", 1f - Mathf.Clamp(ajustes.SolidShadowOpacity, 0f, 1f));
            apresentacao.SetShaderParameter("mostrar_mapa_cru", ajustes.ShowRawMap);
        }

        private void DimensionarJanela(Vector2I celula, Vector2 direcaoDoSol, out int largura, out int altura)
        {
            if (!Engine.IsEditorHint() && _camera is Camera2D camera)
            {
                Vector2 vista = camera.GetViewportRect().Size / camera.Zoom;

                int visivelX = Mathf.CeilToInt(vista.X / celula.X);
                int visivelY = Mathf.CeilToInt(vista.Y / celula.Y);

                float inclinacao = Mathf.Abs(direcaoDoSol.Y) < 1e-3f
                    ? visivelY
                    : Mathf.Abs(direcaoDoSol.X / direcaoDoSol.Y);

                int margem = Mathf.Clamp(Mathf.CeilToInt(inclinacao * visivelY), 4, visivelY);

                largura = visivelX + margem * 2;
                altura = visivelY + margem * 2;
                return;
            }

            largura = Mathf.Max(1, PreviewSize.X);
            altura = Mathf.Max(1, PreviewSize.Y);
        }

        private Vector2 PosicaoDoCentro()
        {
            return _camera != null && IsInstanceValid(_camera) ? _camera.GlobalPosition : GlobalPosition;
        }
    }
}
