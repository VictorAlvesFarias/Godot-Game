using Jogo25D.Characters;

namespace Jogo25D.Effects
{
    public abstract class EffectDefinition
    {
        public float Duration { get; set; }
        public float Elapsed { get; private set; }
        public bool Expired { get; private set; }
        public bool RemoveInOnUnequip { get; private set; }
        public bool Infinite { get; set; }
        public bool ApplyToOwner { get; set; }
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

        protected virtual void Apply(Player player, float delta)
        {
        }

        protected virtual void OnFinished(Player player, float delta)
        {
        }
    }
}
