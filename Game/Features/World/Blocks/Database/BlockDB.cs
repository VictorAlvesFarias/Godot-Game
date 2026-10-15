using Godot;
using Jogo25D.Constants;
using Jogo25D.Structures;
using System.Collections.Generic;

namespace Jogo25D.Blocks
{
    public static class BlockDB
    {
        #region Dinamic properties

        private static readonly Dictionary<string, BlockDefinition> _blocks = new()
        {
            ["wall_wood"] = new BlockDefinition
            {
                Id = "wall_wood",
                DropItemId = "wall_wood",
                IsBackground = true,
                SourceId = TerrainsConstants.WOOD,
                AtlasCoord = new Vector2I(1, 1)
            },
            ["wall_dirt"] = new BlockDefinition
            {
                Id = "wall_dirt",
                DropItemId = "wall_dirt",
                IsBackground = true,
                SourceId = 0,
                AtlasCoord = new Vector2I(1, 1)
            },
            // Terreno natural de cada bioma: terra (a grama do topo e so a textura de composicao).
            ["dirt"] = new BlockDefinition
            {
                Id = "dirt",
                DropItemId = "block_dirt",
                SourceId = 0,
                AtlasCoord = new Vector2I(1, 0),
                TerrainSet = 0,
            },
            ["olive_dirt"] = new BlockDefinition
            {
                Id = "olive_dirt",
                DropItemId = "block_dirt",
                SourceId = 1,
                AtlasCoord = new Vector2I(1, 0),
                TerrainSet = 1,
            },
            ["wood"] = new BlockDefinition
            {
                Id = "wood",
                DropItemId = "item_wood",
                SourceId = TerrainsConstants.WOOD,
                AtlasCoord = new Vector2I(1, 1),
                TerrainSet = TerrainsConstants.WOOD,
            },
            ["leaf"] = new BlockDefinition
            {
                Id = "leaf",
                DropItemId = "item_leaf",
                SourceId = TerrainsConstants.LIME_LEAF,
                AtlasCoord = new Vector2I(1, 1),
                TerrainSet = TerrainsConstants.LIME_LEAF,
            },
        };

        #endregion

        #region Core - Consulta

        public static bool TryGet(string id, out BlockDefinition definition)
        {
            return _blocks.TryGetValue(id, out definition);
        }

        public static IEnumerable<BlockDefinition> All()
        {
            return _blocks.Values;
        }

        // Bloco de terreno (nao parede) que pinta este terreno: o do bioma ou um colocado pelo jogador.
        public static BlockDefinition FindByTerrain(int terrain)
        {
            foreach (var block in _blocks.Values)
            {
                if (!block.IsBackground && (block.TerrainSet ?? block.SourceId) == terrain)
                {
                    return block;
                }
            }

            return null;
        }

        #endregion
    }
}
