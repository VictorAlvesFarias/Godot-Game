using Godot;
using Jogo25D.Core;
using Jogo25D.Dimensions;

namespace Jogo25D.Light
{
    /// <summary>Transient RGB emitter for spells, characters and scene props. Stationary persistent
    /// sources can register directly with LightingField.SetSource independently of a scene node.</summary>
    [Tool, GlobalClass]
    public partial class LightSource2D : Node2D
    {
        [Export] public bool Enabled { get; set; } = true;
        [Export] public Color LightColor { get; set; } = new(1f, 0.65f, 0.3f);
        [Export(PropertyHint.Range, "0,1,0.01")] public float Energy { get; set; } = 1;
        private LogicalLightWorld _world;
        private LightMap2D _preview;
        private TileMapLayer _grid;

        // Procura uma vez e guarda; o editor chama _Process todo frame.
        private static T Filho<T>(Node raiz) where T : Node
        {
            foreach (var filho in raiz.GetChildren()) if (filho is T achado) return achado;
            return null;
        }

        public override void _Process(double delta)
        {
            LogicalLightWorld world;
            TileMapLayer grid;
            if (Engine.IsEditorHint())
            {
                // No editor o script da dimensao nao e [Tool], entao o no raiz NAO e um Dimension:
                // procurar por Dimension aqui falha sempre. O que identifica a dimensao na previa e
                // ter um LightMap2D por filho - e e o mundo logico dele que vale, nao o do manager.
                if (!IsInstanceValid(_preview) || !IsInstanceValid(_grid))
                    for (Node parent = GetParent(); parent != null; parent = parent.GetParent())
                    {
                        var mapa = Filho<LightMap2D>(parent);
                        if (mapa == null) continue;
                        _preview = mapa;
                        _grid = Filho<TileMapLayer>(parent);
                        break;
                    }
                world = _preview?.World;
                grid = _grid;
            }
            else
            {
                Dimension dimension = null;
                for (Node parent = GetParent(); parent != null; parent = parent.GetParent())
                    if (parent is Dimension found) { dimension = found; break; }
                if (dimension == null) return;
                world = Game.Managers.LightMapManager.Node?.GetWorld(dimension.DimensionId);
                grid = Game.Managers.DimensionManager.Node?.ResolveLayer(dimension.DimensionId);
            }
            if (world == null || grid == null) return;

            long id = unchecked((long)GetInstanceId());
            if (world != _world) { _world?.Field.RemoveSource(id); _world = world; }
            var cell = grid.LocalToMap(grid.ToLocal(GlobalPosition));
            byte Channel(float value) => (byte)Mathf.RoundToInt(Mathf.Clamp(value * Energy, 0, 1) * 255);
            world.Field.SetSource(id, new(cell.X, cell.Y), Enabled ? new(0, Channel(LightColor.R), Channel(LightColor.G), Channel(LightColor.B)) : default);
        }

        public override void _ExitTree() => _world?.Field.RemoveSource(unchecked((long)GetInstanceId()));
    }
}
