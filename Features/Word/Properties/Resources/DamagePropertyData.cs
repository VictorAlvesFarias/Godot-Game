using Godot;
using Jogo25D.Items;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Properties
{
    public partial class DamagePropertyData : BasePropertyData
    {
        [Export, GodotDictionaryField]
        public DamageType DamageType { get; set; }

        [Export, GodotDictionaryField]
        public int DamageAmount { get; set; }
    }
}