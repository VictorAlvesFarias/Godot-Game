using Godot;
using Jogo25D.Items;
using Jogo25D.Utils.GodotDictionaryParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jogo25D.Features.World.Items.Resources
{
    public partial class InventoryData : Resource
    {
        #region Properties

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<ItemData> Items { get; set; } = [];

        [Export, GodotDictionaryField]
        public int Size { get; set; } = 16;

        #endregion
    }
}
