using Godot;
using Jogo25D.Actions;
using Jogo25D.Effects;
using Jogo25D.Properties;
using Jogo25D.Utils.GodotDictionaryParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jogo25D.Features.World.Items.Resources
{
    public partial class ItemData : ItemDefinitionData
    {
        public ItemData() { }

        public ItemData(string id)
        {
            Id = id;
        }

        #region Properties

        [Export, GodotDictionaryField]
        public long InstanceId { get; set; }

        [Export, GodotDictionaryField]
        public int Quantity { get; set; }

        [Export, GodotDictionaryField]
        public int CurrentCharges { get; set; }

        #endregion

        #region Timers

        [Export, GodotDictionaryField]
        public float ReloadTimer { get; set; }

        [Export, GodotDictionaryField]
        public float CooldownRemainingTimer { get; set; }

        #endregion
    }
}
