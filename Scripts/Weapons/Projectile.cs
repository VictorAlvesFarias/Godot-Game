using Godot;
using Jogo25D.Characters;
using System;

namespace Jogo25D.Weapons
{
    /// <summary>
    /// Projétil disparado por armas ranged
    /// </summary>
    public partial class Projectile : Area2D
    {
        [Export] public float Speed { get; set; } = 0.0f;
        [Export] public Vector2 Direction { get; set; } = Vector2.Zero;
        [Export] public int Damage { get; set; } = 1;
        [Export] public float Lifetime { get; set; } = 0.3f;
        [Export] public Node2D Shooter { get; set; }

        private float timer = 0.0f;
        private bool isDestroyed = false;

        public override void _Ready()
        {
            BodyEntered += OnBodyEntered;
        }

        public override void _PhysicsProcess(double delta)
        {
            Position += Direction * Speed * (float)delta;

            timer += (float)delta;

            if (timer >= Lifetime && !isDestroyed)
            {
                Destroy();
            }
        }

        private void OnBodyEntered(Node2D body)
        {
            if (isDestroyed)
            {
                return;
            }

            if (body == Shooter)
            {
                return;
            }

            if (body is Player player)
            {
                player.TakeDamage(Damage);
            }

            isDestroyed = true;

            Destroy();
        }

        private void Destroy()
        {
            QueueFree();
        }
    }
}
