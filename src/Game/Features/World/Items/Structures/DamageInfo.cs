using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Items
{
    public partial class DamageInfo : Resource
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public int Amount { get; set; }

        [Export, GodotDictionaryField]
        public DamageType Type { get; set; }

        [Export, GodotDictionaryField]
        public int SourcePeerId { get; set; }

        [Export, GodotDictionaryField]
        public float CritChance { get; set; }

        [Export, GodotDictionaryField]
        public float CritDamage { get; set; }

        #endregion
    }
}
