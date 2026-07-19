using Godot;
using Jogo25D.Characters;
using Jogo25D.Effects;
using Jogo25D.Items;
using Jogo25D.Properties;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Actions
{
    public partial class ActionDefinitionData : Resource
    {
        #region Properties

        [Export, GodotDictionaryField]
        public string Id { get; set; }

        [Export, GodotDictionaryField]
        public int CurrentCharges { get; set; }

        [Export, GodotDictionaryField]
        public bool CanUse { get; set; } = true;

        [Export, GodotDictionaryField]
        public bool InCooldown { get; set; } = false;

        [Export, GodotDictionaryField]
        public bool IsActive { get; set; } = false;

        [Export, GodotDictionaryField]
        public float CooldownTimer { get; set; } = 0f;

        [Export, GodotDictionaryField]
        public float DurationTimer { get; set; } = 0f;

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<BasePropertyData> Properties { get; set; } = new();

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<EffectDefinitionData> Effects { get; set; } = new();

        [Export, GodotDictionaryField]
        public Vector2 DashDirection { get; set; } = Vector2.Zero;

        #endregion
    }
}