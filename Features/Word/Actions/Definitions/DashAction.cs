using Godot;
using Jogo25D.Characters;

namespace Jogo25D.Actions
{
    public class DashDefinition : ActionDefinition
    {
        public float DashSpeed { get; init; } = 800f;
        public float MovementInfluence { get; init; } = 0.4f;

        public override void OnCreate(Player player, ActionInstance instance)
        {
            var particles = new CpuParticles2D();
            player.AddChild(particles);
            instance.DashParticles = particles;
        }

        public override void OnStartAction(Player player, ActionInstance instance, float delta)
        {
            var input = new Vector2(player.Input.MoveX, player.Input.MoveY);
            Vector2 dir;

            if (input.LengthSquared() > 0.01f)
            {
                dir = input.Normalized();
            }
            else if (player.Velocity.LengthSquared() > 100f)
            {
                dir = player.Velocity.Normalized();
            }
            else
            {
                dir = Vector2.Up;
            }

            instance.DashDirection = dir;

            if (instance.DashParticles != null)
            {
                instance.DashParticles.Emitting = true;
            }

            if (player.Sprite != null)
            {
                player.Sprite.DefaultColor = new Color(0.5f, 1f, 1f);
            }

            player.Velocity = dir * DashSpeed;
            player.CanUpdateMovement = false;
        }

        public override void OnFinishedAction(Player player, ActionInstance instance, float delta)
        {
            player.CanUpdateMovement = true;
            instance.DashDirection = Vector2.Zero;

            if (instance.DashParticles != null)
            {
                instance.DashParticles.Emitting = false;
            }

            if (player.Sprite != null && player.DamageEffectTimer <= 0)
            {
                player.Sprite.DefaultColor = Colors.White;
            }
        }

        public override void OnUpdateWhileActive(Player player, ActionInstance instance, float delta)
        {
            var dir = instance.DashDirection.LengthSquared() > 0.01f ? instance.DashDirection : Vector2.Up;
            var input = new Vector2(player.Input.MoveX, player.Input.MoveY);

            if (input.LengthSquared() > 0.01f && MovementInfluence > 0f)
            {
                var blended = dir + input.Normalized() * MovementInfluence;
                if (blended.LengthSquared() > 0.01f)
                {
                    player.Velocity = blended.Normalized() * DashSpeed;
                    return;
                }
            }

            player.Velocity = dir * DashSpeed;
        }

        public override bool OnStartActionValidation(Player player, ActionInstance instance, float delta)
        {
            return player.Input.Dash && instance.CanUse;
        }

        public override void OnEnableAction(Player player, ActionInstance instance, float delta)
        {
        }
    }
}