using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.World.Chunks.Resources
{
    [SaveType("chunk_state")]
    public partial class ChunkStateData : Resource
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<ChunkMutationData> Mutations { get; set; } = new();

        #endregion
    }
}
