using Godot;
using Jogo25D.Core;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    [Tool]
    public partial class LightMap2D : Node2D
    {
        [Export] public bool LightMapEnabled { get; set; } = true;
        [Export] public bool PreviewInEditor { get; set; } = true;
        [Export] public Vector2I PreviewSize { get; set; } = new(120, 80);
        [Export] public Godot.Collections.Array<NodePath> Layers { get; set; } = new();
        [Export] public NodePath Camera { get; set; } = new("");
        [Export(PropertyHint.ResourceType, "LightMapData")] public Resource Settings { get; set; }
        public string DimensionId { get; set; }
        public LogicalLightWorld World { get; private set; }
        public bool PresentationReady => _published && !_building && World != null && World.Field.Settled && _displayedRevision == World.Field.Revision;
        public long SolarUpdates { get; private set; }
        private readonly LightMapComputer _computer = new();
        private readonly List<TileMapLayer> _layers = new();
        private Dictionary<Vector2I, int> _previewTerrain = new();
        private double _editorPoll;
        private readonly LightMapData _defaults = new();
        private readonly LightMapData _editorSettings = new();

        private LightMapData ReadSettings()
        {
            if (Settings is LightMapData typed) return typed;
            if (Settings == null) return _defaults;
            // During editor script reloads Godot can expose a Resource placeholder instead
            // of the managed type. Read its exported values rather than silently using defaults.
            foreach (var property in _defaults.GetPropertyList())
            {
                string name = property["name"].AsString();
                if (name is not (nameof(LightMapData.SunAngleDegrees) or nameof(LightMapData.Penumbra)
                    or nameof(LightMapData.AmbientInfluence) or nameof(LightMapData.SunIntensity)
                    or nameof(LightMapData.SkyColor) or nameof(LightMapData.SunColor) or nameof(LightMapData.ShowRawMap))) continue;
                Variant value = Settings.Get(name);
                _editorSettings.Set(name, value.VariantType == Variant.Type.Nil ? _defaults.Get(name) : value);
            }
            return _editorSettings;
        }
        private Sprite2D _overlay;
        private ShaderMaterial _material;
        private ShaderMaterial _present;
        private ImageTexture _lightTexture, _emissionTexture, _boundaryTexture;
        private SubViewport _pass;
        private ColorRect _passRect;
        private ImageTexture _blackTexture;
        private Camera2D _camera;
        private bool _building, _invalid = true, _published;
        private long _displayedRevision = -1;
        private Vector2I _requestedOrigin, _requestedSize;
        private Vector2I _displayedOrigin, _displayedSize;

        public void Invalidate() => _invalid = true;

        public void DetachWorld()
        {
            DimensionId = null;
            World = null;
            _published = false;
            _building = false;
            _invalid = true;
            if (IsInstanceValid(_overlay)) _overlay.Visible = false;
        }

        public override void _ExitTree()
        {
            foreach (var layer in _layers)
                if (IsInstanceValid(layer) && Engine.IsEditorHint()) layer.Changed -= Invalidate;
        }

        public override void _Process(double delta)
        {
            if (!LightMapEnabled || (Engine.IsEditorHint() && !PreviewInEditor))
            {
                if (IsInstanceValid(_overlay)) _overlay.Visible = false;
                return;
            }
            if (_layers.Count == 0) Resolve();
            if (_layers.Count == 0 || _layers[0].TileSet == null) return;
            if (!Engine.IsEditorHint() && DimensionId == null) return;
            var grid = _layers[0];
            if (Engine.IsEditorHint())
            {
                _editorPoll += delta;
                // The editor does not consistently forward native cell changes to managed
                // Changed callbacks. Poll only the editor snapshot, never the runtime world.
                if (World == null || _invalid || _editorPoll >= 0.25)
                {
                    _editorPoll = 0;
                    if (World == null)
                    {
                        World = new LogicalLightWorld(0, "preview", 1, false);
                        _previewTerrain.Clear();
                        _requestedSize = Vector2I.Zero;
                        _published = false;
                    }
                    var current = new Dictionary<Vector2I, int>();
                    long previousRevision = World.Revision;
                    foreach (var layer in _layers)
                        foreach (var cell in layer.GetUsedCells())
                            if (layer.Name != "Base") current[cell] = LogicalLightWorld.TileTerrain(layer, cell);
                    foreach (var cell in _previewTerrain.Keys)
                        if (!current.ContainsKey(cell)) World.SetTerrain(cell.X, cell.Y, -1);
                    foreach (var entry in current)
                        if (!_previewTerrain.TryGetValue(entry.Key, out int old) || old != entry.Value)
                            World.SetTerrain(entry.Key.X, entry.Key.Y, entry.Value);
                    _previewTerrain = current;
                    if (World.Revision != previousRevision) _building = false;
                    _invalid = false;
                }
            }
            else
            {
                var world = Game.Managers.LightMapManager.Node?.GetWorld(DimensionId);
                if (world == null) return;
                if (world != World) { World = world; _invalid = true; _published = false; }
            }
            EnsureOverlay();
            _overlay.Visible = true;
            Vector2 tileSize = grid.TileSet.TileSize;
            Vector2 view = !Engine.IsEditorHint() && _camera != null ? _camera.GetViewportRect().Size / _camera.Zoom : (Vector2)PreviewSize * tileSize;
            var center = grid.LocalToMap(grid.ToLocal(_camera?.GlobalPosition ?? GlobalPosition));
            int w = Mathf.CeilToInt(view.X / tileSize.X), h = Mathf.CeilToInt(view.Y / tileSize.Y);
            // Chunk-aligned presentation cache with a full chunk of motion reserve.
            var origin = new Vector2I(LightingField.FloorChunk(center.X - w / 2) * 32 - 16,
                LightingField.FloorChunk(center.Y - h / 2) * 32 - 16);
            var size = new Vector2I(((w + 31) / 32 + 2) * 32, ((h + 31) / 32 + 2) * 32);
            if (_published && (center.X - w / 2 < _displayedOrigin.X || center.Y - h / 2 < _displayedOrigin.Y
                || center.X + (w + 1) / 2 > _displayedOrigin.X + _displayedSize.X
                || center.Y + (h + 1) / 2 > _displayedOrigin.Y + _displayedSize.Y)) _published = false;
            if (!_published)
            {
                // Never reveal an unlit world while a new logical region is converging.
                _overlay.GlobalPosition = grid.ToGlobal(grid.MapToLocal(origin) - tileSize / 2);
                _overlay.Scale = tileSize * size;
                _overlay.Texture = _blackTexture;
                _present.SetShaderParameter("light_data", _blackTexture);
                _present.SetShaderParameter("emission_data", _blackTexture);
                _present.SetShaderParameter("map_size", Vector2.One);
            }
            if (origin != _requestedOrigin || size != _requestedSize || _invalid)
            {
                World.Field.SetRegion(origin.X, origin.Y, size.X, size.Y);
                _requestedOrigin = origin; _requestedSize = size;
            }
            World.Field.Process();
            var settings = ReadSettings();
            bool geometryChanged = _invalid || !_computer.IsWorld(World) || _computer.Origin != origin || _computer.Size != size
                || _computer.WorldRevision != World.Revision;
            bool inputsChanged = geometryChanged || _computer.Angle != settings.SunAngleDegrees || _computer.Penumbra != settings.Penumbra;
            if (geometryChanged) _building = false;
            // A moving sun queues the next snapshot rather than starving the current build.
            if (World.Field.Settled && (_building || !_published || inputsChanged || _displayedRevision != World.Field.Revision))
            {
                if (!_building)
                {
                    _computer.Begin(World, origin, size, settings.SunAngleDegrees, settings.Penumbra);
                    _building = true; _invalid = false;
                }
                _computer.Process();
                if (_computer.Complete)
                {
                    Upload(ref _lightTexture, _computer.LightImage());
                    Upload(ref _emissionTexture, _computer.EmissionImage());
                    Upload(ref _boundaryTexture, _computer.BoundaryImage());
                    _material.SetShaderParameter("light_data", _lightTexture);
                    _material.SetShaderParameter("boundary_data", _boundaryTexture);
                    _material.SetShaderParameter("map_size", (Vector2)size * LightMapComputer.Subdivisions);
                    _material.SetShaderParameter("sun_angle", Mathf.DegToRad(_computer.Angle));
                    _material.SetShaderParameter("penumbra", _computer.Penumbra);
                    _pass.Size = size * LightMapComputer.ShadowSubdivisions;
                    _passRect.Size = _pass.Size;
                    if (_computer.RebuiltSun) { _pass.RenderTargetUpdateMode = SubViewport.UpdateMode.Once; SolarUpdates++; }
                    _overlay.Texture = _pass.GetTexture();
                    _present.SetShaderParameter("light_data", _lightTexture);
                    _present.SetShaderParameter("emission_data", _emissionTexture);
                    _present.SetShaderParameter("map_size", (Vector2)size * LightMapComputer.Subdivisions);
                    _overlay.GlobalPosition = grid.ToGlobal(grid.MapToLocal(origin) - tileSize / 2);
                    _overlay.Scale = tileSize / LightMapComputer.ShadowSubdivisions;
                    _displayedRevision = _computer.FieldRevision;
                    _displayedOrigin = origin; _displayedSize = size;
                    _published = true; _building = false;
                }
            }
            _present.SetShaderParameter("ambient_energy", settings.AmbientInfluence);
            _present.SetShaderParameter("sun_energy", settings.SunIntensity);
            _present.SetShaderParameter("sky_color", settings.SkyColor);
            _present.SetShaderParameter("sun_color", settings.SunColor);
            _present.SetShaderParameter("show_raw", settings.ShowRawMap);
        }

        private static void Upload(ref ImageTexture texture, Image image)
        {
            if (texture != null && texture.GetSize() == image.GetSize()) texture.Update(image);
            else texture = ImageTexture.CreateFromImage(image);
            image.Dispose();
        }

        private void Resolve()
        {
            foreach (var path in Layers)
                if (GetNodeOrNull<TileMapLayer>(path) is { } layer) _layers.Add(layer);
            if (_layers.Count == 0)
                foreach (var child in GetParent().GetChildren()) if (child is TileMapLayer layer) _layers.Add(layer);
            if (Engine.IsEditorHint()) foreach (var layer in _layers) layer.Changed += Invalidate;
            _camera = Camera != null && !Camera.IsEmpty ? GetNodeOrNull<Camera2D>(Camera) : GetParent().GetNodeOrNull<Camera2D>("Camera2D");
        }

        private void EnsureOverlay()
        {
            if (IsInstanceValid(_overlay)) return;
            _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/Shaders/light_map.gdshader") };
            _pass = new SubViewport { Name = "LightMapGpuPass", Disable3D = true, TransparentBg = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
            AddChild(_pass);
            _passRect = new ColorRect { Material = _material, Color = Colors.White };
            _pass.AddChild(_passRect);
            var black = Image.CreateEmpty(1, 1, false, Image.Format.Rgb8);
            black.Fill(Colors.Black);
            _blackTexture = ImageTexture.CreateFromImage(black);
            _present = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/Shaders/light_map_present.gdshader") };
            _present.SetShaderParameter("light_data", _blackTexture);
            _present.SetShaderParameter("emission_data", _blackTexture);
            _overlay = new Sprite2D { Name = "LightMapOverlay", Centered = false, TopLevel = true,
                ZIndex = 900, ZAsRelative = false,
                Material = _present,
                Texture = _blackTexture, TextureFilter = TextureFilterEnum.Linear };
            black.Dispose();
            AddChild(_overlay);
        }
    }
}
