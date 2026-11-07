using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Properties
{
    public partial class AttackPropertyData : BasePropertyData
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public float AttackRange { get; set; } = 80f;

        [Export, GodotDictionaryField]
        public float AttackArea { get; set; } = 25f;

        [Export, GodotDictionaryField]
        public float KnockbackForce { get; set; } = 0f;

        [Export, GodotDictionaryField]
        public float ProjectileSpeed { get; set; } = 500f;

        #endregion
    }
}