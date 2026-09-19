using Godot;
namespace Jogo25D.Light;

// Render only on geometry/solar changes. Sampling this field costs one lookup per frame.
internal sealed class WindowBeamCache
{
    private SubViewport _viewport;
    private ShaderMaterial _material;
    private ImageTexture _cells;
    private LogicalLightWorld _world;
    private long _revision=-1,_background=-1;
    private Vector2I _origin,_size;
    private float _angle=-999,_penumbra=-999,_shadowCurve=-999,_ambientCurve=-999,_depth=-999;
    public void Update(Node parent,LogicalLightWorld world,Vector2I origin,Vector2I size,float angle,float penumbra,float shadowCurve=1,float ambientCurve=1,float terrainDepth=3)
    {
        if(_viewport==null) {
            _material=new ShaderMaterial { Shader=GD.Load<Shader>("res://Assets/Shaders/window_beam_cache.gdshader") };
            _viewport=new SubViewport { Name="WindowBeamCache",Disable3D=true,TransparentBg=true,RenderTargetUpdateMode=SubViewport.UpdateMode.Disabled };
            _viewport.AddChild(new ColorRect { Name="Field",Material=_material,MouseFilter=Control.MouseFilterEnum.Ignore });
            parent.AddChild(_viewport);
        }
        if(_world==world && _revision==world.Revision && _background==world.BackgroundRevision && _origin==origin && _size==size && _angle==angle && _penumbra==penumbra && _shadowCurve==shadowCurve && _ambientCurve==ambientCurve && _depth==terrainDepth) return;
        _world=world;_revision=world.Revision;_background=world.BackgroundRevision;_origin=origin;_size=size;_angle=angle;_penumbra=penumbra;_shadowCurve=shadowCurve;_ambientCurve=ambientCurve;_depth=terrainDepth;
        var bytes=SkyAccessField.Build(world,origin,size,terrainDepth);
        using var image=Image.CreateFromData(size.X,size.Y,false,Image.Format.Rgba8,bytes);
        if(_cells==null) _cells=ImageTexture.CreateFromImage(image); else _cells.SetImage(image);
        _viewport.Size=size*4;
        _viewport.GetNode<ColorRect>("Field").Size=size*4;
        _material.SetShaderParameter("cells",_cells);
        _material.SetShaderParameter("grid_size",(Vector2)size);
        _material.SetShaderParameter("angle",Mathf.DegToRad(angle));
        _material.SetShaderParameter("aperture",Mathf.DegToRad(8)*(1-penumbra)*(1-penumbra));
        _material.SetShaderParameter("shadow_curve",shadowCurve);
        _material.SetShaderParameter("ambient_curve",ambientCurve);
        _viewport.RenderTargetUpdateMode=SubViewport.UpdateMode.Once;
        _viewport.GetNode<ColorRect>("Field").QueueRedraw();
    }
    public void Bind(ShaderMaterial material,TileMapLayer grid,bool enabled,Color sunColor=default)
    {
        if(material==null) return;
        material.SetShaderParameter("window_sun_color",sunColor==default?Colors.White:sunColor);
        material.SetShaderParameter("window_beam_ready",enabled && _viewport!=null);
        if(_viewport==null) return;
        var inverse=grid.GlobalTransform.AffineInverse();
        Vector2 tile=grid.TileSet.TileSize;
        material.SetShaderParameter("window_beam",_viewport.GetTexture());
        material.SetShaderParameter("window_cells",_cells);
        material.SetShaderParameter("sky_access",_cells);
        material.SetShaderParameter("beam_origin",grid.ToGlobal(grid.MapToLocal(_origin)-tile/2));
        material.SetShaderParameter("beam_axis_x",new Vector2(inverse.X.X,inverse.Y.X)/tile.X);
        material.SetShaderParameter("beam_axis_y",new Vector2(inverse.X.Y,inverse.Y.Y)/tile.Y);
        material.SetShaderParameter("beam_size",(Vector2)_size);
    }
}
