using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.World.Chunks.Resources
{
    public partial class ChunkMutationData : Resource
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public string Type { get; set; } = "";

        [Export, GodotDictionaryField]
        public Vector2 Position { get; set; } = Vector2.Zero;

        [Export, GodotDictionaryField]
        public string ExtraData { get; set; } = "";

        #endregion
    }
}
