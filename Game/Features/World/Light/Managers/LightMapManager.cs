using Godot;
using Jogo25D.Core;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    // Owns logical optical state per dimension. LightMap2D is only its presentation/cache client.
    public partial class LightMapManager : Node
    {
        #region Dinamic properties

        /// <summary>Os nos de mapa de luz achados, por id de dimensao.</summary>
        public Dictionary<string, LightMap2D> Nodes { get; } = new();
        private readonly Dictionary<string, LogicalLightWorld> _worlds = new();
        private bool _authored;

        public void UseProceduralWorlds()
        {
            _authored = false;
            _worlds.Clear();
        }

        public void UseAuthoredWorlds()
        {
            _authored = true;
            _worlds.Clear();
            foreach (var id in Game.Managers.DimensionManager.Node.Ids)
            {
                var world = GetWorld(id);
                var layer = Game.Managers.DimensionManager.Node.ResolveLayer(id);
                foreach (var cell in layer.GetUsedCells())
                    world.SetTerrain(cell.X, cell.Y, LogicalLightWorld.TileTerrain(layer, cell));
            }
        }

        public LogicalLightWorld GetWorld(string dimensionId)
        {
            var streaming = Game.Managers.TileStreamingManager.Node;
            long seed = streaming?.WorldSeed ?? 0;
            int scale = Mathf.Max(1, Mathf.RoundToInt(32f / (streaming?.TileSize ?? 32)));
            if (!_worlds.TryGetValue(dimensionId, out var world) || world.Seed != seed || world.WorldScale != scale)
                _worlds[dimensionId] = world = new LogicalLightWorld(seed, dimensionId, scale, !_authored);
            return world;
        }

        public void ReplaceWorld(string dimensionId, bool procedural)
        {
            var old = GetWorld(dimensionId);
            _worlds[dimensionId] = new LogicalLightWorld(old.Seed, dimensionId, old.WorldScale, procedural);
        }

        public void SetCell(string dimensionId, Vector2I cell, string type, string blockId = "")
        {
            if (dimensionId == null) return;
            GetWorld(dimensionId).ApplyMutation(cell.X, cell.Y, type, blockId);
        }

        #endregion

        #region Core

        /// <summary>
        /// Acha o LightMap2D de cada dimensao e registra. Chamado quando o mundo nasce;
        /// idempotente, rodar de novo so reescreve o registro. A dimensao sem o no simplesmente
        /// nao tem mapa de luz - a cena e que manda.
        /// </summary>
        public void AttachToDimensions()
        {
            var dimensoes = Game.Managers.DimensionManager.Node;

            if (dimensoes == null)
            {
                return;
            }

            Nodes.Clear();

            foreach (var id in dimensoes.Ids)
            {
                var parent = dimensoes.ResolveParent(id);

                if (parent == null)
                {
                    continue;
                }

                var no = ResolverNo(parent);

                if (no != null)
                {
                    Nodes[id] = no;
                    no.DimensionId = id;
                }
            }
        }

        /// <summary>Esquece os nos registrados. Chamado quando o mundo e destruido.</summary>
        public void Detach()
        {
            foreach (var node in Nodes.Values) if (IsInstanceValid(node)) node.DetachWorld();
            Nodes.Clear();
            _worlds.Clear();
            _authored = false;
        }

        /// <summary>
        /// Avisa o no da dimensao que um bloco nasceu ou sumiu. Nao recalcula na hora: o no marca
        /// a janela como suja e o proximo quadro a refaz inteira.
        /// </summary>
        public void Invalidate(string dimensionId)
        {
            if (dimensionId == null)
            {
                return;
            }

            if (Nodes.TryGetValue(dimensionId, out var no) && no != null && IsInstanceValid(no))
            {
                no.Invalidate();
            }
        }

        // A dimensao sem o no simplesmente nao tem mapa de luz; a cena e que manda.
        private static LightMap2D ResolverNo(Node2D parent)
        {
            foreach (var filho in parent.GetChildren())
            {
                if (filho is LightMap2D no)
                {
                    return no;
                }
            }

            return null;
        }

        #endregion
    }
}
