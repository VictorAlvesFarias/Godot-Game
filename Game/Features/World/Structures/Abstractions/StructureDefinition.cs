using Godot;
using System.Collections.Generic;

namespace Jogo25D.Structures
{

    public readonly struct StructureCellGroup
    {
        public readonly int TerrainSet;
        public readonly List<Vector2I> Cells;

        public StructureCellGroup(int terrainSet, List<Vector2I> cells)
        {
            TerrainSet = terrainSet;
            Cells = cells;
        }
    }

    public readonly struct StructureBounds
    {
        public readonly int Left;
        public readonly int Right;
        public readonly int Top;

        public StructureBounds(int left, int right, int top)
        {
            Left = left;
            Right = right;
            Top = top;
        }
    }

    public abstract class StructureDefinition
    {
        #region Dinamic properties

        public string Id { get; init; }

        public float Chance { get; init; }

        #endregion

        #region Core - Abstract

        public abstract IReadOnlyCollection<int> TerrainSets { get; }

        public abstract StructureBounds GetBounds(long worldSeed, string dimensionId, int worldX, int worldScale);

        public virtual int GetMaxRightExtent(int worldScale) => 0;

        public abstract List<StructureCellGroup> CollectCells(Vector2I groundCell, long worldSeed, string dimensionId, int worldScale);

        #endregion
    }
}
