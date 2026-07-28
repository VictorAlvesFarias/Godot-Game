using Godot;
using System.Collections.Generic;

namespace Jogo25D.Blocks
{
    public class BlockDefinition
    {
        public string Id { get; init; }
        public string DropItemId { get; init; }
        public int SourceId { get; init; }
        public Vector2I AtlasCoord { get; init; }
    }

    // Lookup de "que bloco eh esse" - so 1 entrada por enquanto (grama), ja
    // que o mundo procedural so tem um unico terreno ("Dirt 0", ver
    // ChunkGenerator). SourceId=7/AtlasCoord=(1,0) e a variante "cercada
    // por todos os lados" do atlas de chao (TX Tileset Ground.png),
    // validada como valida tanto no TileSet do mundo a mao (Upsidedown-
    // Tiles) quanto no procedural (ProceduralTiles) - os dois usam o
    // mesmo atlas nesse source id.
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
