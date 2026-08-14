using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Properties
{
    public partial class CritPropertyData : BasePropertyData
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public float CritChance { get; set; }

        [Export, GodotDictionaryField]
        public float CritDamage { get; set; }

        #endregion
    }
}