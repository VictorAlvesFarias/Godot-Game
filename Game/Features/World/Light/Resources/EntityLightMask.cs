using Godot;

namespace Jogo25D.Light
{
    // Cobertura das entidades no mesmo espaco da tela do viewport da dimensao, lida no overlay
    // por SCREEN_UV como a cor da cena. O overlay so conhece os tiles pela TerrainLightMask;
    // sem esta mascara, personagem, itens e projeteis na frente do ar nunca recebiam a luz nem
    // a escuridao do mundo. Em espaco de mundo a mascara arredondava a posicao das entidades
    // e a diferenca virava uma borda iluminando o fundo em volta delas.
    public sealed class EntityLightMask
    {
        #region Constructors

        public EntityLightMask(Node parent)
        {
            _source = parent.GetViewport();
            _viewport = new SubViewport
            {
                Name = "EntityLightMask",
                Disable3D = true,
                TransparentBg = true,
                CanvasCullMask = LayerBit,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                World2D = _source.World2D,
                Snap2DTransformsToPixel = _source.Snap2DTransformsToPixel,
                Snap2DVerticesToPixel = _source.Snap2DVerticesToPixel,
                CanvasItemDefaultTextureFilter = _source.CanvasItemDefaultTextureFilter,
            };

            parent.AddChild(_viewport);
        }

        #endregion

        #region Dinamic properties

        // Camada de visibilidade 20: so as entidades (e seus ancestrais) sao desenhadas aqui.
        public const uint LayerBit = 1u << 19;

        private readonly Viewport _source;
        private readonly SubViewport _viewport;

        private Node2D _level;
        private Node2D _entities;

        #endregion

        #region Core - Atualizacao

        public void Track(Node2D level, Node2D entities)
        {
            if (entities == _entities && level == _level)
            {
                return;
            }

            _level = level;
            _entities = entities;

            Retag();
        }

        public void Retag()
        {
            if (GodotObject.IsInstanceValid(_level))
            {
                _level.VisibilityLayer |= LayerBit;
            }

            SetTree(_entities, true);
        }

        public void Untag()
        {
            if (GodotObject.IsInstanceValid(_level))
            {
                _level.VisibilityLayer &= ~LayerBit;
            }

            SetTree(_entities, false);
        }

        // Chamado logo antes do desenho (FramePreDraw), com camera e entidades ja na posicao final
        // do frame: tamanho e transformacao iguais aos do viewport da dimensao, pixel a pixel.
        public void Sync()
        {
            if (!GodotObject.IsInstanceValid(_source) || !GodotObject.IsInstanceValid(_viewport))
            {
                return;
            }

            // Alvo de render real: na janela raiz com stretch canvas_items a textura informa o
            // tamanho antes do stretch, e o stretch vem em GetFinalTransform.
            var size = _source is Window window ? window.Size : ((SubViewport)_source).Size;

            if (_viewport.Size != size)
            {
                _viewport.Size = size;
            }

            // GetFinalTransform ja inclui o GlobalCanvasTransform (zoom/pan do editor) e o stretch.
            _viewport.GlobalCanvasTransform = _source.GetFinalTransform();
            _viewport.CanvasTransform = _source.CanvasTransform;
        }

        public void Bind(ShaderMaterial material, bool enabled)
        {
            if (material == null)
            {
                return;
            }

            material.SetShaderParameter("entity_mask", _viewport.GetTexture());
            material.SetShaderParameter("entity_mask_ready", enabled);
        }

        #endregion

        #region Core - Camadas

        public void Tag(Node node)
        {
            if (_entities != null && GodotObject.IsInstanceValid(_entities) && _entities.IsAncestorOf(node))
            {
                SetNode(node, true);
            }
        }

        private static void SetTree(Node root, bool tagged)
        {
            if (root == null || !GodotObject.IsInstanceValid(root))
            {
                return;
            }

            SetNode(root, tagged);

            foreach (var child in root.GetChildren())
            {
                SetTree(child, tagged);
            }
        }

        // Controls (nome, vida, texto flutuante) ficam fora: sao interface, nao recebem luz.
        private static void SetNode(Node node, bool tagged)
        {
            if (node is not CanvasItem item || node is Control)
            {
                return;
            }

            item.VisibilityLayer = tagged ? item.VisibilityLayer | LayerBit : item.VisibilityLayer & ~LayerBit;
        }

        #endregion

        #region Core - Descarte

        public void Dispose()
        {
            Untag();

            if (GodotObject.IsInstanceValid(_viewport))
            {
                _viewport.QueueFree();
            }
        }

        #endregion
    }
}
