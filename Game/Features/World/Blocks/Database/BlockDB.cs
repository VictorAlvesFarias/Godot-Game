using Godot;
using System.Collections.Generic;

namespace Jogo25D.Blocks
{
    public static class BlockDB
    {
        private static readonly Dictionary<string, BlockDefinition> _blocks = new()
        {
            ["grass"] = new BlockDefinition
            {
                Id = "grass",
                DropItemId = "block_grass",
                SourceId = 7,
                AtlasCoord = new Vector2I(1, 0),
            },
        };

        public static bool TryGet(string id, out BlockDefinition definition)
        {
            return _blocks.TryGetValue(id, out definition);
        }
    }
}
