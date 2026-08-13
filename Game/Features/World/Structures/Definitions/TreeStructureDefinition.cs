using Godot;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using System.Collections.Generic;

namespace Jogo25D.Structures
{
    public class TreeStructureDefinition : StructureDefinition
    {
        public const int WoodTerrainSet = 6;
        public const int LeafTerrainSet = 7;

        private static readonly int[] _terrainSets = { WoodTerrainSet, LeafTerrainSet };

        public override IReadOnlyCollection<int> TerrainSets => _terrainSets;

        #region Formato

        private sealed class TreeShape
        {
            public int TrunkHeight;
            public int CanopyHeight;
            public int TrunkLean;
            public int[] RadiusByRow;
        }

        private TreeShape GenerateShape(long worldSeed, string dimensionId, int worldX)
        {
            var trunkHeight = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, 0, (7, 12));
            var canopyHeight = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, 1, (5, 9));
            var maxRadius = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, 2, (3, 6));
            var trunkLean = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, 3, (-1, 1));

            var radiusByRow = new int[canopyHeight];

            for (int row = 0; row < canopyHeight; row++)
            {
                var normalized = canopyHeight <= 1 ? 0f : (float)row / (canopyHeight - 1);
                float taper;

                if (normalized < 0.20f)
                {
                    taper = normalized / 0.20f;
                }
                else if (normalized > 0.80f)
                {
                    taper = (1f - normalized) / 0.20f;
                }
                else
                {
                    taper = 1f;
                }

                var variation = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX + row, 100, (-1, 1));
                var radius = Mathf.RoundToInt(maxRadius * taper) + variation;

                radiusByRow[row] = Mathf.Max(0, radius);
            }

            return new TreeShape
            {
                TrunkHeight = trunkHeight,
                CanopyHeight = canopyHeight,
                TrunkLean = trunkLean,
                RadiusByRow = radiusByRow,
            };
        }

        private static Vector2I TrunkPosition(TreeShape shape, int step)
        {
            var progress = shape.TrunkHeight <= 1 ? 0f : (float)step / shape.TrunkHeight;
            var x = Mathf.RoundToInt(shape.TrunkLean * progress);

            return new Vector2I(x, step);
        }

        #endregion

        #region Geracao das celulas (relativas ao chao, X=0/Y=0)

        private void BuildTree(long worldSeed, string dimensionId, int worldX, List<Vector2I> trunkCells, List<Vector2I> canopyCells)
        {
            var shape = GenerateShape(worldSeed, dimensionId, worldX);
            var trunkCellSet = new HashSet<Vector2I>();
            var canopyCellSet = new HashSet<Vector2I>();

            for (int step = 1; step <= shape.TrunkHeight; step++)
            {
                var trunkCell = TrunkPosition(shape, step);

                if (trunkCellSet.Add(trunkCell))
                {
                    trunkCells.Add(trunkCell);
                }
            }

            for (int row = 0; row < shape.CanopyHeight; row++)
            {
                var radius = shape.RadiusByRow[row];
                var normalized = shape.CanopyHeight <= 1 ? 0f : (float)row / (shape.CanopyHeight - 1);
                var centerX = shape.TrunkLean + Mathf.RoundToInt(shape.TrunkLean * normalized);
                var y = shape.TrunkHeight + row;

                for (int x = -radius; x <= radius; x++)
                {
                    var isEdge = Mathf.Abs(x) >= radius - 1;

                    if (isEdge)
                    {
                        var edgeRoll = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX + centerX + x, 500 + row, (0, 5));

                        if (edgeRoll == 0)
                        {
                            continue;
                        }
                    }

                    var canopyCell = new Vector2I(centerX + x, y);

                    if (trunkCellSet.Contains(canopyCell) || !canopyCellSet.Add(canopyCell))
                    {
                        continue;
                    }

                    canopyCells.Add(canopyCell);
                }
            }

            AddLeafClusters(canopyCells, trunkCellSet, canopyCellSet, shape, worldSeed, dimensionId, worldX);
            AddBranches(trunkCells, trunkCellSet, canopyCells, canopyCellSet, shape, worldSeed, dimensionId, worldX);
        }

        private void AddLeafClusters(List<Vector2I> canopyCells, HashSet<Vector2I> trunkCellSet, HashSet<Vector2I> canopyCellSet, TreeShape shape, long worldSeed, string dimensionId, int worldX)
        {
            var clusterCount = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, 800, (3, 7));

            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                var salt = 810 + cluster * 10;
                var row = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, salt, (0, shape.CanopyHeight - 1));
                var radius = shape.RadiusByRow[row];
                var x = radius > 0 ? WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, salt + 1, (-radius, radius)) : 0;
                var clusterRadius = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, salt + 2, (1, 3));

                var centerX = shape.TrunkLean + x;
                var centerY = shape.TrunkHeight + row;

                for (int dx = -clusterRadius; dx <= clusterRadius; dx++)
                {
                    for (int dy = -clusterRadius; dy <= clusterRadius; dy++)
                    {
                        if (dx * dx + dy * dy > clusterRadius * clusterRadius)
                        {
                            continue;
                        }

                        var canopyCell = new Vector2I(centerX + dx, centerY + dy);

                        if (trunkCellSet.Contains(canopyCell) || !canopyCellSet.Add(canopyCell))
                        {
                            continue;
                        }

                        canopyCells.Add(canopyCell);
                    }
                }
            }
        }

        private void AddBranches(List<Vector2I> trunkCells, HashSet<Vector2I> trunkCellSet, List<Vector2I> canopyCells, HashSet<Vector2I> canopyCellSet, TreeShape shape, long worldSeed, string dimensionId, int worldX)
        {
            var branchCount = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, 700, (2, 4));

            for (int branch = 0; branch < branchCount; branch++)
            {
                var branchSalt = 710 + branch * 10;

                var drop = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, branchSalt, (0, 2));
                var step = Mathf.Max(1, shape.TrunkHeight - drop);

                var direction = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, branchSalt + 1, (0, 1)) == 0 ? -1 : 1;
                var length = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, branchSalt + 2, (2, 4));

                var start = TrunkPosition(shape, step);

                for (int i = 1; i <= length; i++)
                {
                    var verticalOffset = Mathf.RoundToInt(i * 0.35f);
                    var position = start + new Vector2I(direction * i, verticalOffset);

                    if (trunkCellSet.Add(position))
                    {
                        trunkCells.Add(position);
                    }

                    var leafRadius = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX + i, branchSalt + 3, (1, 2));

                    for (int lx = -leafRadius; lx <= leafRadius; lx++)
                    {
                        for (int ly = -leafRadius; ly <= leafRadius; ly++)
                        {
                            if (Mathf.Abs(lx) + Mathf.Abs(ly) > leafRadius + 1)
                            {
                                continue;
                            }

                            var canopyCell = position + new Vector2I(lx, ly);

                            if (trunkCellSet.Contains(canopyCell) || !canopyCellSet.Add(canopyCell))
                            {
                                continue;
                            }

                            canopyCells.Add(canopyCell);
                        }
                    }
                }
            }
        }

        #endregion

        #region StructureDefinition

        public override StructureBounds GetBounds(long worldSeed, string dimensionId, int worldX, int worldScale)
        {
            var trunkCells = new List<Vector2I>();
            var canopyCells = new List<Vector2I>();

            BuildTree(worldSeed, dimensionId, worldX, trunkCells, canopyCells);

            var left = 0;
            var right = 0;
            var top = 0;

            void Measure(Vector2I cell)
            {
                if (cell.X < 0) left = Mathf.Max(left, -cell.X);
                if (cell.X > 0) right = Mathf.Max(right, cell.X);
                top = Mathf.Max(top, cell.Y);
            }

            foreach (var cell in trunkCells) Measure(cell);
            foreach (var cell in canopyCells) Measure(cell);

            return new StructureBounds(left, right, top);
        }

        public override int GetMaxRightExtent(int worldScale)
        {
            return StructurePlacementConstants.TreeMaxRightExtent;
        }

        public override List<StructureCellGroup> CollectCells(Vector2I groundCell, long worldSeed, string dimensionId, int worldScale)
        {
            var trunkCells = new List<Vector2I>();
            var canopyCells = new List<Vector2I>();

            BuildTree(worldSeed, dimensionId, groundCell.X, trunkCells, canopyCells);

            var absoluteTrunk = new List<Vector2I>(trunkCells.Count);
            var absoluteCanopy = new List<Vector2I>(canopyCells.Count);

            foreach (var cell in trunkCells) absoluteTrunk.Add(groundCell + new Vector2I(cell.X, -cell.Y));
            foreach (var cell in canopyCells) absoluteCanopy.Add(groundCell + new Vector2I(cell.X, -cell.Y));

            return new List<StructureCellGroup>
            {
                new StructureCellGroup(WoodTerrainSet, absoluteTrunk),
                new StructureCellGroup(LeafTerrainSet, absoluteCanopy),
            };
        }

        #endregion
    }
}
