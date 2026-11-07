using Godot;
using Jogo25D.Items;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Properties
{
    public partial class DamageResistenceMultiplierPropertyData : BasePropertyData
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public DamageType DamageType { get; set; }

        [Export, GodotDictionaryField]
        public float Multiplier { get; set; } = 1f;

        #endregion
    }
}