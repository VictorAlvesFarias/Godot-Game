using Godot;
using System.Collections.Generic;

namespace Jogo25D.Structures
{
    public struct StructureCellGroup
    {
        public int TerrainSet { get; set; }
        public List<Vector2I> Cells { get; set; }

        public StructureCellGroup(int terrainSet, List<Vector2I> cells)
        {
            TerrainSet = terrainSet;
            Cells = cells;
        }
    }
}
