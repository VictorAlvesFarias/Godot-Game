using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Properties
{
    public partial class ChargesPropertyData : BasePropertyData
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public int MaxCharges { get; set; } = 1;

        [Export, GodotDictionaryField]
        public string ChargeItemId { get; set; } = "";

        [Export, GodotDictionaryField]
        public bool InfiniteCharges { get; set; } = true;

        [Export, GodotDictionaryField]
        public float ReloadCooldown { get; set; } = 1.0f;

        #endregion
    }
}