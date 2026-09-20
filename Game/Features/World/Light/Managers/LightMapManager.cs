using Godot;
using Jogo25D.Blocks;
using Jogo25D.Constants;
using Jogo25D.Core;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    public partial class LightMapManager : Node
    {
        #region Dinamic properties

        public Dictionary<string, LightMap2D> Nodes { get; } = new();

        private readonly Dictionary<string, LogicalLightWorld> _worlds = new();

        private bool _authored;

        #endregion

        #region Core - Mundos

        public void UseProceduralWorlds()
        {
            _authored = false;

            _worlds.Clear();
        }

        public void UseAuthoredWorlds()
        {
            _authored = true;

            _worlds.Clear();

            var dimensions = Game.Managers.DimensionManager.Node;

            foreach (var id in dimensions.Ids)
            {
                var world = GetWorld(id);

                LoadAuthoredLayer(world, dimensions.ResolveLayer(id));
                LoadAuthoredLayer(world, dimensions.ResolveBaseLayer(id));
            }
        }

        public LogicalLightWorld GetWorld(string dimensionId)
        {
            var streaming = Game.Managers.TileStreamingManager.Node;
            var seed = streaming?.WorldSeed ?? 0;
            var referenceSize = ChunkStreamingConstants.REFERENCE_TILE_SIZE;
            var scale = Mathf.Max(1, Mathf.RoundToInt((float)referenceSize / (streaming?.TileSize ?? referenceSize)));

            if (_worlds.TryGetValue(dimensionId, out var world) && world.Seed == seed && world.WorldScale == scale)
            {
                return world;
            }

            _worlds[dimensionId] = world = new LogicalLightWorld(seed, dimensionId, scale, !_authored);

            var walls = Game.Managers.DimensionManager.Node?.ResolveParent(dimensionId)?.GetNodeOrNull<BackgroundWallLayer>("BackgroundWalls");

            if (walls != null)
            {
                foreach (var cell in walls.LogicalCells)
                {
                    world.SetBackground(cell.X, cell.Y, true);
                }
            }

            return world;
        }

        public void ReplaceWorld(string dimensionId, bool procedural)
        {
            var current = GetWorld(dimensionId);

            _worlds[dimensionId] = new LogicalLightWorld(current.Seed, dimensionId, current.WorldScale, procedural);
        }

        public void SetCell(string dimensionId, Vector2I cell, string type, string blockId = "")
        {
            if (dimensionId == null)
            {
                return;
            }

            GetWorld(dimensionId).ApplyMutation(cell.X, cell.Y, type, blockId);
        }

        private static void LoadAuthoredLayer(LogicalLightWorld world, TileMapLayer layer)
        {
            if (layer == null)
            {
                return;
            }

            foreach (var cell in layer.GetUsedCells())
            {
                world.SetTerrain(cell.X, cell.Y, LogicalLightWorld.TileTerrain(layer, cell));
            }
        }

        #endregion

        #region Core - Nodes

        public void AttachToDimensions()
        {
            var dimensions = Game.Managers.DimensionManager.Node;

            if (dimensions == null)
            {
                return;
            }

            Nodes.Clear();

            foreach (var id in dimensions.Ids)
            {
                var parent = dimensions.ResolveParent(id);

                if (parent == null)
                {
                    continue;
                }

                var node = ResolveNode(parent);

                if (node != null)
                {
                    Nodes[id] = node;
                    node.DimensionId = id;
                }
            }
        }

        public void Detach()
        {
            foreach (var node in Nodes.Values)
            {
                if (IsInstanceValid(node))
                {
                    node.DetachWorld();
                }
            }

            Nodes.Clear();
            _worlds.Clear();

            _authored = false;
        }

        public void Invalidate(string dimensionId)
        {
            if (dimensionId == null)
            {
                return;
            }

            if (Nodes.TryGetValue(dimensionId, out var node) && node != null && IsInstanceValid(node))
            {
                node.Invalidate();
            }
        }

        private static LightMap2D ResolveNode(Node2D parent)
        {
            foreach (var child in parent.GetChildren())
            {
                if (child is LightMap2D node)
                {
                    return node;
                }
            }

            return null;
        }

        #endregion
    }
}
