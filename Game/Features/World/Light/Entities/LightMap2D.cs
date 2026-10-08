using Godot;
using Jogo25D.Biomes;
using Jogo25D.Blocks;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Entities;
using Jogo25D.Utils.Coordinates;
using System;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    [Tool]
    public partial class LightMap2D : Node2D
    {
        #region Dinamic properties

        private const double EditorPollSeconds = 0.25;
        private const double GeometryBudgetMsec = 3;
        private const int RegionMarginChunks = 3;
        private const int OverlayZIndex = 900;

        [Export]
        public bool LightMapEnabled { get; set; } = true;

        [Export]
        public bool PreviewInEditor { get; set; } = true;

        [Export]
        public Vector2I PreviewSize { get; set; } = new(120, 80);

        [Export]
        public Godot.Collections.Array<NodePath> Layers { get; set; } = new();

        [Export]
        public NodePath Camera { get; set; } = new("");

        [Export(PropertyHint.ResourceType, "LightMapData")]
        public Resource Settings { get; set; }

        [ExportGroup("Preview do mapa de luz")]
        [Export]
        public bool IncludeSkylight { get; set; } = true;

        [Export]
        public NodePath ComposeLayerPath { get; set; } = new("../Compose");

        [Export]
        public NodePath BaseLayerPath { get; set; } = new("../Base");

        [Export(PropertyHint.Range, "8,64,1")]
        public int PreviewPadding { get; set; } = 16;

        [Export(PropertyHint.Range, "0.1,3,0.1")]
        public float PreviewRefreshIntervalSeconds { get; set; } = 0.5f;

        public LightingEditorPreview EditorPreview { get; private set; }

        public string DimensionId => (GetParent() as Dimension)?.DimensionId;

        private TerrainLayer ComposeLayer => GetNodeOrNull<TerrainLayer>(ComposeLayerPath);
        private TerrainLayer BaseLayer => GetNodeOrNull<TerrainLayer>(BaseLayerPath);
        private TileMapLayer WallLayer => GetParent()?.GetNodeOrNull<TileMapLayer>("BackgroundWalls");
        public LogicalLightWorld World { get; private set; }

        private static readonly LightPropagationDispatcher Propagation = new();

        private static Dictionary<(int SourceId, Vector2I AtlasCoord), BlockDefinition> _lightEmittingBlocks;

        private readonly HashSet<Vector2I> _loadedChunks = new();
        private readonly HashSet<Vector2I> _dirtyChunks = new();
        private readonly HashSet<Vector2I> _inFlightChunks = new();
        private readonly Dictionary<Vector2I, LightChunkOverlay> _chunkOverlays = new();

        private Node2D _overlayRoot;
        private Rect2I _authoredRect;
        private bool _chunksAttached;
        public long SolarUpdates { get; private set; }

        public bool PresentationReady => _published && !_building && World != null && _revision == World.Revision;

        private readonly AnalyticShadowGeometry _geometry = new();
        private readonly WindowBeamCache _windowBeam = new();
        private readonly LightMapData _defaults = new();
        private readonly HashSet<ShaderMaterial> _boundMaterials = new();
        private readonly List<TileMapLayer> _layers = new();
        private readonly List<TileMapLayer> _walls = new();
        private readonly Dictionary<TileMapLayer, Material> _originalWallMaterials = new();
        private readonly Dictionary<TileMapLayer, long> _previewRevisions = new();
        private readonly HashSet<TileMapLayer> _previewWatched = new();

        private (long Beam, Rid Geometry, Vector2I Size, float Angle, float Penumbra,
            bool Published, bool Debug, bool Skylight, Color Sun, float Strength, float Influence,
            float ShadowCurve, float AmbientCurve, Transform2D Grid) _bindKey;

        private Dictionary<Vector2I, int> _preview = new();
        private HashSet<Vector2I> _previewWalls = new();
        private Color _backgroundBase = Colors.White;
        private Color _backgroundApplied = Colors.White;
        private ImageTexture _texture;
        private Vector2I _origin;
        private Vector2I _size;
        private float _angle;
        private float _penumbra;
        private double _poll;
        private long _revision = -1;
        private bool _previewDirty = true;
        private bool _invalid = true;
        private bool _published;
        private bool _building;
        private bool _debug;

        #endregion

        #region Node children references

        private Sprite2D _overlay;
        private ShaderMaterial _material;
        private ShaderMaterial _wallMaterial;
        private CanvasItem _background;
        private Shader _debugShader;
        private Camera2D _camera;

        #endregion

        #region Godot implementation

        public override void _Process(double delta)
        {
            if (!Engine.IsEditorHint() && DimensionId != null)
            {
                AttachChunks();
                EnsureAuthoredChunks();
                RebuildChunks(LightingConstants.MAX_CHUNK_REBUILDS_PER_FRAME);
            }

            UpdateEditorPreview();

            var terrainOverlay = GetParent().GetNodeOrNull<Node2D>("LightOverlay");

            if (terrainOverlay != null)
            {
                terrainOverlay.Visible = LightMapEnabled;
            }

            if (!LightMapEnabled || (Engine.IsEditorHint() && !PreviewInEditor))
            {
                Suspend();

                return;
            }

            if (_layers.Count == 0)
            {
                Resolve();
            }

            if (_layers.Count == 0 || _layers[0].TileSet == null)
            {
                return;
            }

            var grid = _layers[0];

            if (!UpdateWorld(delta))
            {
                return;
            }

            EnsureOverlay();

            var angle = Setting(nameof(LightMapData.SunAngleDegrees), _defaults.SunAngleDegrees);
            var penumbra = Mathf.Clamp(Setting(nameof(LightMapData.SunPenumbra), _defaults.SunPenumbra), 0, 1);

            UpdateGeometry(grid, angle, penumbra);
            UpdateDebugAndWalls();

            var sunColor = SunColor();

            UpdateBackground(sunColor);

            _windowBeam.Update(
                this,
                World,
                _origin,
                _size,
                angle,
                penumbra,
                Setting(nameof(LightMapData.SunPenumbraShadowCurve), _defaults.SunPenumbraShadowCurve),
                Setting(nameof(LightMapData.SunPenumbraAmbientCurve), _defaults.SunPenumbraAmbientCurve),
                Setting(nameof(LightMapData.TerrainLightDepthTiles), _defaults.TerrainLightDepthTiles));

            UpdateOverlayMaterials(grid, sunColor);

            _overlay.Visible = _published && _debug;
        }

        public override void _ExitTree()
        {
            DetachChunks();
            RestoreBackground();
            ReleaseWallMaterials();
            DisableWindowBeam();

            foreach (var layer in _previewWatched)
            {
                if (IsInstanceValid(layer))
                {
                    layer.Changed -= PreviewChanged;
                }
            }
        }

        #endregion

        #region Core - Mundo

        public void Invalidate()
        {
            _invalid = true;
        }

        private bool UpdateWorld(double delta)
        {
            var world = Engine.IsEditorHint() ? null : (GetParent() as Dimension)?.World;

            if (world == null)
            {
                UpdatePreviewWorld(delta);

                return true;
            }

            if (world != World)
            {
                World = world;
                _invalid = true;
                _published = false;
                _building = false;
            }

            return true;
        }

        private void Resolve()
        {
            foreach (var path in Layers)
            {
                if (GetNodeOrNull<TileMapLayer>(path) is { } layer && IsLightBlocker(layer))
                {
                    _layers.Add(layer);
                }
            }

            if (_layers.Count == 0)
            {
                foreach (var child in GetParent().GetChildren())
                {
                    if (child is TileMapLayer layer && IsLightBlocker(layer))
                    {
                        _layers.Add(layer);
                    }
                }
            }

            foreach (var child in GetParent().GetChildren())
            {
                if (child is BackgroundWallLayer wall)
                {
                    _walls.Add(wall);
                }
            }

            var baseLayer = GetParent().GetNodeOrNull<TileMapLayer>("Base");

            if (baseLayer != null && !_layers.Contains(baseLayer))
            {
                _layers.Add(baseLayer);
            }

            if (Engine.IsEditorHint())
            {
                WatchPreviewLayers(_layers);
                WatchPreviewLayers(_walls);
            }

            _camera = Camera != null && !Camera.IsEmpty
                ? GetNodeOrNull<Camera2D>(Camera)
                : GetParent().GetNodeOrNull<Camera2D>("Camera2D");
        }

        private static bool IsLightBlocker(TileMapLayer layer)
        {
            return layer is not BackgroundWallLayer && layer.Name != "Base";
        }

        #endregion

        #region Core - Preview do editor

        private void UpdateEditorPreview()
        {
            if (!Engine.IsEditorHint())
            {
                return;
            }

            if (!IsInstanceValid(EditorPreview))
            {
                EditorPreview = GetNodeOrNull<LightingEditorPreview>("TerrainPreview");

                if (EditorPreview == null)
                {
                    EditorPreview = new LightingEditorPreview
                    {
                        Name = "TerrainPreview",
                        Enabled = false
                    };

                    AddChild(EditorPreview, false, InternalMode.Back);
                }
            }

            EditorPreview.Transform = Transform.AffineInverse();
            EditorPreview.ComposeLayerPath = RelativeToParent(ComposeLayerPath);
            EditorPreview.BaseLayerPath = RelativeToParent(BaseLayerPath);
            EditorPreview.IncludeSkylight = IncludeSkylight;
            EditorPreview.Padding = PreviewPadding;
            EditorPreview.RefreshIntervalSeconds = PreviewRefreshIntervalSeconds;
            EditorPreview.Enabled = LightMapEnabled && PreviewInEditor;
        }

        private void UpdatePreviewWorld(double delta)
        {
            _poll += delta;

            if (_poll >= EditorPollSeconds)
            {
                foreach (var layer in _previewWatched)
                {
                    var revision = EditorTileRevision.Get(layer);

                    if (!_previewRevisions.TryGetValue(layer, out var previous) || previous != revision)
                    {
                        _previewRevisions[layer] = revision;
                        _previewDirty = true;
                    }
                }
            }

            if (World != null && !_invalid && !(_previewDirty && _poll >= EditorPollSeconds))
            {
                return;
            }

            _poll = 0;
            _previewDirty = false;

            World ??= new LogicalLightWorld(0, "shadow-preview", 1, false);

            SyncPreviewTerrain();
            SyncPreviewWalls();
        }

        private void SyncPreviewTerrain()
        {
            var cells = new Dictionary<Vector2I, int>();

            foreach (var layer in _layers)
            {
                foreach (var cell in layer.GetUsedCells())
                {
                    cells[cell] = LogicalLightWorld.TileTerrain(layer, cell);
                }
            }

            foreach (var cell in _preview.Keys)
            {
                if (!cells.ContainsKey(cell))
                {
                    World.SetTerrain(cell.X, cell.Y, -1);
                }
            }

            foreach (var entry in cells)
            {
                if (!_preview.TryGetValue(entry.Key, out var previous) || previous != entry.Value)
                {
                    World.SetTerrain(entry.Key.X, entry.Key.Y, entry.Value);
                }
            }

            _preview = cells;
        }

        private void SyncPreviewWalls()
        {
            var walls = new HashSet<Vector2I>();

            foreach (var layer in _walls)
            {
                foreach (var cell in layer.GetUsedCells())
                {
                    walls.Add(cell);
                }
            }

            foreach (var cell in _previewWalls)
            {
                if (!walls.Contains(cell))
                {
                    World.SetBackground(cell.X, cell.Y, false);
                }
            }

            foreach (var cell in walls)
            {
                if (!_previewWalls.Contains(cell))
                {
                    World.SetBackground(cell.X, cell.Y, true);
                }
            }

            _previewWalls = walls;
        }

        private void WatchPreviewLayers(List<TileMapLayer> layers)
        {
            foreach (var layer in layers)
            {
                if (_previewWatched.Add(layer))
                {
                    layer.Changed += PreviewChanged;
                }
            }
        }

        private void PreviewChanged()
        {
            _previewDirty = true;
        }

        #endregion

        #region Core - Geometria

        private void UpdateGeometry(TileMapLayer grid, float angle, float penumbra)
        {
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;

            Vector2 tileSize = grid.TileSet.TileSize;
            Vector2 view = !Engine.IsEditorHint() && _camera != null
                ? _camera.GetViewportRect().Size / _camera.Zoom
                : (Vector2)PreviewSize * tileSize;

            var centre = grid.LocalToMap(grid.ToLocal(_camera?.GlobalPosition ?? GlobalPosition));
            var width = Mathf.CeilToInt(view.X / tileSize.X);
            var height = Mathf.CeilToInt(view.Y / tileSize.Y);
            var origin = new Vector2I(
                CoordinateUtilities.CellToChunk(centre.X - width / 2) * chunkSize - chunkSize,
                CoordinateUtilities.CellToChunk(centre.Y - height / 2) * chunkSize - chunkSize);
            var size = new Vector2I(
                ((width + chunkSize - 1) / chunkSize + RegionMarginChunks) * chunkSize,
                ((height + chunkSize - 1) / chunkSize + RegionMarginChunks) * chunkSize);

            if (_invalid || _revision != World.Revision || _angle != angle || _penumbra != penumbra || _origin != origin || _size != size)
            {
                _origin = origin;
                _size = size;
                _angle = angle;
                _penumbra = penumbra;
                _revision = World.Revision;
                _building = true;
                _invalid = false;
                _published = false;

                _geometry.Begin(World, origin, size, angle, penumbra);
            }

            if (!_building)
            {
                return;
            }

            _geometry.Process(GeometryBudgetMsec);

            if (!_geometry.Complete)
            {
                return;
            }

            PublishGeometry(grid, tileSize);
        }

        private void PublishGeometry(TileMapLayer grid, Vector2 tileSize)
        {
            using var data = _geometry.ToImage();

            if (_texture == null)
            {
                _texture = ImageTexture.CreateFromImage(data);
            }
            else
            {
                _texture.SetImage(data);
            }

            BindGeometry(_material, true);

            var local = Transform2D.Identity.Scaled((Vector2)_size * tileSize);

            local.Origin = grid.MapToLocal(_origin) - tileSize / 2;

            _overlay.GlobalTransform = grid.GlobalTransform * local;
            _building = false;
            _published = true;

            SolarUpdates++;
        }

        #endregion

        #region Core - Apresentacao

        private void Suspend()
        {
            if (IsInstanceValid(_overlay))
            {
                _overlay.Visible = false;
            }

            ReleaseWallMaterials();
            RestoreBackground();
            DisableWindowBeam();
        }

        private void EnsureOverlay()
        {
            if (IsInstanceValid(_overlay))
            {
                return;
            }

            using var white = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);

            white.Fill(Colors.White);

            _debugShader = GD.Load<Shader>("res://Assets/Shaders/projected_shadow_debug.gdshader");

            _material = new ShaderMaterial
            {
                Shader = _debugShader
            };

            _wallMaterial = new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://Assets/Shaders/wall_projected_shadow.gdshader")
            };

            _overlay = new Sprite2D
            {
                Name = "ProjectedShadow",
                Centered = false,
                ZIndex = OverlayZIndex,
                ZAsRelative = false,
                Material = _material,
                Texture = ImageTexture.CreateFromImage(white),
                Visible = false
            };

            AddChild(_overlay);
        }

        private void UpdateDebugAndWalls()
        {
            var debug = Settings?.Get(nameof(LightMapData.DebugShadow)).AsBool() ?? false;

            if (debug != _debug)
            {
                _debug = debug;
                _material.Shader = _debugShader;

                BindGeometry(_material, _published);
            }

            if (!_debug)
            {
                _material.SetShaderParameter("shadow_strength", Setting(nameof(LightMapData.ShadowStrength), _defaults.ShadowStrength));
            }

            _material.SetShaderParameter("penumbra_shadow_transition", Setting(nameof(LightMapData.SunPenumbraShadowCurve), _defaults.SunPenumbraShadowCurve));
            _material.SetShaderParameter("penumbra_ambient_transition", Setting(nameof(LightMapData.SunPenumbraAmbientCurve), _defaults.SunPenumbraAmbientCurve));

            foreach (var wall in _walls)
            {
                if (!_originalWallMaterials.ContainsKey(wall))
                {
                    _originalWallMaterials[wall] = wall.Material;
                    wall.Material = _wallMaterial;
                }
            }
        }

        private void ReleaseWallMaterials()
        {
            foreach (var pair in _originalWallMaterials)
            {
                if (IsInstanceValid(pair.Key) && pair.Key.Material == _wallMaterial)
                {
                    pair.Key.Material = pair.Value;
                }
            }

            _originalWallMaterials.Clear();
        }

        private void UpdateBackground(Color color)
        {
            var target = GetParent().GetNodeOrNull<CanvasItem>("Background");

            if (target != _background)
            {
                RestoreBackground();

                _background = target;

                if (target != null)
                {
                    _backgroundBase = target.Modulate;
                }
            }

            if (target == null)
            {
                return;
            }

            var tinted = _backgroundBase * new Color(color.R, color.G, color.B, 1);

            if (target.Modulate != tinted)
            {
                target.Modulate = tinted;
            }

            _backgroundApplied = tinted;
        }

        private void RestoreBackground()
        {
            if (IsInstanceValid(_background) && _background.Modulate == _backgroundApplied)
            {
                _background.Modulate = _backgroundBase;
            }

            _background = null;
        }

        #endregion

        #region Core - Materiais

        private void UpdateOverlayMaterials(TileMapLayer grid, Color sunColor)
        {
            var bindingKey = (
                _windowBeam.Revision, _texture?.GetRid() ?? default, _size, _angle, _penumbra,
                _published, _debug, IncludeSkylight, sunColor,
                Setting(nameof(LightMapData.ShadowStrength), _defaults.ShadowStrength),
                Setting(nameof(LightMapData.AmbientLightInfluence), _defaults.AmbientLightInfluence),
                Setting(nameof(LightMapData.SunPenumbraShadowCurve), _defaults.SunPenumbraShadowCurve),
                Setting(nameof(LightMapData.SunPenumbraAmbientCurve), _defaults.SunPenumbraAmbientCurve),
                grid.GlobalTransform);

            if (!_bindKey.Equals(bindingKey))
            {
                _bindKey = bindingKey;

                _boundMaterials.Clear();
            }

            BindMaterial(EditorPreview?.GetNodeOrNull<Sprite2D>("PreviewSprite")?.Material as ShaderMaterial, grid, sunColor);

            var runtimeOverlay = GetParent().GetNodeOrNull<Node2D>("LightOverlay");

            if (runtimeOverlay == null)
            {
                return;
            }

            foreach (var child in runtimeOverlay.GetChildren())
            {
                if (child is Sprite2D sprite)
                {
                    BindMaterial(sprite.Material as ShaderMaterial, grid, sunColor);
                }
            }
        }

        private void BindMaterial(ShaderMaterial material, TileMapLayer grid, Color sunColor)
        {
            if (material == null || !_boundMaterials.Add(material))
            {
                return;
            }

            _windowBeam.Bind(material, grid, !_debug, sunColor);
            BindSolarComposition(material);
        }

        private void BindSolarComposition(ShaderMaterial material)
        {
            material.SetShaderParameter("include_skylight", IncludeSkylight);

            BindGeometry(material, _published && !_debug);

            material.SetShaderParameter("shadow_strength", Setting(nameof(LightMapData.ShadowStrength), _defaults.ShadowStrength));
            material.SetShaderParameter("ambient_light_influence", Setting(nameof(LightMapData.AmbientLightInfluence), _defaults.AmbientLightInfluence));
            material.SetShaderParameter("penumbra_shadow_transition", Setting(nameof(LightMapData.SunPenumbraShadowCurve), _defaults.SunPenumbraShadowCurve));
            material.SetShaderParameter("penumbra_ambient_transition", Setting(nameof(LightMapData.SunPenumbraAmbientCurve), _defaults.SunPenumbraAmbientCurve));
        }

        private void BindGeometry(ShaderMaterial material, bool ready)
        {
            material.SetShaderParameter("shadow_geometry", _texture);
            material.SetShaderParameter("map_size", (Vector2)_size * 2);
            material.SetShaderParameter("sun_angle", Mathf.DegToRad(_angle));
            material.SetShaderParameter("penumbra", _penumbra);
            material.SetShaderParameter("geometry_ready", ready);
        }

        private void DisableWindowBeam()
        {
            _boundMaterials.Clear();

            var parent = GetParent();

            if (parent == null)
            {
                return;
            }

            if (EditorPreview?.GetNodeOrNull<Sprite2D>("PreviewSprite")?.Material is ShaderMaterial preview)
            {
                preview.SetShaderParameter("window_beam_ready", false);
            }

            var runtime = parent.GetNodeOrNull<Node2D>("LightOverlay");

            if (runtime == null)
            {
                return;
            }

            foreach (var child in runtime.GetChildren())
            {
                if (child is Sprite2D sprite && sprite.Material is ShaderMaterial material)
                {
                    material.SetShaderParameter("window_beam_ready", false);
                }
            }
        }

        #endregion

        #region Core - Chunks de luz

        public void OnCellChanged(Vector2I cell)
        {
            MarkDirtyWithNeighbors(CoordinateUtilities.CellToChunk(cell));
        }

        private void AttachChunks()
        {
            if (_chunksAttached)
            {
                return;
            }

            _chunksAttached = true;
            _lightEmittingBlocks ??= LightSourceScanner.BuildLightEmittingBlockIndex();

            foreach (var layer in _layers)
            {
                if (layer is TerrainLayer terrain)
                {
                    terrain.CellChanged += OnCellChanged;
                }
            }

            if (GetParent() is Dimension dimension)
            {
                dimension.ChunkLoaded += OnChunkLoaded;
                dimension.ChunkUnloaded += OnChunkUnloaded;
            }
        }

        private void DetachChunks()
        {
            if (!_chunksAttached)
            {
                return;
            }

            _chunksAttached = false;

            foreach (var layer in _layers)
            {
                if (layer is TerrainLayer terrain && IsInstanceValid(terrain))
                {
                    terrain.CellChanged -= OnCellChanged;
                }
            }

            if (GetParent() is Dimension dimension)
            {
                dimension.ChunkLoaded -= OnChunkLoaded;
                dimension.ChunkUnloaded -= OnChunkUnloaded;
            }

            foreach (var overlay in _chunkOverlays.Values)
            {
                overlay.Dispose();
            }

            _chunkOverlays.Clear();
            _loadedChunks.Clear();
            _dirtyChunks.Clear();
            _inFlightChunks.Clear();
        }

        private void OnChunkLoaded(Vector2I chunkCoord)
        {
            _loadedChunks.Add(chunkCoord);

            MarkDirtyWithNeighbors(chunkCoord);
        }

        private void OnChunkUnloaded(Vector2I chunkCoord)
        {
            _loadedChunks.Remove(chunkCoord);
            _dirtyChunks.Remove(chunkCoord);

            if (_chunkOverlays.TryGetValue(chunkCoord, out var overlay))
            {
                overlay.Dispose();
                _chunkOverlays.Remove(chunkCoord);
            }
        }

        private void MarkDirtyWithNeighbors(Vector2I chunkCoord)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    _dirtyChunks.Add(chunkCoord + new Vector2I(dx, dy));
                }
            }
        }

        private void EnsureAuthoredChunks()
        {
            if ((GetParent() as Dimension)?.TileStreaming == true)
            {
                return;
            }

            var used = AuthoredRect();

            if (used.Size.X <= 0 || used.Size.Y <= 0 || _authoredRect == used)
            {
                return;
            }

            _authoredRect = used;

            var first = CoordinateUtilities.CellToChunk(used.Position);
            var last = CoordinateUtilities.CellToChunk(used.End - Vector2I.One);

            for (var cx = first.X; cx <= last.X; cx++)
            {
                for (var cy = first.Y; cy <= last.Y; cy++)
                {
                    var chunkCoord = new Vector2I(cx, cy);

                    _loadedChunks.Add(chunkCoord);

                    MarkDirtyWithNeighbors(chunkCoord);
                }
            }
        }

        private Rect2I AuthoredRect()
        {
            var used = ComposeLayer.GetUsedRect();
            var baseLayer = BaseLayer;

            if (baseLayer != null)
            {
                var baseUsed = baseLayer.GetUsedRect();

                used = baseUsed.Size == Vector2I.Zero ? used : used.Merge(baseUsed);
            }

            var walls = WallLayer;

            if (walls != null && walls.GetUsedRect().Size != Vector2I.Zero)
            {
                used = used.Size == Vector2I.Zero ? walls.GetUsedRect() : used.Merge(walls.GetUsedRect());
            }

            return used;
        }

        private void RebuildChunks(int maxChunks)
        {
            if (_dirtyChunks.Count == 0 || ComposeLayer == null)
            {
                return;
            }

            var overlayRoot = OverlayRoot();

            if (overlayRoot == null)
            {
                return;
            }

            var processed = 0;
            var toProcess = new List<Vector2I>();

            foreach (var chunkCoord in _dirtyChunks)
            {
                if (!_loadedChunks.Contains(chunkCoord))
                {
                    toProcess.Add(chunkCoord);

                    continue;
                }

                if (_inFlightChunks.Contains(chunkCoord) || processed >= maxChunks)
                {
                    continue;
                }

                toProcess.Add(chunkCoord);

                processed++;
            }

            foreach (var chunkCoord in toProcess)
            {
                _dirtyChunks.Remove(chunkCoord);

                if (_loadedChunks.Contains(chunkCoord))
                {
                    RebuildChunk(overlayRoot, chunkCoord);
                }
            }
        }

        private async void RebuildChunk(Node2D overlayRoot, Vector2I chunkCoord)
        {
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var padding = LightingConstants.CHUNK_PADDING;
            var chunkOrigin = CoordinateUtilities.ChunkToCell(chunkCoord);
            var region = new Rect2I(
                chunkOrigin - new Vector2I(padding, padding),
                new Vector2I(chunkSize + (padding * 2), chunkSize + (padding * 2)));
            var layer = ComposeLayer;
            var baseLayer = BaseLayer;
            var walls = WallLayer;

            bool IsSolid(Vector2I cell)
            {
                return layer.GetCellSourceId(cell) != -1 || (baseLayer != null && baseLayer.GetCellSourceId(cell) != -1);
            }

            var sources = LightSourceScanner.CollectSources(
                layer,
                baseLayer,
                region,
                _lightEmittingBlocks,
                IsSolid,
                includeSkylight: false,
                hasBackground: cell => walls != null && walls.GetCellSourceId(cell) != -1);

            _inFlightChunks.Add(chunkCoord);

            try
            {
                var output = await Propagation.ComputeTextureAsync(region, IsSolid, sources);

                if (!IsInstanceValid(this) || !_loadedChunks.Contains(chunkCoord) || !IsInstanceValid(overlayRoot))
                {
                    output.Dispose();

                    return;
                }

                PublishChunk(overlayRoot, chunkCoord, chunkOrigin, region, output);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception error)
            {
                GD.PushError(error.ToString());
            }
            finally
            {
                _inFlightChunks.Remove(chunkCoord);
            }
        }

        private void PublishChunk(Node2D overlayRoot, Vector2I chunkCoord, Vector2I chunkOrigin, Rect2I region, LightTextureOutput output)
        {
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var tileSize = ComposeLayer.TileSet.TileSize.X;

            if (!_chunkOverlays.TryGetValue(chunkCoord, out var overlay))
            {
                overlay = new LightChunkOverlay(overlayRoot, chunkCoord, tileSize);
                _chunkOverlays[chunkCoord] = overlay;
            }

            var bounds = new Rect2I(chunkOrigin * tileSize, new Vector2I(chunkSize, chunkSize) * tileSize);

            using var mask = TerrainLightMask.Build(bounds, overlayRoot, BaseLayer, ComposeLayer, WallLayer);

            overlay.UpdateOutput(output, mask, LightingConstants.CHUNK_PADDING, region.Size.X);
        }

        private Node2D OverlayRoot()
        {
            if (IsInstanceValid(_overlayRoot))
            {
                return _overlayRoot;
            }

            var parent = GetParent();

            if (parent == null)
            {
                return null;
            }

            _overlayRoot = parent.GetNodeOrNull<Node2D>("LightOverlay");

            if (_overlayRoot == null)
            {
                _overlayRoot = new Node2D
                {
                    Name = "LightOverlay"
                };

                parent.AddChild(_overlayRoot);
            }

            return _overlayRoot;
        }

        #endregion

        #region Utils

        private float Setting(string name, float fallback)
        {
            if (Settings == null)
            {
                return fallback;
            }

            var value = Settings.Get(name);

            return value.VariantType == Variant.Type.Nil ? fallback : value.AsSingle();
        }

        private Color SunColor()
        {
            var value = Settings?.Get(nameof(LightMapData.SunColor));

            return value.HasValue && value.Value.VariantType == Variant.Type.Color ? value.Value.AsColor() : Colors.White;
        }

        private static NodePath RelativeToParent(NodePath path)
        {
            var text = path.ToString();

            return new NodePath(text.StartsWith("/") ? text : "../" + text);
        }

        #endregion
    }
}
