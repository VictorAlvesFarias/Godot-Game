using Godot;
using Jogo25D.Core;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    // One receiver-independent projection. Logical geometry is the only input.
    [Tool]
    public partial class LightMap2D : Node2D
    {
        [Export] public bool LightMapEnabled { get; set; } = true;
        [Export] public bool PreviewInEditor { get; set; } = true;
        [Export] public Vector2I PreviewSize { get; set; } = new(120,80);
        [Export] public Godot.Collections.Array<NodePath> Layers { get; set; } = new();
        [Export] public NodePath Camera { get; set; } = new("");
        [Export(PropertyHint.ResourceType,"LightMapData")] public Resource Settings { get; set; }
        [ExportGroup("Preview do mapa de luz")]
        [Export] public bool IncludeSkylight { get; set; } = true;
        [Export] public NodePath ComposeLayerPath { get; set; } = new("../Compose");
        [Export] public NodePath BaseLayerPath { get; set; } = new("../Base");
        [Export(PropertyHint.Range,"8,64,1")] public int PreviewPadding { get; set; } = 16;
        [Export(PropertyHint.Range,"0.1,3,0.1")] public float PreviewRefreshIntervalSeconds { get; set; } = 0.5f;
        public LightingEditorPreview EditorPreview { get; private set; }
        private void UpdateEditorPreview()
        {
            if(!Engine.IsEditorHint()) return;
            if(!IsInstanceValid(EditorPreview))
            {
                EditorPreview=GetNodeOrNull<LightingEditorPreview>("TerrainPreview");
                if(EditorPreview==null)
                {
                    EditorPreview=new LightingEditorPreview { Name="TerrainPreview",Enabled=false };
                    AddChild(EditorPreview,false,InternalMode.Back);
                }
            }
            // The grid belongs to the level, not to the movable LightMap node.
            EditorPreview.Transform=Transform.AffineInverse();
            EditorPreview.ComposeLayerPath=new NodePath(ComposeLayerPath.ToString().StartsWith("/")?ComposeLayerPath.ToString():"../"+ComposeLayerPath);
            EditorPreview.BaseLayerPath=new NodePath(BaseLayerPath.ToString().StartsWith("/")?BaseLayerPath.ToString():"../"+BaseLayerPath);
            EditorPreview.IncludeSkylight=IncludeSkylight;
            EditorPreview.Padding=PreviewPadding;
            EditorPreview.RefreshIntervalSeconds=PreviewRefreshIntervalSeconds;
            EditorPreview.Enabled=LightMapEnabled && PreviewInEditor;
        }
        public string DimensionId { get; set; }
        public LogicalLightWorld World { get; private set; }
        public bool PresentationReady => _published && !_building && World != null && _revision == World.Revision;
        public long SolarUpdates { get; private set; }
        private readonly AnalyticShadowGeometry _geometry = new();
        private readonly WindowBeamCache _windowBeam = new();
        // Os parametros de feixe e composicao sao os mesmos para todos os sprites de overlay e so
        // mudam quando muda o campo, a regiao, o sol ou o Settings. Reescrever os 19 uniforms em
        // cada sprite a cada frame custa ~1,7 ms com 32 chunks e ~3 ms com 64. Aqui a escrita so
        // acontece quando a chave muda, e sprites novos sao ligados sozinhos.
        private readonly HashSet<ShaderMaterial> _boundMaterials = new();
        private (long Beam, Rid Geometry, Vector2I Size, float Angle, float Penumbra, bool Published,
            bool Debug, bool Skylight, Color Sun, float Strength, float Influence,
            float ShadowCurve, float AmbientCurve, Transform2D Grid) _bindKey;
        private readonly List<TileMapLayer> _layers = new();
        private Dictionary<Vector2I,int> _preview = new();
        private readonly List<TileMapLayer> _walls=new();
        private HashSet<Vector2I> _previewWalls=new();
        private Sprite2D _overlay;
        private ShaderMaterial _material,_wallMaterial;
        private readonly Dictionary<TileMapLayer,Material> _originalWallMaterials=new();
        private void ReleaseWallMaterials()
        {
            foreach(var pair in _originalWallMaterials)
                if(IsInstanceValid(pair.Key) && pair.Key.Material==_wallMaterial) pair.Key.Material=pair.Value;
            _originalWallMaterials.Clear();
        }
        private CanvasItem _background;
        private Color _backgroundBase=Colors.White;
        private Color _backgroundApplied=Colors.White;
        private void RestoreBackground()
        {
            if(IsInstanceValid(_background) && _background.Modulate==_backgroundApplied)
                _background.Modulate=_backgroundBase;
            _background=null;
        }
        private void UpdateBackground(Color color)
        {
            var target=GetParent().GetNodeOrNull<CanvasItem>("Background");
            if(target!=_background) {
                RestoreBackground();_background=target;
                if(target!=null) _backgroundBase=target.Modulate;
            }
            if(target==null) return;
            var tinted=_backgroundBase*new Color(color.R,color.G,color.B,1);
            if(target.Modulate!=tinted) target.Modulate=tinted;
            _backgroundApplied=tinted;
        }
        private bool _previewDirty=true;
        private readonly Dictionary<TileMapLayer,long> _previewRevisions=new();
        private readonly HashSet<TileMapLayer> _previewWatched=new();
        private void PreviewChanged() => _previewDirty=true;
        public override void _ExitTree()
        {
            RestoreBackground();
            ReleaseWallMaterials();
            DisableWindowBeam();
            foreach(var layer in _previewWatched) if(IsInstanceValid(layer)) layer.Changed -= PreviewChanged;
        }
        private Shader _debugShader;
        private bool _debug;
        private ImageTexture _texture;
        private Camera2D _camera;
        private bool _invalid=true, _published, _building;
        private double _poll;
        private long _revision=-1;
        private Vector2I _origin, _size;
        private float _angle, _penumbra;
        private readonly LightMapData _defaults = new();

        private float Setting(string name, float fallback)
        {
            if (Settings == null) return fallback;
            var value=Settings.Get(name);
            return value.VariantType == Variant.Type.Nil ? fallback : value.AsSingle();
        }
        public void Invalidate() => _invalid=true;
        public void DetachWorld()
        {
            RestoreBackground();
            ReleaseWallMaterials();
            DisableWindowBeam();
            DimensionId=null; World=null; _published=false; _building=false; _invalid=true;
            if (IsInstanceValid(_overlay)) _overlay.Visible=false;
        }
        private void Resolve()
        {
            foreach (var path in Layers)
                if (GetNodeOrNull<TileMapLayer>(path) is {} layer && layer is not Jogo25D.Blocks.BackgroundWallLayer && layer.Name!="Base") _layers.Add(layer);
            if (_layers.Count==0)
                foreach (var child in GetParent().GetChildren())
                    if (child is TileMapLayer layer && layer is not Jogo25D.Blocks.BackgroundWallLayer && layer.Name!="Base") _layers.Add(layer);
            foreach(var child in GetParent().GetChildren()) if(child is Jogo25D.Blocks.BackgroundWallLayer wall) _walls.Add(wall);
            // Base fills physical terrain cells that Compose does not contain.
            var baseLayer=GetParent().GetNodeOrNull<TileMapLayer>("Base");
            if(baseLayer!=null && !_layers.Contains(baseLayer)) _layers.Add(baseLayer);
            if(Engine.IsEditorHint())
            {
                foreach(var layer in _layers) if(_previewWatched.Add(layer)) layer.Changed += PreviewChanged;
                foreach(var layer in _walls) if(_previewWatched.Add(layer)) layer.Changed += PreviewChanged;
            }
            _camera=Camera!=null && !Camera.IsEmpty?GetNodeOrNull<Camera2D>(Camera):GetParent().GetNodeOrNull<Camera2D>("Camera2D");
        }
        public override void _Process(double delta)
        {
            UpdateEditorPreview();
            var terrainOverlay=GetParent().GetNodeOrNull<Node2D>("LightOverlay");
            if(terrainOverlay!=null) terrainOverlay.Visible=LightMapEnabled;
            if (!LightMapEnabled || (Engine.IsEditorHint() && !PreviewInEditor))
            { if (IsInstanceValid(_overlay)) _overlay.Visible=false; ReleaseWallMaterials(); RestoreBackground(); DisableWindowBeam(); return; }
            if (_layers.Count==0) Resolve();
            if (_layers.Count==0 || _layers[0].TileSet==null) return;
            var grid=_layers[0];
            if (Engine.IsEditorHint())
            {
                _poll+=delta;
                if(_poll>=0.25)
                    foreach(var layer in _previewWatched)
                    {
                        long revision=EditorTileRevision.Get(layer);
                        if(!_previewRevisions.TryGetValue(layer,out long oldRevision) || oldRevision!=revision)
                        { _previewRevisions[layer]=revision;_previewDirty=true; }
                    }
                if (World==null || _invalid || (_previewDirty && _poll>=0.25))
                {
                    _poll=0;_previewDirty=false;
                    World ??= new LogicalLightWorld(0,"shadow-preview",1,false) { DepthLightEnabled=true };
                    var cells=new Dictionary<Vector2I,int>();
                    foreach (var layer in _layers) foreach (var c in layer.GetUsedCells()) cells[c]=LogicalLightWorld.TileTerrain(layer,c);
                    foreach (var c in _preview.Keys) if (!cells.ContainsKey(c)) World.SetTerrain(c.X,c.Y,-1);
                    foreach (var entry in cells)
                        if (!_preview.TryGetValue(entry.Key,out int old) || old!=entry.Value) World.SetTerrain(entry.Key.X,entry.Key.Y,entry.Value);
                    _preview=cells;
                    var walls=new HashSet<Vector2I>();
                    foreach(var layer in _walls) foreach(var cell in layer.GetUsedCells()) walls.Add(cell);
                    foreach(var cell in _previewWalls) if(!walls.Contains(cell)) World.SetBackground(cell.X,cell.Y,false);
                    foreach(var cell in walls) if(!_previewWalls.Contains(cell)) World.SetBackground(cell.X,cell.Y,true);
                    _previewWalls=walls;
                }
            }
            else
            {
                if (DimensionId==null) return;
                var world=Game.Managers.LightMapManager.Node?.GetWorld(DimensionId);
                if (world==null) return;
                if (world!=World) { World=world; _invalid=true; _published=false; _building=false; }
            }
            if (!IsInstanceValid(_overlay))
            {
                using var white=Image.CreateEmpty(1,1,false,Image.Format.Rgba8); white.Fill(Colors.White);
                _debugShader=GD.Load<Shader>("res://Assets/Shaders/projected_shadow_debug.gdshader");
                _material=new ShaderMaterial { Shader=_debugShader };
                _wallMaterial=new ShaderMaterial { Shader=GD.Load<Shader>("res://Assets/Shaders/wall_projected_shadow.gdshader") };
                _overlay=new Sprite2D { Name="ProjectedShadow",Centered=false,ZIndex=900,ZAsRelative=false,Material=_material,Texture=ImageTexture.CreateFromImage(white),Visible=false };
                AddChild(_overlay);
            }
            float angle=Setting(nameof(LightMapData.SunAngleDegrees),_defaults.SunAngleDegrees);
            float penumbra=Mathf.Clamp(Setting(nameof(LightMapData.SunPenumbra),_defaults.SunPenumbra),0,1);
            Vector2 tileSize=grid.TileSet.TileSize;
            Vector2 view=!Engine.IsEditorHint() && _camera!=null?_camera.GetViewportRect().Size/_camera.Zoom:(Vector2)PreviewSize*tileSize;
            var centre=grid.LocalToMap(grid.ToLocal(_camera?.GlobalPosition??GlobalPosition));
            int w=Mathf.CeilToInt(view.X/tileSize.X),h=Mathf.CeilToInt(view.Y/tileSize.Y);
            var origin=new Vector2I(LightingField.FloorChunk(centre.X-w/2)*32-32,LightingField.FloorChunk(centre.Y-h/2)*32-32);
            var size=new Vector2I(((w+31)/32+3)*32,((h+31)/32+3)*32);
            if (_invalid || _revision!=World.Revision || _angle!=angle || _penumbra!=penumbra || _origin!=origin || _size!=size)
            {
                _origin=origin; _size=size; _angle=angle; _penumbra=penumbra; _revision=World.Revision;
                _geometry.Begin(World,origin,size,angle,penumbra);
                _building=true; _invalid=false;
                // Never present geometry with a different coordinate transform.
                _published=false;
            }
            if (_building)
            {
                _geometry.Process(3);
                if (_geometry.Complete)
                {
                    using var data=_geometry.Image();
                    if (_texture==null) _texture=ImageTexture.CreateFromImage(data); else _texture.SetImage(data);
                    _material.SetShaderParameter("shadow_geometry",_texture);
                    _material.SetShaderParameter("map_size",(Vector2)_size*2);
                    _material.SetShaderParameter("sun_angle",Mathf.DegToRad(_angle));
                    _material.SetShaderParameter("penumbra",_penumbra);
                    _material.SetShaderParameter("geometry_ready",true);
                    var local=Transform2D.Identity.Scaled((Vector2)_size*tileSize);
                    local.Origin=grid.MapToLocal(_origin)-tileSize/2;
                    _overlay.GlobalTransform=grid.GlobalTransform*local;
                    _building=false; _published=true; SolarUpdates++;
                }
            }
            bool debug=Settings?.Get(nameof(LightMapData.DebugShadow)).AsBool() ?? false;
            if (debug!=_debug)
            {
                _debug=debug;
                _material.Shader=_debugShader;
                _material.SetShaderParameter("shadow_geometry",_texture);
                _material.SetShaderParameter("map_size",(Vector2)_size*2);
                _material.SetShaderParameter("sun_angle",Mathf.DegToRad(_angle));
                _material.SetShaderParameter("penumbra",_penumbra);
                _material.SetShaderParameter("geometry_ready",_published);
            }
            if (!_debug) _material.SetShaderParameter("shadow_strength",Setting(nameof(LightMapData.ShadowStrength),0.65f));
            _material.SetShaderParameter("penumbra_shadow_transition",Setting(nameof(LightMapData.SunPenumbraShadowCurve),1));
            _material.SetShaderParameter("penumbra_ambient_transition",Setting(nameof(LightMapData.SunPenumbraAmbientCurve),1));
            foreach(var wall in _walls)
                if(!_originalWallMaterials.ContainsKey(wall)) { _originalWallMaterials[wall]=wall.Material; wall.Material=_wallMaterial; }








            var colorValue=Settings?.Get(nameof(LightMapData.SunColor));
            Color sunColor=colorValue.HasValue && colorValue.Value.VariantType==Variant.Type.Color?colorValue.Value.AsColor():Colors.White;
            UpdateBackground(sunColor);
            _windowBeam.Update(this,World,_origin,_size,angle,penumbra,
                Setting(nameof(LightMapData.SunPenumbraShadowCurve),1),Setting(nameof(LightMapData.SunPenumbraAmbientCurve),1),
                Setting(nameof(LightMapData.TerrainLightDepthTiles),3));
            var chave=(_windowBeam.Revision,_texture?.GetRid()??default,_size,_angle,_penumbra,
                _published,_debug,IncludeSkylight,sunColor,
                Setting(nameof(LightMapData.ShadowStrength),0.65f),
                Setting(nameof(LightMapData.AmbientLightInfluence),0.75f),
                Setting(nameof(LightMapData.SunPenumbraShadowCurve),1),
                Setting(nameof(LightMapData.SunPenumbraAmbientCurve),1),
                grid.GlobalTransform);
            if(!_bindKey.Equals(chave)) { _bindKey=chave; _boundMaterials.Clear(); }
            void Ligar(ShaderMaterial material)
            {
                if(material==null || !_boundMaterials.Add(material)) return;
                _windowBeam.Bind(material,grid,!_debug,sunColor);
                BindSolarComposition(material);
            }
            Ligar(EditorPreview?.GetNodeOrNull<Sprite2D>("PreviewSprite")?.Material as ShaderMaterial);
            var runtimeOverlay=GetParent().GetNodeOrNull<Node2D>("LightOverlay");
            if(runtimeOverlay!=null) foreach(var child in runtimeOverlay.GetChildren())
                if(child is Sprite2D sprite) Ligar(sprite.Material as ShaderMaterial);



            _overlay.Visible=_published && _debug;
        }
        private void BindSolarComposition(ShaderMaterial material)
        {
            material.SetShaderParameter("include_skylight",IncludeSkylight);
            material.SetShaderParameter("shadow_geometry",_texture);
            material.SetShaderParameter("map_size",(Vector2)_size*2);
            material.SetShaderParameter("sun_angle",Mathf.DegToRad(_angle));
            material.SetShaderParameter("penumbra",_penumbra);
            material.SetShaderParameter("geometry_ready",_published && !_debug);
            material.SetShaderParameter("shadow_strength",Setting(nameof(LightMapData.ShadowStrength),0.65f));
            material.SetShaderParameter("ambient_light_influence",Setting(nameof(LightMapData.AmbientLightInfluence),0.75f));
            material.SetShaderParameter("penumbra_shadow_transition",Setting(nameof(LightMapData.SunPenumbraShadowCurve),1));
            material.SetShaderParameter("penumbra_ambient_transition",Setting(nameof(LightMapData.SunPenumbraAmbientCurve),1));
        }
        private void DisableWindowBeam()
        {
            // Este caminho escreve nos materiais por fora do cache; forca religar depois.
            _boundMaterials.Clear();
            var parent=GetParent();
            if(parent==null) return;
            if(EditorPreview?.GetNodeOrNull<Sprite2D>("PreviewSprite")?.Material is ShaderMaterial preview)
                preview.SetShaderParameter("window_beam_ready",false);
            var runtime=parent.GetNodeOrNull<Node2D>("LightOverlay");
            if(runtime!=null) foreach(var child in runtime.GetChildren())
                if(child is Sprite2D sprite && sprite.Material is ShaderMaterial material)
                    material.SetShaderParameter("window_beam_ready",false);
        }
    }
}
