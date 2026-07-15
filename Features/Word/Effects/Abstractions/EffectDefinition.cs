using Godot;
using Jogo25D.Characters;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Effects
{
    public abstract partial class EffectDefinition : Resource
    {
        [Export, GodotDictionaryField]
        public float Duration { get; set; }

        [Export, GodotDictionaryField]
        public float Elapsed { get; set; }

        [Export, GodotDictionaryField]
        public bool Expired { get; set; }

        [Export, GodotDictionaryField]
        public bool RemoveInOnUnequip { get; set; }

        [Export, GodotDictionaryField]
        public bool Infinite { get; set; }

        [Export, GodotDictionaryField]
        public bool ApplyToOwner { get; set; }

        [Export, GodotDictionaryField]
        public bool ApplyToTarget { get; set; }

        public void Tick(Player player, float delta)
        {
            if (!Infinite)
            {
                if (Expired)
                {
                    return;
                }

                if (Duration > 0)
                {
                    Elapsed += delta;

                    if (Elapsed >= Duration)
                    {
                        Expired = true;
                        OnFinished(player, delta);
                        return;
                    }
                }
            }

            Apply(player, delta);
        }

        public EffectDefinition Clone()
        {
            return (EffectDefinition)MemberwiseClone();
        }

        public virtual void Apply(Player player, float delta)
        {
        }

        public virtual void OnFinished(Player player, float delta)
        {
        }
    }
}