using Godot;

namespace Jogo25D.Biomes
{
    [Tool]
    public partial class TerrainDebugOverlay : Node2D
    {
        public TerrainLayer TerrainLayerOwner { get; set; }

        public override void _Draw()
        {
            if (TerrainLayerOwner != null && TerrainLayerOwner.ShowTerrainSetDebug)
            {
                TerrainLayerOwner.DrawTerrainSetDebug(this);
            }
        }
    }
}
