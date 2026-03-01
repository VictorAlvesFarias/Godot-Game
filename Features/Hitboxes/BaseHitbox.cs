using Godot;
using System.Collections.Generic;
using Jogo25D.Items;
using Jogo25D.Effects;
using Jogo25D.Characters;

namespace Jogo25D.Hitboxes
{
    public partial class BaseHitbox : Area2D
    {
        public DamageInfo Damage { get; set; }
        public List<EffectDefinition> Effects { get; set; } = new();
        public new Player Owner { get; set; }

        public virtual void Initialize(DamageInfo damage, List<EffectDefinition> effects, Player owner)
        {
            Damage  = damage;
            Effects = new List<EffectDefinition>(effects ?? new());
            Owner   = owner;

            BodyEntered += OnBodyEntered;
        }

        protected virtual void OnBodyEntered(Node body)
        {
            if (body == Owner)
            {
                return;
            }

            if (body is Player target)
            {
                target.ReceiveDamage(Damage);

                foreach (var effect in Effects)
                {
                    target.AddEffect(effect);
                }

                QueueFree();
            }
        }
    }
}
