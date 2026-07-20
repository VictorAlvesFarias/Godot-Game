using Godot;
using Jogo25D.Characters;
using Jogo25D.Properties;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Effects
{
    public abstract class EffectDefinition
    {
        #region Properties

        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public Texture2D Icon { get; set; }

        public float Duration { get; init; } = 0f;
        public bool Infinite { get; init; } = false;
        public EffectTriggerType Type { get; init; } = EffectTriggerType.OnUse;
        public EffectApply ApplyTo { get; init; } = EffectApply.ToOwner;

        public Godot.Collections.Array<BasePropertyData> Properties { get; set; } = new();

        #endregion

        #region Core - Virtuals

        public virtual void Apply(Player player, EffectDefinitionData data, float delta) { }

        public virtual void OnFinished(Player player, EffectDefinitionData data, float delta) { }

        #endregion

        #region Core - Utils

        public void Tick(Player player, EffectDefinitionData data, float delta)
        {
            if (!data.Infinite)
            {
                if (data.Expired)
                {
                    return;
                }

                if (data.Duration > 0)
                {
                    data.Elapsed += delta;

                    if (data.Elapsed >= data.Duration)
                    {
                        data.Expired = true;

                        OnFinished(player, data, delta);

                        return;
                    }
                }
            }

            Apply(player, data, delta);
        }

        public float GetRemainingDuration(EffectDefinitionData data)
        {
            if (data == null || data.Infinite || data.Duration <= 0f)
            {
                return 0f;
            }

            return Mathf.Max(0f, data.Duration - data.Elapsed);
        }

        #endregion
    }
}
