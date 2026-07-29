using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.World.Chunks.Resources
{
    public partial class ChunkStateData : Resource
    {
        [Export, GodotDictionaryField]
        public Godot.Collections.Array<ChunkMutationData> Mutations { get; set; } = new();
    }
}
