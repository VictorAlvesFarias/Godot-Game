using Godot;
using Jogo25D.Items;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Properties
{
    public partial class DamagePropertyData : BasePropertyData
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public DamageType DamageType { get; set; }

        [Export, GodotDictionaryField]
        public int DamageAmount { get; set; }

        [Export, GodotDictionaryField]
        public float DamageMultiplier { get; set; } = 1f;

        #endregion
    }
}