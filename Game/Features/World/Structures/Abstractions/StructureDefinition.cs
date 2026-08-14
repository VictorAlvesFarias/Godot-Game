using Godot;
using System.Collections.Generic;

namespace Jogo25D.Structures
{
    public abstract class StructureDefinition
    {
        #region Dinamic properties

        public string Id { get; init; }

        public float Chance { get; init; }

        #endregion

        #region Core - Abstract

        public abstract StructureBounds GetBounds(long worldSeed, string dimensionId, int worldX, int worldScale);

        public abstract List<StructureCellGroup> CollectCells(Vector2I groundCell, long worldSeed, string dimensionId, int worldScale);

        #endregion

        #region Core - Virtuals

        public virtual int GetMaxRightExtent(int worldScale) => 0;

        #endregion
    }
}
