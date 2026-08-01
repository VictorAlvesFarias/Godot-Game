using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.Managers.Save.Resources
{
    public partial class DimensionSaveData : Resource
    {
        [Export, GodotDictionaryField]
        public Godot.Collections.Array<ChunkEntryData> Chunks { get; set; } = new();
    }
}
