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
        private HashSet<Vector2I> _previewBackground = new();
        private double _editorPoll;
        // Reflexao em vez de lista escrita a mao: um ajuste novo nao pode depender de alguem
        // lembrar de registra-lo aqui para sobreviver a um reload de script do editor.
        private static readonly string[] Tracked = System.Array.ConvertAll(
            typeof(LightMapData).GetProperties(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly),
            property => property.Name);
        private readonly LightMapData _defaults = new();
        private readonly LightMapData _editorSettings = new();

        private LightMapData ReadSettings()
        {
            if (Settings is LightMapData typed) return typed;
            if (Settings == null) return _defaults;
            // During editor script reloads Godot can expose a Resource placeholder instead
            // of the managed type. Read its exported values rather than silently using defaults.
            foreach (string name in Tracked)
            {
                Variant value = Settings.Get(name);
                _editorSettings.Set(name, value.VariantType == Variant.Type.Nil ? _defaults.Get(name) : value);
            }
            return _editorSettings;
        }
        private Sprite2D _overlay, _volumeOverlay;
        private ShaderMaterial _receiverMaterial, _volumeMaterial;
        private readonly HashSet<CanvasItem> _receivers = new();
        private readonly List<string> _sharedUniforms = new();
        private double _receiverPoll;
        private void ReleaseReceivers()
        {
            foreach (var item in _receivers)
                if (IsInstanceValid(item) && item.Material == _receiverMaterial) item.Material = null;
            _receivers.Clear();
            if (IsInstanceValid(_volumeOverlay)) _volumeOverlay.Visible = false;
        }
        private void FindReceivers(Node parent)
        {
            foreach (var child in parent.GetChildren())
            {
                if (child == this || child is TileMapLayer or CanvasLayer or Control or Parallax2D || child.Name == "Background") continue;
                if (child is Sprite2D or AnimatedSprite2D)
                {
                    var item = (CanvasItem)child;
                    if (item.Material == null && !item.UseParentMaterial)
                    { item.Material = _receiverMaterial; _receivers.Add(item); }
                }
                FindReceivers(child);
            }
        }
        private ShaderMaterial _material;
        private ShaderMaterial _present;
        private ImageTexture _lightTexture, _emissionTexture, _shadowGeometryTexture, _depthBeamTexture;
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
            ReleaseReceivers();
            DimensionId = null;
            World = null;
            _published = false;
            _building = false;
            _invalid = true;
            if (IsInstanceValid(_overlay)) _overlay.Visible = false;
        }

        public override void _ExitTree()
        {
            ReleaseReceivers();
            foreach (var layer in _layers)
                if (IsInstanceValid(layer) && Engine.IsEditorHint()) layer.Changed -= Invalidate;
        }

        public override void _Process(double delta)
        {
            if (!LightMapEnabled || (Engine.IsEditorHint() && !PreviewInEditor))
            {
                if (IsInstanceValid(_overlay)) _overlay.Visible = false;
                ReleaseReceivers();
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
                        World = new LogicalLightWorld(0, "preview", 1, false) { DepthLightEnabled = true };
                        _previewTerrain.Clear();
                        _previewBackground.Clear();
                        _requestedSize = Vector2I.Zero;
                        _published = false;
                    }
                    var current = new Dictionary<Vector2I, int>();
                    long previousRevision = World.Revision;
                    foreach (var layer in _layers)
                        foreach (var cell in layer.GetUsedCells())
                            if (layer.Name != "Base" && layer is not Jogo25D.Blocks.BackgroundWallLayer) current[cell] = LogicalLightWorld.TileTerrain(layer, cell);
                    foreach (var cell in _previewTerrain.Keys)
                        if (!current.ContainsKey(cell)) World.SetTerrain(cell.X, cell.Y, -1);
                    foreach (var entry in current)
                        if (!_previewTerrain.TryGetValue(entry.Key, out int old) || old != entry.Value)
                            World.SetTerrain(entry.Key.X, entry.Key.Y, entry.Value);
                    _previewTerrain = current;
                    var background = new HashSet<Vector2I>();
                    foreach (var layer in _layers)
                        if (layer is Jogo25D.Blocks.BackgroundWallLayer)
                            foreach (var cell in layer.GetUsedCells()) background.Add(cell);
                    foreach (var cell in _previewBackground)
                        if (!background.Contains(cell)) World.SetBackground(cell.X,cell.Y,false);
                    foreach (var cell in background)
                        if (!_previewBackground.Contains(cell)) World.SetBackground(cell.X,cell.Y,true);
                    _previewBackground = background;
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
            var settings = ReadSettings();
            if (World.DepthLightEnabled != settings.DepthEnabled)
            {
                // Isso muda o oraculo de ceu do solver, nao so a apresentacao: o cache derivado
                // inteiro precisa ser refeito. Despejar a regiao para longe e voltar faz isso.
                World.DepthLightEnabled = settings.DepthEnabled;
                World.Field.SetRegion(1000000, 1000000, 1, 1);
                _requestedSize = Vector2I.Zero;
                _invalid = true; _published = false;
            }
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
                _present.SetShaderParameter("geometry_ready", false);
            }
            if (origin != _requestedOrigin || size != _requestedSize || _invalid)
            {
                World.Field.SetRegion(origin.X, origin.Y, size.X, size.Y);
                _requestedOrigin = origin; _requestedSize = size;
            }
            World.Field.Process();
            bool geometryChanged = _invalid || !_computer.IsWorld(World) || _computer.Origin != origin || _computer.Size != size
                || _computer.WorldRevision != World.Revision;
            bool inputsChanged = geometryChanged || _computer.BackgroundRevision != World.BackgroundRevision || _computer.Angle != settings.SunAngleDegrees || _computer.Penumbra != settings.SunPenumbra
                || _computer.TerrainTransition != Mathf.Clamp(settings.TerrainLightDepthTiles, 0.25f, 16f)
                || _computer.ShadowSoftness != settings.SunPenumbraShadowCurve
                || _computer.AmbientSoftness != settings.SunPenumbraAmbientCurve;
            if (geometryChanged) _building = false;
            // A moving sun queues the next snapshot rather than starving the current build.
            if (World.Field.Settled && (_building || !_published || inputsChanged || _displayedRevision != World.Field.Revision))
            {
                if (!_building)
                {
                    _computer.Begin(World, origin, size, settings.SunAngleDegrees, settings.SunPenumbra,
                        settings.TerrainLightDepthTiles,
                        settings.SunPenumbraShadowCurve, settings.SunPenumbraAmbientCurve);
                    _building = true; _invalid = false;
                }
                _computer.Process();
                if (_computer.Complete)
                {
                    Upload(ref _lightTexture, _computer.LightImage());
                    Upload(ref _emissionTexture, _computer.EmissionImage());
                    Upload(ref _shadowGeometryTexture, _computer.ShadowGeometryImage());
                    Upload(ref _depthBeamTexture, _computer.DepthBeamImage());
                    _present.SetShaderParameter("depth_beam_data", _depthBeamTexture);
                    _material.SetShaderParameter("light_data", _lightTexture);
                    _material.SetShaderParameter("shadow_geometry", _shadowGeometryTexture);
                    _material.SetShaderParameter("map_size", (Vector2)size * LightMapComputer.Subdivisions);
                    _material.SetShaderParameter("sun_angle", Mathf.DegToRad(_computer.Angle));
                    _material.SetShaderParameter("penumbra", _computer.Penumbra);
                    _material.SetShaderParameter("terrain_transition_tiles", _computer.TerrainTransition);
                    _material.SetShaderParameter("penumbra_shadow_transition", _computer.ShadowSoftness);
                    _material.SetShaderParameter("penumbra_ambient_transition", _computer.AmbientSoftness);
                    _pass.Size = size * LightMapComputer.ShadowSubdivisions;
                    _passRect.Size = _pass.Size;
                    if (_computer.RebuiltSun) { _pass.RenderTargetUpdateMode = SubViewport.UpdateMode.Once; SolarUpdates++; }
                    _overlay.Texture = _pass.GetTexture();
                    _present.SetShaderParameter("light_data", _lightTexture);
                    _present.SetShaderParameter("emission_data", _emissionTexture);
                    _present.SetShaderParameter("map_size", (Vector2)size * LightMapComputer.Subdivisions);
                    _present.SetShaderParameter("shadow_geometry", _shadowGeometryTexture);
                    _present.SetShaderParameter("sun_angle", Mathf.DegToRad(_computer.Angle));
                    _present.SetShaderParameter("penumbra", _computer.Penumbra);
                    _present.SetShaderParameter("terrain_transition_tiles", _computer.TerrainTransition);
                    _present.SetShaderParameter("penumbra_shadow_transition", _computer.ShadowSoftness);
                    _present.SetShaderParameter("penumbra_ambient_transition", _computer.AmbientSoftness);
                    _present.SetShaderParameter("geometry_ready", true);
                    _overlay.GlobalPosition = grid.ToGlobal(grid.MapToLocal(origin) - tileSize / 2);
                    _overlay.Scale = tileSize / LightMapComputer.ShadowSubdivisions;
                    _displayedRevision = _computer.FieldRevision;
                    _displayedOrigin = origin; _displayedSize = size;
                    _published = true; _building = false;
                }
            }
            static float Mute(bool on, float value) => on ? Mathf.Clamp(value, 0f, 2f) : 0f;
            _present.SetShaderParameter("ambient_energy", Mute(settings.GlobalLightEnabled, settings.GlobalLightIntensity));
            _present.SetShaderParameter("sky_color", settings.GlobalLightColor);
            _present.SetShaderParameter("sun_energy", Mute(settings.SunEnabled, settings.SunIntensity));
            _present.SetShaderParameter("sun_color", settings.SunColor);
            _present.SetShaderParameter("emission_energy", Mute(settings.EmissionEnabled, settings.EmissionIntensity));
            _present.SetShaderParameter("depth_beam_enabled", settings.BeamEnabled);
            _present.SetShaderParameter("volumetric_reach", Mathf.Clamp(settings.BeamReachTiles, 0.25f, 24f));
            _present.SetShaderParameter("volume_density", settings.DustDensity);
            _present.SetShaderParameter("shadow_air", Mute(settings.AirShadowEnabled, settings.AirShadowStrength));
            _present.SetShaderParameter("shadow_terrain", Mute(settings.TerrainShadowEnabled, settings.TerrainShadowStrength));
            _present.SetShaderParameter("shadow_background", Mute(settings.BackgroundShadowEnabled, settings.BackgroundShadowStrength));
            _present.SetShaderParameter("terrain_light_enabled", settings.TerrainLightEnabled);
            _present.SetShaderParameter("show_raw", settings.ShowRawMap);
            foreach (string uniform in _sharedUniforms)
            {
                var value = _present.GetShaderParameter(uniform);
                _receiverMaterial.SetShaderParameter(uniform,value);
                _volumeMaterial.SetShaderParameter(uniform,value);
            }
            _receiverMaterial.SetShaderParameter("shadow_entity", Mute(settings.EntityShadowEnabled, settings.EntityShadowStrength));
            var inverse = grid.GlobalTransform.AffineInverse();
            _receiverMaterial.SetShaderParameter("map_world_origin",_overlay.GlobalPosition);
            _receiverMaterial.SetShaderParameter("map_world_axis_x",new Vector2(inverse.X.X,inverse.Y.X)/tileSize.X);
            _receiverMaterial.SetShaderParameter("map_world_axis_y",new Vector2(inverse.X.Y,inverse.Y.Y)/tileSize.Y);
            _volumeOverlay.Texture = _overlay.Texture;
            _volumeOverlay.GlobalTransform = _overlay.GlobalTransform;
            _volumeOverlay.Visible = _published && !settings.ShowRawMap && settings.DustEnabled;
            _receiverPoll += delta;
            if (_receiverPoll >= 0.25)
            {
                _receiverPoll = 0;
                _receivers.RemoveWhere(item => !IsInstanceValid(item));
                FindReceivers(GetParent());
            }
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
            _receiverMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/Shaders/world_light_receiver.gdshader") };
            _volumeMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/Shaders/window_volume.gdshader") };
            foreach (var uniform in _present.Shader.GetShaderUniformList()) _sharedUniforms.Add(uniform.AsGodotDictionary()["name"].AsString());
            _volumeOverlay = new Sprite2D { Name = "WindowVolume", Centered = false, TopLevel = true,
                ZIndex = 901, ZAsRelative = false, Material = _volumeMaterial, Texture = _blackTexture, Visible = false };
            AddChild(_volumeOverlay);
            black.Dispose();
            AddChild(_overlay);
        }
    }
}
