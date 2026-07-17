using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Items
{
    public enum DamageType
    {
        Physical,
        Fire,
        Ice,
        Poison,
        Electric,
        True
    }

    public partial class DamageInfo: Resource
    {
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
    }
}