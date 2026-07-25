using Godot;
using Jogo25D.Items;

namespace Jogo25D.Characters
{
    public partial class NPC : Player
    {
        #region Godot implementation

        public override void _Ready()
        {
            PeerId = -999;

            Gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();

            AddToGroup("players");

            Visuals = GetNodeOrNull<Node2D>("Visuals");
            Sprite = GetNodeOrNull<AnimatedSprite2D>("Visuals/Sprite");
            Labels = GetNodeOrNull<Node2D>("Labels");
            NameLabel = GetNodeOrNull<Label>("Labels/NameLabel");
            HealthLabel = GetNodeOrNull<Label>("Labels/HealthLabel");
            Sprite?.Play("idle");

            if (Sprite != null)
            {
                Sprite.AnimationFinished += OnAnimationFinished;
            }

            DisplayName = "NPC";
        }

        public override void _PhysicsProcess(double delta)
        {
            var dt = (float)delta;

            TickKnockback(dt);

            if (!Data.CanUpdateMovement)
            {
                MoveAndSlide();

                UpdateAnimation();

                return;
            }

            var v = Velocity;

            if (!IsOnFloor())
            {
                v.Y += Gravity * dt;
            }

            Velocity = v;

            MoveAndSlide();

            UpdateAnimation();
        }

        #endregion

        #region Core - Damage system

        public override void ReceiveDamage(DamageInfo damage)
        {
            if (!IsAuthoritative())
            {
                return;
            }

            var finalDamage = Mathf.Max(0, damage.Amount);
            var newHealth = Mathf.Max(0, Data.CurrentHealth - finalDamage);

            SetHealthRequest(newHealth);
        }

        // Renasce sozinho assim que a animacao "dead" termina de tocar -
        // dispara so no peer autoritativo (servidor) pra nao mandar o RPC de
        // cura em dobro, os outros peers so recebem via SetHealthReceive.
        private void OnAnimationFinished()
        {
            if (Sprite.Animation == "dead" && IsAuthoritative())
            {
                SetHealthRequest(GetMaxHealth());
            }
        }

        #endregion
    }
}
