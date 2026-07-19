using Godot;
using System.Collections.Generic;
using Jogo25D.Items;
using Jogo25D.Effects;
using Jogo25D.Characters;
using System;

namespace Jogo25D.Hitboxes
{
    public partial class BaseHitbox : Area2D
    {
        #region Properties

        public Godot.Collections.Array<DamageInfo> Damages { get; set; } = new();
        public Godot.Collections.Array<EffectDefinitionData> Effects { get; set; } = new();
        public float KnockbackForce { get; set; } = 0f;
        public Player Owner { get; set; }
        public AnimatedSprite2D Sprite { get; set; }
        public int Perfuracao { get; set; } = 0;
        public bool DestroyInAllBodies { get; set; } = true;
        public bool Destroy { get; set; } = true;
        public bool StopDamageOnMaxPerfuracao { get; set; } = false;
        public int HitCount { get; set; } = 0;

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            base._Ready();

            Sprite = GetNodeOrNull<AnimatedSprite2D>("Sprite");

            if (Sprite is not null)
            {
                Sprite.Play("idle");
            }
        }

        #endregion

        #region Core - Virtuals

        public virtual void Initialize(Godot.Collections.Array<DamageInfo> damages, Godot.Collections.Array<EffectDefinitionData> effects, Player owner, float knockbackForce = 0f)
        {
            Damages = damages ?? new();
            Effects = new Godot.Collections.Array<EffectDefinitionData>(effects ?? new());
            Owner = owner;
            KnockbackForce = knockbackForce;

            BodyEntered += OnBodyEntered;
        }

        public virtual void OnBodyEntered(Node body)
        {
            if (body == Owner) return;

            if (body is Player target)
            {
                if (Destroy && CanApplyImpact())
                {
                    ApplyImpact(target);
                }

                if (Destroy || DestroyInAllBodies)
                {
                    HandleDestruction(true);
                }
            }
            else if (DestroyInAllBodies)
            {
                HandleDestruction(false);
            }
        }

        #endregion

        #region Core - Utils

        protected bool CanApplyImpact()
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
            {
                return true;
            }

            return Multiplayer.IsServer();
        }

        protected void ApplyImpact(Player target)
        {
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

        protected void HandleDestruction(bool hitTarget)
        {
            if (hitTarget)
            {
                if (HitCount >= Perfuracao)
                {
                    if (StopDamageOnMaxPerfuracao)
                    {
                        Destroy = false;
                    }
                    else
                    {
                        QueueFree();
                    }
                }
                else
                {
                    HitCount++;
                }
            }
            else
            {
                QueueFree();
            }
        }

        #endregion
    }
}