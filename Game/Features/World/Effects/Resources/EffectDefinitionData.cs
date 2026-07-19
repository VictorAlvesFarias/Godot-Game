using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Effects
{
    public partial class EffectDefinitionData : Resource
    {
        public EffectDefinitionData() { }

        public EffectDefinitionData(string id)
        {
            Id = id;
        }

        #region Properties

        [Export, GodotDictionaryField]
        public string Id { get; set; }

        [Export, GodotDictionaryField]
        public long InstanceId { get; set; }

        #endregion

        #region Timers

        [Export, GodotDictionaryField]
        public float Duration { get; set; }

        [Export, GodotDictionaryField]
        public float Elapsed { get; set; }

        [Export, GodotDictionaryField]
        public float Timer { get; set; }

        #endregion

        #region Flags

        [Export, GodotDictionaryField]
        public bool Expired { get; set; }

        [Export, GodotDictionaryField]
        public bool Infinite { get; set; }

        [Export, GodotDictionaryField]
        public EffectTriggerType Type { get; set; }

        [Export, GodotDictionaryField]
        public EffectApply ApplyTo { get; set; }

        #endregion
    }
}
