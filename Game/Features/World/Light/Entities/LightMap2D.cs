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
        public string DimensionId { get; set; }
        public LogicalLightWorld World { get; private set; }
        public bool PresentationReady => _published && !_building && World != null && _revision == World.Revision;
        public long SolarUpdates { get; private set; }
        private readonly AnalyticShadowGeometry _geometry = new();
        private readonly List<TileMapLayer> _layers = new();
        private Dictionary<Vector2I,int> _preview = new();
        private Sprite2D _overlay;
        private ShaderMaterial _material;
        private Shader _normalShader, _debugShader;
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
            _camera=Camera!=null && !Camera.IsEmpty?GetNodeOrNull<Camera2D>(Camera):GetParent().GetNodeOrNull<Camera2D>("Camera2D");
        }
        public override void _Process(double delta)
        {
            if (!LightMapEnabled || (Engine.IsEditorHint() && !PreviewInEditor))
            { if (IsInstanceValid(_overlay)) _overlay.Visible=false; return; }
            if (_layers.Count==0) Resolve();
            if (_layers.Count==0 || _layers[0].TileSet==null) return;
            var grid=_layers[0];
            if (Engine.IsEditorHint())
            {
                _poll+=delta;
                if (World==null || _invalid || _poll>=0.25)
                {
                    _poll=0;
                    World ??= new LogicalLightWorld(0,"shadow-preview",1,false);
                    var cells=new Dictionary<Vector2I,int>();
                    foreach (var layer in _layers) foreach (var c in layer.GetUsedCells()) cells[c]=LogicalLightWorld.TileTerrain(layer,c);
                    foreach (var c in _preview.Keys) if (!cells.ContainsKey(c)) World.SetTerrain(c.X,c.Y,-1);
                    foreach (var entry in cells)
                        if (!_preview.TryGetValue(entry.Key,out int old) || old!=entry.Value) World.SetTerrain(entry.Key.X,entry.Key.Y,entry.Value);
                    _preview=cells;
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
                _normalShader=GD.Load<Shader>("res://Assets/Shaders/projected_shadow.gdshader");
                _debugShader=GD.Load<Shader>("res://Assets/Shaders/projected_shadow_debug.gdshader");
                _material=new ShaderMaterial { Shader=_normalShader };
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
                _material.Shader=debug?_debugShader:_normalShader;
                _material.SetShaderParameter("shadow_geometry",_texture);
                _material.SetShaderParameter("map_size",(Vector2)_size*2);
                _material.SetShaderParameter("sun_angle",Mathf.DegToRad(_angle));
                _material.SetShaderParameter("penumbra",_penumbra);
                _material.SetShaderParameter("geometry_ready",_published);
            }
            if (!_debug) _material.SetShaderParameter("shadow_strength",Setting(nameof(LightMapData.ShadowStrength),0.65f));
            _material.SetShaderParameter("penumbra_shadow_transition",Setting(nameof(LightMapData.SunPenumbraShadowCurve),1));
            _material.SetShaderParameter("penumbra_ambient_transition",Setting(nameof(LightMapData.SunPenumbraAmbientCurve),1));
            _overlay.Visible=_published;
        }
    }
}
