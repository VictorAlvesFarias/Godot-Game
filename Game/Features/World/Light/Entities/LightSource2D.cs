using Godot;
using Jogo25D.Core;
using Jogo25D.Dimensions;

namespace Jogo25D.Light
{
    /// <summary>Transient RGB emitter for spells, characters and scene props. Stationary persistent
    /// sources can register directly with LightingField.SetSource independently of a scene node.</summary>
    [GlobalClass]
    public partial class LightSource2D : Node2D
    {
        [Export] public bool Enabled { get; set; } = true;
        [Export] public Color LightColor { get; set; } = new(1f, 0.65f, 0.3f);
        [Export(PropertyHint.Range, "0,1,0.01")] public float Energy { get; set; } = 1;
        private LogicalLightWorld _world;

        public override void _Process(double delta)
        {
            Dimension dimension = null;
            for (Node parent = GetParent(); parent != null; parent = parent.GetParent())
                if (parent is Dimension found) { dimension = found; break; }
            if (dimension == null) return;
            var world = Game.Managers.LightMapManager.Node?.GetWorld(dimension.DimensionId);
            if (world == null) return;
            long id = unchecked((long)GetInstanceId());
            if (world != _world) { _world?.Field.RemoveSource(id); _world = world; }
            var grid = Game.Managers.DimensionManager.Node.ResolveLayer(dimension.DimensionId);
            var cell = grid.LocalToMap(grid.ToLocal(GlobalPosition));
            byte Channel(float value) => (byte)Mathf.RoundToInt(Mathf.Clamp(value * Energy, 0, 1) * 255);
            world.Field.SetSource(id, new(cell.X, cell.Y), Enabled ? new(0, Channel(LightColor.R), Channel(LightColor.G), Channel(LightColor.B)) : default);
        }

        public override void _ExitTree() => _world?.Field.RemoveSource(unchecked((long)GetInstanceId()));
    }
}
