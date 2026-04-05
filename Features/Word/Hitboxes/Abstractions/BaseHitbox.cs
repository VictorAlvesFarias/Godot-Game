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
        public List<DamageInfo> Damages { get; set; } = new();
        public List<EffectDefinition> Effects { get; set; } = new();
        public Player Owner { get; set; }
        public AnimatedSprite2D Sprite { get; set; }
        public int Perfuracao { get; set; } = 0;
        public bool DestroyInAllBodies { get; set; } = true;
        public bool Destroy { get; set; } = true;
        public bool StopDamageOnMaxPerfuracao { get; set; } = false;
        public int HitCount { get; set; } = 0;

        public override void _Ready()
        {
            base._Ready();

            Sprite = GetNodeOrNull<AnimatedSprite2D>("Sprite");

            if (Sprite is not null)
            {
                Sprite.Play("idle");
            }
        }

        public virtual void Initialize(List<DamageInfo> damages, List<EffectDefinition> effects, Player owner)
        {
            Damages = damages ?? new();
            Effects = new List<EffectDefinition>(effects ?? new());
            Owner = owner;

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

            foreach (var effect in Effects)
            {
                target.AddEffect(effect);
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
    }
}