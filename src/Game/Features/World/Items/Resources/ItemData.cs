using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.World.Items.Resources
{
    [SaveType("item")]
    public partial class ItemData : ItemDefinitionData
    {
        #region Constructors

        public ItemData() { }

        public ItemData(string id)
        {
            Id = id;
        }

        #endregion

        #region Dinamic properties

        [Export, GodotDictionaryField]
        public long InstanceId { get; set; }

        [Export, GodotDictionaryField]
        public int Quantity { get; set; }

        [Export, GodotDictionaryField]
        public int CurrentCharges { get; set; }

        [Export, GodotDictionaryField]
        public float ReloadTimer { get; set; }

        [Export, GodotDictionaryField]
        public float CooldownRemainingTimer { get; set; }

        #endregion
    }
}
