using Godot;
using Jogo25D.Biomes;
using Jogo25D.Blocks;
using Jogo25D.Constants;
using System;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    [Tool]
    public partial class LightingEditorPreview : Node2D
    {
        #region Dinamic properties

        private const long MaxPreviewCells = 500 * 500;

        [Export]
        public bool Enabled { get; set; } = true;

        [Export]
        public bool IncludeSkylight { get; set; } = true;

        [Export]
        public NodePath ComposeLayerPath { get; set; } = new NodePath("../Compose");

        [Export]
        public NodePath BaseLayerPath { get; set; } = new NodePath("../Base");

        [Export(PropertyHint.Range, "8,64,1")]
        public int Padding { get; set; } = 16;

        [Export(PropertyHint.Range, "0.1,3.0,0.1")]
        public float RefreshIntervalSeconds { get; set; } = 0.5f;

        public long RebuildCount { get; private set; }

        private readonly HashSet<TileMapLayer> _watched = new();
        private readonly HashSet<TileSet> _watchedSets = new();

        private Dictionary<(int SourceId, Vector2I AtlasCoord), BlockDefinition> _lightEmittingBlocks;
        private LightPropagationDispatcher _propagation;
        private LightTextureOutput _output;
        private ImageTexture _maskTexture;
        private Rect2I _maskBounds;
        private double _timer;
        private int _lifecycle;
        private int _visualState;
        private int _maskVersion;
        private int _publishedMaskVersion = -1;
        private int _maskVisualState;
        private bool _building;
        private bool _warnedAboutSize;
        private bool _dirty = true;

        #endregion

        #region Node children references

        private TerrainLayer _layer;
        private TerrainLayer _baseLayer;
        private TileMapLayer _walls;
        private Sprite2D _sprite;

        #endregion

        #region Godot implementation

        public override void _EnterTree()
        {
            _lifecycle++;
            _propagation = new LightPropagationDispatcher();
            _building = false;
            _dirty = true;
            _publishedMaskVersion = -1;
        }

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

        public override void _Process(double delta)
        {
            if (!Engine.IsEditorHint())
            {
                return;
            }

            if (!Enabled)
            {
                SetPreviewVisible(false);

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

            var state = VisualState();

            if (state != _visualState)
            {
                _visualState = state;
                _dirty = true;
            }

            if (!_dirty)
            {
                SetPreviewVisible(true);

                return;
            }

            if (_building)
            {
                return;
            }

            _dirty = false;

            Rebuild();
        }

        public override void _ExitTree()
        {
            foreach (var layer in _watched)
            {
                if (IsInstanceValid(layer))
                {
                    layer.Changed -= MarkDirty;
                }
            }

            foreach (var set in _watchedSets)
            {
                if (IsInstanceValid(set))
                {
                    set.Changed -= MarkDirty;
                }
            }

            _lifecycle++;

            _watched.Clear();
            _watchedSets.Clear();

            _propagation?.Dispose();
            _propagation = null;
            _building = false;
            _dirty = true;

            _output?.Dispose();
            _output = null;
        }

        #endregion

        #region Core - Invalidacao

        private void MarkDirty()
        {
            _dirty = true;
            _maskVersion++;
        }

        private void Watch(TileMapLayer layer)
        {
            if (layer == null)
            {
                return;
            }

            if (_watched.Add(layer))
            {
                layer.Changed += MarkDirty;
            }

            if (layer.TileSet != null && _watchedSets.Add(layer.TileSet))
            {
                layer.TileSet.Changed += MarkDirty;
            }
        }

        private int VisualState(bool maskOnly = false)
        {
            var hash = new HashCode();

            if (!maskOnly)
            {
                hash.Add(IncludeSkylight);
                hash.Add(SceneLightSources.Revision(_layer?.GetParent()));
            }

            hash.Add(Padding);
            hash.Add(GlobalTransform);

            foreach (var layer in new TileMapLayer[] { _layer, _baseLayer, _walls })
            {
                if (layer == null)
                {
                    continue;
                }

                hash.Add(EditorTileRevision.Get(layer));
                hash.Add(layer.GetInstanceId());
                hash.Add(layer.GlobalTransform);
                hash.Add(layer.Modulate);
                hash.Add(layer.SelfModulate);
                hash.Add(layer.Enabled);
                hash.Add(layer.IsVisibleInTree());
            }

            return hash.ToHashCode();
        }

        #endregion

        #region Core - Referencias

        private void ResolveReferences()
        {
            var level = GetParent() is LightMap2D ? GetParent().GetParent() : GetParent();

            _walls = level?.GetNodeOrNull<BackgroundWallLayer>("BackgroundWalls");
            _layer = GetNodeOrNull<TerrainLayer>(ComposeLayerPath);
            _baseLayer = GetNodeOrNull<TerrainLayer>(BaseLayerPath);

            Watch(_layer);
            Watch(_baseLayer);
            Watch(_walls);
        }

        #endregion

        #region Core - Apresentacao

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
                Material = new ShaderMaterial
                {
                    Shader = GD.Load<Shader>("res://Assets/Shaders/terrain_light_overlay.gdshader")
                },
            };

            AddChild(_sprite);
        }

        private void SetPreviewVisible(bool visible)
        {
            if (_sprite != null)
            {
                _sprite.Visible = visible;
            }
        }

        #endregion

        #region Core - Propagacao

        private async void Rebuild()
        {
            _lightEmittingBlocks ??= LightSourceScanner.BuildLightEmittingBlockIndex();

            if (_sprite == null || !IsInstanceValid(_sprite))
            {
                EnsureSprite();
            }

            var used = UsedRect();

            if (used.Size.X <= 0 || used.Size.Y <= 0)
            {
                SetPreviewVisible(false);

                return;
            }

            SetPreviewVisible(true);

            var region = new Rect2I(
                used.Position - new Vector2I(Padding, Padding),
                used.Size + new Vector2I(Padding * 2, Padding * 2));

            if ((long)region.Size.X * region.Size.Y > MaxPreviewCells)
            {
                WarnAboutSize(region);
                SetPreviewVisible(false);

                return;
            }

            bool IsSolid(Vector2I cell)
            {
                return _layer.GetCellSourceId(cell) != -1 || (_baseLayer != null && _baseLayer.GetCellSourceId(cell) != -1);
            }

            var sources = LightSourceScanner.CollectSources(
                _layer,
                _baseLayer,
                region,
                _lightEmittingBlocks,
                IsSolid,
                includeSkylight: false,
                hasBackground: cell => _walls != null && _walls.GetCellSourceId(cell) != -1);

            _propagation ??= new LightPropagationDispatcher();

            var lifecycle = _lifecycle;
            var requestedState = VisualState();
            var requestedMaskVersion = _maskVersion;

            _building = true;

            try
            {
                var output = await _propagation.ComputeTextureAsync(region, IsSolid, sources);

                if (!IsInstanceValid(this) || !IsInsideTree() || lifecycle != _lifecycle)
                {
                    output.Dispose();

                    return;
                }

                if (requestedState != VisualState() || requestedMaskVersion != _maskVersion)
                {
                    output.Dispose();

                    _dirty = true;

                    return;
                }

                Publish(region, output);
            }
            catch (ObjectDisposedException)
            {
                if (lifecycle == _lifecycle)
                {
                    _dirty = true;
                }
            }
            catch (Exception error)
            {
                GD.PushError(error.ToString());

                _dirty = true;
            }
            finally
            {
                if (lifecycle == _lifecycle)
                {
                    _building = false;
                }
            }
        }

        private void Publish(Rect2I region, LightTextureOutput output)
        {
            var tileSize = _layer.TileSet?.TileSize.X ?? ChunkStreamingConstants.REFERENCE_TILE_SIZE;
            var bounds = new Rect2I(region.Position * tileSize, region.Size * tileSize);
            var material = (ShaderMaterial)_sprite.Material;
            var maskState = VisualState(maskOnly: true);

            if (_maskTexture == null || _publishedMaskVersion != _maskVersion || _maskBounds != bounds || _maskVisualState != maskState)
            {
                using var mask = TerrainLightMask.Build(bounds, this, _baseLayer, _layer, _walls);

                TerrainLightMask.UpdateMaterial(material, ref _maskTexture, mask);

                _publishedMaskVersion = _maskVersion;
                _maskBounds = bounds;
                _maskVisualState = maskState;
            }

            material.SetShaderParameter("ambient_min", LightingConstants.AMBIENT_MIN);

            _sprite.Texture = output.Texture;
            _sprite.Position = new Vector2(region.Position.X * tileSize, region.Position.Y * tileSize);
            _sprite.Scale = new Vector2(tileSize, tileSize);

            _output?.Dispose();
            _output = output;

            RebuildCount++;
        }

        private Rect2I UsedRect()
        {
            var used = _layer.GetUsedRect();

            if (_baseLayer != null && IsInstanceValid(_baseLayer))
            {
                var baseUsed = _baseLayer.GetUsedRect();

                used = baseUsed.Size == Vector2I.Zero ? used : used.Merge(baseUsed);
            }

            if (_walls != null && _walls.GetUsedRect().Size != Vector2I.Zero)
            {
                used = used.Size == Vector2I.Zero ? _walls.GetUsedRect() : used.Merge(_walls.GetUsedRect());
            }

            return used;
        }

        private void WarnAboutSize(Rect2I region)
        {
            if (_warnedAboutSize)
            {
                return;
            }

            GD.PushWarning($"[LightingEditorPreview] Area pintada em '{Name}' e grande demais ({region.Size.X}x{region.Size.Y}) pro preview do editor - deixando sem luz aqui. O calculo em tempo de jogo (chunk a chunk) nao tem esse limite.");

            _warnedAboutSize = true;
        }

        #endregion
    }
}
