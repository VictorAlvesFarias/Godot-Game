using Godot;
using Jogo25D.Light;
using System;
namespace Jogo25D.Testing;
[Tool]
public partial class LightPerformanceEditorCheck : Node
{
    private LightingEditorPreview _preview;
    private double _time;
    private int _stage;
    private long _count;
    private TileMapLayer _base,_compose;
    private int _source;
    private Vector2I _atlas;
    private byte[] _before;
    private byte[] Pixels() => _preview.GetNode<Sprite2D>("PreviewSprite").Texture.GetImage().GetData();
    private void Patch(TileMapLayer layer)
    { for(int y=-4;y<=4;y++) for(int x=-4;x<=4;x++) layer.SetCell(new(x,y),_source,_atlas); }
    private void CheckChanged(string label)
    {
        if(System.Linq.Enumerable.SequenceEqual(_before,Pixels())) throw new Exception(label+" did not change light pixels");
        GD.Print(label+" PASS: texture changed");
    }
    public override void _Ready()
    {
        if(!Engine.IsEditorHint() || System.Environment.GetEnvironmentVariable("LIGHT_PERF_EDITOR_TEST")!="1") { SetProcess(false);return; }
        foreach(var child in GetParent().GetChildren()) if(child is LightingEditorPreview preview) _preview=preview;
        if(_preview==null) { GD.PushError("Missing preview");GetTree().Quit(1); }
    }
    public override void _Process(double delta)
    {
        if(_preview==null) return;
        _time+=delta;
        if(_stage==0 && _preview.RebuildCount>0)
        {
            if(_count!=_preview.RebuildCount) { _count=_preview.RebuildCount;_time=0; }
            else if(_time>2) { _stage=1;_time=0; }
        }
        else if(_stage==1 && _time>3)
        {
            if(_preview.RebuildCount!=_count) { GD.PushError("Idle preview rebuilt");GetTree().Quit(1);return; }
            GD.Print("EDITOR IDLE PASS: zero rebuilds in 3 seconds");
            _preview.IncludeSkylight=!_preview.IncludeSkylight;_stage=2;_time=0;
        }
        else if(_stage==2 && _preview.RebuildCount>_count)
        {
            GD.Print("EDITOR SETTINGS PASS: invalidation rebuilt GPU output");
            _count=_preview.RebuildCount;
            var walls=GetParent().GetNode<TileMapLayer>("BackgroundWalls");
            var cells=walls.GetUsedCells();
            if(cells.Count==0) throw new Exception("No wall fixture");
            walls.EraseCell(cells[0]);_stage=3;_time=0;
        }
        else if(_stage==3 && _preview.RebuildCount>_count)
        {
            GD.Print("EDITOR WALL EDIT PASS");
            _base=GetParent().GetNode<TileMapLayer>("Base");_compose=GetParent().GetNode<TileMapLayer>("Compose");
            var cell=_base.GetUsedCells()[0];_source=_base.GetCellSourceId(cell);_atlas=_base.GetCellAtlasCoords(cell);
            _base.Clear();_compose.Clear();GetParent().GetNode<TileMapLayer>("BackgroundWalls").Clear();
            _base.SetCell(new(-16,-16),_source,_atlas);_base.SetCell(new(16,16),_source,_atlas);
            _preview.IncludeSkylight=true;_count=_preview.RebuildCount;_stage=4;_time=0;
        }
        else if(_stage==4 && _preview.RebuildCount>_count && _time>2)
        { _before=Pixels();Patch(_base);_count=_preview.RebuildCount;_stage=5;_time=0; }
        else if(_stage==5 && _preview.RebuildCount>_count && _time>2)
        {
            CheckChanged("EDITOR BASE PAINT");_before=Pixels();
            for(int y=-4;y<=4;y++) for(int x=-4;x<=4;x++) _base.EraseCell(new(x,y));
            _count=_preview.RebuildCount;_stage=6;_time=0;
        }
        else if(_stage==6 && _preview.RebuildCount>_count && _time>2)
        { CheckChanged("EDITOR BASE ERASE");_before=Pixels();Patch(_compose);_count=_preview.RebuildCount;_stage=7;_time=0; }
        else if(_stage==7 && _preview.RebuildCount>_count && _time>2)
        {
            CheckChanged("EDITOR COMPOSE PAINT");
            _count=_preview.RebuildCount;
            var parent=_preview.GetParent();parent.RemoveChild(_preview);parent.AddChild(_preview);
            _stage=8;_time=0;
        }
        else if(_stage==8 && _preview.RebuildCount>_count && _time>2)
        {
            _before=Pixels();_count=_preview.RebuildCount;_compose.Clear();_stage=9;_time=0;
        }
        else if(_stage==9 && _preview.RebuildCount>_count && _time>2)
        { CheckChanged("EDITOR REENTER AND EDIT");GetTree().Quit(); }
        if(_time>30) { GD.PushError("Preview timeout");GetTree().Quit(1); }
    }
}
