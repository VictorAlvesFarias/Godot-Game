using Godot;
using Jogo25D.Biomes;
using Jogo25D.Blocks;
using Jogo25D.Constants;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    // Preview da iluminacao direto no editor, sem precisar dar Play: [Tool] que recalcula a luz
    // sobre a area pintada nas TerrainLayer (GetUsedRect) e desenha o mesmo overlay multiplicativo
    // que o LightingManager usa em tempo de jogo. So processa quando Engine.IsEditorHint() e true -
    // em jogo de verdade quem cuida do overlay e o LightingManager (streaming de chunk, nao
    // "area pintada", que no jogo real e o mundo inteiro gerado proceduralmente).
    [Tool]
    public partial class LightingEditorPreview : Node2D
    {
        [Export] public bool Enabled { get; set; } = true;
        [Export] public bool IncludeSkylight { get; set; } = true;
        [Export] public NodePath ComposeLayerPath { get; set; } = new NodePath("../Compose");
        [Export] public NodePath BaseLayerPath { get; set; } = new NodePath("../Base");
        [Export(PropertyHint.Range, "8,64,1")] public int Padding { get; set; } = 16;
        [Export(PropertyHint.Range, "0.1,3.0,0.1")] public float RefreshIntervalSeconds { get; set; } = 0.5f;

        private const long MaxPreviewCells = 500 * 500;

        private TerrainLayer _layer;
        private TerrainLayer _baseLayer;
        private TileMapLayer _walls;
        private Sprite2D _sprite;
        private ImageTexture _texture;
        private ImageTexture _maskTexture;
        private Dictionary<(int SourceId, Vector2I AtlasCoord), BlockDefinition> _lightEmittingBlocks;
        private double _timer;
        private readonly LightPropagationDispatcher _propagacao = new();
        private bool _warnedAboutSize;

        public override void _Ready()
        {
            if (!Engine.IsEditorHint())
            {
                return;
            }

            _lightEmittingBlocks = LightSourceScanner.BuildLightEmittingBlockIndex();

            ResolveReferences();
            EnsureSprite();
            SetProcess(true);
        }

        public override void _ExitTree()
        {
            _propagacao.Dispose();
        }

        public override void _Process(double delta)
        {
            if (!Engine.IsEditorHint())
            {
                return;
            }

            if (!Enabled)
            {
                if (_sprite != null)
                {
                    _sprite.Visible = false;
                }

                return;
            }

            _timer += delta;

            if (_timer < RefreshIntervalSeconds)
            {
                return;
            }

            _timer = 0;

            ResolveReferences();

            if (_layer == null || !IsInstanceValid(_layer))
            {
                return;
            }

            Rebuild();
        }

        private void ResolveReferences()
        {
            _walls = GetParent()?.GetNodeOrNull<BackgroundWallLayer>("BackgroundWalls");
            if (_layer == null || !IsInstanceValid(_layer))
            {
                _layer = GetNodeOrNull<TerrainLayer>(ComposeLayerPath);
            }

            if (_baseLayer == null || !IsInstanceValid(_baseLayer))
            {
                _baseLayer = GetNodeOrNull<TerrainLayer>(BaseLayerPath);
            }
        }

        private void EnsureSprite()
        {
            _sprite = GetNodeOrNull<Sprite2D>("PreviewSprite");

            if (_sprite != null)
            {
                return;
            }

            _sprite = new Sprite2D
            {
                Name = "PreviewSprite",
                Centered = false,
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Features/World/Light/Resources/TerrainLightOverlay.gdshader") },
            };

            AddChild(_sprite);

            if (GetTree()?.EditedSceneRoot != null)
            {
                _sprite.Owner = GetTree().EditedSceneRoot;
            }
        }

        private void Rebuild()
        {
            // O editor recria a instancia C# deste [Tool] quando recompila o assembly, e nem sempre
            // chama _Ready de novo - mas o _Process volta a rodar. Sem isto, o indice de emissores e
            // o sprite ficam nulos e o rebuild estoura toda frame.
            _lightEmittingBlocks ??= LightSourceScanner.BuildLightEmittingBlockIndex();

            if (_sprite == null || !IsInstanceValid(_sprite))
            {
                EnsureSprite();
            }

            var used = _layer.GetUsedRect();

            if (_baseLayer != null && IsInstanceValid(_baseLayer))
            {
                var baseUsed = _baseLayer.GetUsedRect();

                used = baseUsed.Size == Vector2I.Zero ? used : used.Merge(baseUsed);
            }

            if (_walls != null && _walls.GetUsedRect().Size != Vector2I.Zero)
                used = used.Size == Vector2I.Zero ? _walls.GetUsedRect() : used.Merge(_walls.GetUsedRect());

            if (used.Size.X <= 0 || used.Size.Y <= 0)
            {
                _sprite.Visible = false;

                return;
            }

            _sprite.Visible = true;

            var region = new Rect2I(
                used.Position - new Vector2I(Padding, Padding),
                used.Size + new Vector2I(Padding * 2, Padding * 2));

            if ((long)region.Size.X * region.Size.Y > MaxPreviewCells)
            {
                if (!_warnedAboutSize)
                {
                    GD.PushWarning($"[LightingEditorPreview] Area pintada em '{Name}' e grande demais ({region.Size.X}x{region.Size.Y}) pro preview do editor - deixando sem luz aqui. O calculo em tempo de jogo (chunk a chunk) nao tem esse limite.");
                    _warnedAboutSize = true;
                }

                _sprite.Visible = false;

                return;
            }

            bool IsSolid(Vector2I cell)
            {
                return _layer.GetCellSourceId(cell) != -1 || (_baseLayer != null && _baseLayer.GetCellSourceId(cell) != -1);
            }

            var sources = LightSourceScanner.CollectSources(_layer, _baseLayer, region, _lightEmittingBlocks, IsSolid, IncludeSkylight);
            var grid = _propagacao.Compute(region, IsSolid, sources);

            var image = Image.CreateEmpty(region.Size.X, region.Size.Y, false, Image.Format.Rgba8);

            for (var x = 0; x < region.Size.X; x++)
            {
                for (var y = 0; y < region.Size.Y; y++)
                {
                    var value = grid[x, y];

                    image.SetPixel(x, y, new Color(
                        Mathf.Max(value.R, LightingConstants.AMBIENT_MIN),
                        Mathf.Max(value.G, LightingConstants.AMBIENT_MIN),
                        Mathf.Max(value.B, LightingConstants.AMBIENT_MIN)));
                }
            }

            var tileSize = _layer.TileSet?.TileSize.X ?? ChunkStreamingConstants.REFERENCE_TILE_SIZE;
            using var mask = TerrainLightMask.Build(new Rect2I(region.Position * tileSize, region.Size * tileSize), this, _baseLayer, _layer, _walls);
            TerrainLightMask.UpdateMaterial((ShaderMaterial)_sprite.Material, ref _maskTexture, mask);

            if (_texture == null || _texture.GetSize() != image.GetSize())
            {
                _texture = ImageTexture.CreateFromImage(image);
                _sprite.Texture = _texture;
            }
            else
            {
                _texture.Update(image);
            }

            _sprite.Position = new Vector2(region.Position.X * tileSize, region.Position.Y * tileSize);
            _sprite.Scale = new Vector2(tileSize, tileSize);
        }
    }
}
