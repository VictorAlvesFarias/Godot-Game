using Godot;
using Jogo25D.Characters;

namespace Jogo25D.Scripts.Actions
{
    public class DashAction : PlayerAction
    {
        [Export] public float DashSpeed { get; set; } = 800.0f;
        [Export] public Vector2 DashDirection { get; private set; } = Vector2.Zero;

        private CpuParticles2D dashParticles;

        public DashAction(Player player) : base(player)
        {
            dashParticles = player.GetNodeOrNull<CpuParticles2D>("DashParticles");

            DurationTime = 0.2f;
            CooldownTime = 0.5f;
        }

        public override void Update(float delta)
        {
            var dashPressed = NodePlayer.Controls.InputDash;

            if (dashPressed && CanUse && !Cooldown)
            {
                Vector2 direction = new Vector2(NodePlayer.Controls.InputX, NodePlayer.Controls.InputY);

                if (direction.Length() == 0)
                {
                    DashDirection = Vector2.Up;
                }
                else
                {
                    DashDirection = direction.Normalized();
                }

                IsActive = true;
                CanUse = false;
                Cooldown = true;
                DurationTimer = 0f;
                CooldownTimer = 0f;

                if (dashParticles != null)
                {
                    dashParticles.Emitting = true;
                }

                if (NodePlayer.Sprite != null)
                {
                    NodePlayer.Sprite.DefaultColor = new Color(0.5f, 1f, 1f);
                }

                NodePlayer.Velocity = DashDirection * DashSpeed;
                NodePlayer.CanUpdateMovement = false;
            }

            if (IsActive)
            {
                DurationTimer += delta;

                if (DurationTimer >= DurationTime)
                {
                    IsActive = false;
                    NodePlayer.CanUpdateMovement = true;
                    DashDirection = Vector2.Zero;

                    if (dashParticles != null)
                    {
                        dashParticles.Emitting = false;
                    }

                    if (NodePlayer.Sprite != null && NodePlayer.DamageEffectTimer <= 0)
                    {
                        NodePlayer.Sprite.DefaultColor = Colors.White;
                    }
                }
            }

            if (Cooldown)
            {
                CooldownTimer += delta;

                if (CooldownTimer >= CooldownTime)
                {
                    Cooldown = false;
                    CanUse = true;
                    CooldownTimer = 0f;
                }
            }
        }
    }
}
