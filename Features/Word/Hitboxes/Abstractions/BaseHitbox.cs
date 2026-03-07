using Godot;
using System.Collections.Generic;
using Jogo25D.Items;
using Jogo25D.Effects;
using Jogo25D.Characters;

namespace Jogo25D.Hitboxes
{
    public partial class BaseHitbox : Area2D
    {
        public List<DamageInfo> Damages { get; set; } = new();
        public List<EffectDefinition> Effects { get; set; } = new();
        public new Player Owner { get; set; }

        public virtual void Initialize(List<DamageInfo> damages, List<EffectDefinition> effects, Player owner)
        {
            Damages = damages ?? new();
            Effects = new List<EffectDefinition>(effects ?? new());
            Owner = owner;

            BodyEntered += OnBodyEntered;
        }

        public virtual void OnBodyEntered(Node body)
        {
            if (body == Owner)
            {
                return;
            }

            if (body is Player target)
            {
                foreach (var damage in Damages)
                {
                    target.ReceiveDamage(damage);
                }

                foreach (var effect in Effects)
                {
                    target.AddEffect(effect);
                }

                QueueFree();
            }
        }
    }
}