using Godot;
using Jogo25D.Characters;
using Jogo25D.Effects;
using Jogo25D.Items;

namespace Jogo25D.Hitboxes
{
    public partial class BaseHitbox : Area2D
    {
        #region Dinamic properties

        public Godot.Collections.Array<DamageInfo> Damages { get; set; } = new();
        public Godot.Collections.Array<EffectDefinitionData> Effects { get; set; } = new();
        public float KnockbackForce { get; set; } = 0f;
        public float DirectionAngle { get; set; }
        public float Speed { get; set; } = 600f;
        public float Lifetime { get; set; } = 0.2f;
        public int Perfuracao { get; set; } = 0;
        public int HitCount { get; set; } = 0;
        public bool DestroyInAllBodies { get; set; } = true;
        public bool HitCountInAllBodies { get; set; } = true;
        public bool StopDamageOnMaxPerfuracao { get; set; } = false;
        public Player Owner { get; set; }
        public AnimatedSprite2D Sprite { get; set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            base._Ready();

            Rotation = DirectionAngle;

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
            if (body == Owner)
            {
                return;
            }

            if (body is Player target)
            {
                if (IsPvpBlocked(target))
                {
                    return;
                }

                if (CanApplyImpact())
                {
                    ApplyImpact(target);
                }

                RegisterHitOrDestroy();

                return;
            }

            // Alvo que nao e jogador (boneco de treino): recebe o dano no servidor e conta como acerto, como um jogador.
            if (HitTargetOf(body) is IHitTarget hitTarget)
            {
                if (CanApplyImpact())
                {
                    foreach (var damage in Damages)
                    {
                        hitTarget.ReceiveHit(damage, Owner);
                    }

                    // Para longe de quem bateu (o centro do golpe pode ficar alem de um alvo colado no jogador).
                    if (KnockbackForce > 0f && body is Node2D struck)
                    {
                        hitTarget.ApplyKnockback(struck.GlobalPosition - (Owner?.GlobalPosition ?? GlobalPosition), KnockbackForce);
                    }
                }

                RegisterHitOrDestroy();

                return;
            }

            if (!DestroyInAllBodies)
            {
                return;
            }

            if (!HitCountInAllBodies)
            {
                QueueFree();

                return;
            }

            RegisterHitOrDestroy();
        }

        private void RegisterHitOrDestroy()
        {
            if (StopDamageOnMaxPerfuracao)
            {
                if (HitCount >= Perfuracao)
                {
                    QueueFree();
                }
            }
            else
            {
                HitCount++;
            }
        }

        #endregion

        #region Core - Utils

        // O corpo atingido, ou o pai dele (o corpo do boneco e um filho do prop), quando e um alvo.
        protected static IHitTarget HitTargetOf(Node body)
        {
            return body as IHitTarget ?? body?.GetParent() as IHitTarget;
        }

        protected bool IsPvpBlocked(Player target)
        {
            if (Owner is null or NPC || target is NPC)
            {
                return false;
            }

            return !Owner.PvpEnabled || !target.PvpEnabled;
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

        #endregion
    }
}