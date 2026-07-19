using Godot;
using System.Collections.Generic;
using Jogo25D.Characters;
using Jogo25D.Effects;
using Jogo25D.Items;

namespace Jogo25D.Hitboxes
{
    public partial class GroundHitbox : BaseHitbox
    {
        #region Properties

        public float Lifetime { get; set; } = 1.5f;
        public float _timer;

        public readonly HashSet<Player> _alreadyHit = new();

        #endregion

        #region Godot implementation

        public override void _PhysicsProcess(double delta)
        {
            _timer += (float)delta;

            if (_timer >= Lifetime)
            {
                QueueFree();
            }
        }

        #endregion

        #region Core - Virtuals

        public override void OnBodyEntered(Node body)
        {
            if (body == Owner)
            {
                return;
            }

            if (body is Player target && !_alreadyHit.Contains(target))
            {
                _alreadyHit.Add(target);

                foreach (var damage in Damages)
                {
                    target.ReceiveDamage(damage);
                }

                if (KnockbackForce > 0f)
                {
                    target.ApplyKnockback(target.GlobalPosition - GlobalPosition, KnockbackForce);
                }

                foreach (var effect in Effects)
                {
                    if (effect.ApplyTo == EffectApply.ToTarget || effect.ApplyTo == EffectApply.ToAll)
                    {
                        target.GiveEffect(effect.Id);
                    }

                    if (effect.ApplyTo == EffectApply.ToOwner || effect.ApplyTo == EffectApply.ToAll)
                    {
                        Owner?.GiveEffect(effect.Id);
                    }
                }
            }
        }

        #endregion
    }
}