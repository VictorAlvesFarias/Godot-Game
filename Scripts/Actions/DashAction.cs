using Godot;
using Jogo25D.Characters;

namespace Jogo25D.Scripts.Actions
{
    public class DashAction : PlayerAction
    {
        [Export] public float DashSpeed { get; set; } = 800.0f;
        [Export] public Vector2 DashDirection { get; private set; } = Vector2.Zero;
        /// <summary>Quanto o input de movimento influencia a direção do dash (0 = nenhum, 1 = total).</summary>
        [Export] public float MovementInfluence { get; set; } = 0.4f;

        private CpuParticles2D dashParticles;

        public DashAction(Player player) : base(player)
        {
            dashParticles = player.GetNodeOrNull<CpuParticles2D>("DashParticles");
            Duration = 0.2f;
            Cooldown = 1f;
            MaxCharges = 2;
            CurrentCharges = MaxCharges;
            ActionName = "Dash";
        }

        public override void OnStartAction(float delta)
        {
            Vector2 inputDirection = new Vector2(NodePlayer.Controls.InputX, NodePlayer.Controls.InputY);

            if (inputDirection.LengthSquared() > 0.01f)
            {
                DashDirection = inputDirection.Normalized();
            }
            else if (NodePlayer.Velocity.LengthSquared() > 100f)
            {
                DashDirection = NodePlayer.Velocity.Normalized();
            }
            else
            {
                DashDirection = Vector2.Up;
            }

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

        public override void OnFinishedAction(float delta)
        {
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

        public override void OnUpdateWhileActive(float delta)
        {
            var inputDirection = new Vector2(NodePlayer.Controls.InputX, NodePlayer.Controls.InputY);
            if (inputDirection.LengthSquared() > 0.01f && MovementInfluence > 0f)
            {
                var blended = DashDirection + inputDirection.Normalized() * MovementInfluence;
                if (blended.LengthSquared() > 0.01f)
                {
                    NodePlayer.Velocity = blended.Normalized() * DashSpeed;
                    return;
                }
            }
            NodePlayer.Velocity = DashDirection * DashSpeed;
        }

        public override bool OnStartActionValidation(float delta)
        {
            return NodePlayer.Controls.InputDash && CanUse;
        }
    }
}
