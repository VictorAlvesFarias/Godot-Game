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

            Sprite = GetNodeOrNull<AnimatedSprite2D>("Sprite");
            Sprite?.Play("idle");

            DisplayName = "NPC";
        }

        public override void _PhysicsProcess(double delta)
        {
            var v = Velocity;

            if (!IsOnFloor())
            {
                v.Y += Gravity * (float)delta;
            }

            Velocity = v;

            MoveAndSlide();
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

            if (newHealth <= 0)
            {
                SetHealthRequest(Data.MaxHealth);
            }
        }

        #endregion
    }
}
