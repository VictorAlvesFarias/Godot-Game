using Godot;

namespace Jogo25D.Light
{
    // Cobertura das entidades em espaco de mundo (1 texel por pixel). O overlay de luz
    // so conhece os tiles pela TerrainLightMask; sem esta mascara, personagem, itens e
    // projeteis na frente do ar nunca recebiam a luz nem a escuridao do mundo.
    public sealed class EntityLightMask
    {
        #region Constructors

        public EntityLightMask(Node parent)
        {
            _viewport = new SubViewport
            {
                Name = "EntityLightMask",
                Disable3D = true,
                TransparentBg = true,
                CanvasCullMask = LayerBit,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                World2D = parent.GetViewport().World2D,
            };

            parent.AddChild(_viewport);
        }

        #endregion

        #region Dinamic properties

        // Camada de visibilidade 20: so as entidades (e seus ancestrais) sao desenhadas aqui.
        public const uint LayerBit = 1u << 19;

        private const int Margin = 32;

        public Vector2 Origin { get; private set; }
        public Vector2 Size { get; private set; }

        private readonly SubViewport _viewport;

        private Node2D _level;
        private Node2D _entities;

        #endregion

        #region Core - Atualizacao

        public void Update(Node2D level, Node2D entities)
        {
            if (entities != _entities || level != _level)
            {
                _level = level;
                _entities = entities;

                if (_level != null)
                {
                    _level.VisibilityLayer |= LayerBit;
                }

                TagTree(_entities);
            }

            var viewport = _viewport.GetParent<Node>().GetViewport();
            var visible = viewport.GetCanvasTransform().AffineInverse() * viewport.GetVisibleRect();
            var origin = (visible.Position - new Vector2(Margin, Margin)).Floor();
            var size = new Vector2I(
                Mathf.CeilToInt(visible.Size.X) + Margin * 2,
                Mathf.CeilToInt(visible.Size.Y) + Margin * 2);

            if (_viewport.Size != size)
            {
                _viewport.Size = size;
            }

            _viewport.CanvasTransform = new Transform2D(0, -origin);

            Origin = origin;
            Size = size;
        }

        public void Bind(ShaderMaterial material, bool enabled)
        {
            if (material == null)
            {
                return;
            }

            material.SetShaderParameter("entity_mask", _viewport.GetTexture());
            material.SetShaderParameter("entity_mask_origin", Origin);
            material.SetShaderParameter("entity_mask_size", Size);
            material.SetShaderParameter("entity_mask_ready", enabled);
        }

        #endregion

        #region Core - Camadas

        public void Tag(Node node)
        {
            if (_entities != null && GodotObject.IsInstanceValid(_entities) && _entities.IsAncestorOf(node))
            {
                TagNode(node);
            }
        }

        private static void TagTree(Node root)
        {
            if (root == null || !GodotObject.IsInstanceValid(root))
            {
                return;
            }

            TagNode(root);

            foreach (var child in root.GetChildren())
            {
                TagTree(child);
            }
        }

        // Controls (nome, vida, texto flutuante) ficam fora: sao interface, nao recebem luz.
        private static void TagNode(Node node)
        {
            if (node is CanvasItem item and not Control)
            {
                item.VisibilityLayer |= LayerBit;
            }
        }

        #endregion

        #region Core - Descarte

        public void Dispose()
        {
            if (GodotObject.IsInstanceValid(_viewport))
            {
                _viewport.QueueFree();
            }
        }

        #endregion
    }
}
