using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.World.Items.Resources
{
    public partial class InventoryData : Resource
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<ItemData> Items { get; set; } = [];

        [Export, GodotDictionaryField]
        public int Size { get; set; } = 16;

        #endregion
    }
}
