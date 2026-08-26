using Godot;
using Jogo25D.Features.World.Chunks.Resources;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.Managers.Save.Resources
{
    [SaveType("chunk_entry")]
    public partial class ChunkEntryData : Resource
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public int ChunkCoordX { get; set; }

        [Export, GodotDictionaryField]
        public int ChunkCoordY { get; set; }

        [Export, GodotDictionaryField]
        public ChunkStateData State { get; set; } = new ChunkStateData();

        #endregion
    }
}
