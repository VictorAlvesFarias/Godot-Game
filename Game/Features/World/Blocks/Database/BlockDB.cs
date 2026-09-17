using Godot;
using Jogo25D.Constants;
using Jogo25D.Structures;
using System.Collections.Generic;

namespace Jogo25D.Blocks
{
    public static class BlockDB
    {
        private static readonly Dictionary<string, BlockDefinition> _blocks = new()
        {
            ["wall_wood"] = new BlockDefinition { Id = "wall_wood", DropItemId = "wall_wood", IsBackground = true, SourceId = TerrainsConstants.WOOD, AtlasCoord = new Vector2I(1, 1) },
            ["wall_dirt"] = new BlockDefinition { Id = "wall_dirt", DropItemId = "wall_dirt", IsBackground = true, SourceId = 0, AtlasCoord = new Vector2I(1, 1) },
            ["grass"] = new BlockDefinition
            {
                Id = "grass",
                DropItemId = "block_grass",
                SourceId = 0,
                AtlasCoord = new Vector2I(1, 0),
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

        public static bool TryGet(string id, out BlockDefinition definition)
        {
            return _blocks.TryGetValue(id, out definition);
        }

        public static IEnumerable<BlockDefinition> All()
        {
            return _blocks.Values;
        }
    }
}
