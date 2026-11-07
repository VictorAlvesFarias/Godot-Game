using Godot;
using Jogo25D.Items;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Properties
{
    public partial class DamageResistencePropertyData : BasePropertyData
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public DamageType DamageType { get; set; }

        [Export, GodotDictionaryField]
        public float ResistanceFactor { get; set; }

        #endregion
    }
}