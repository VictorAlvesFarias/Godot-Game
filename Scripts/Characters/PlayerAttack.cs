using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jogo25D.Characters
{
    public partial class PlayerAttack : Area2D
    {
        [Export] public float Speed = 0.0f;
        [Export] public Vector2 Direction = Vector2.Zero;
        [Export] public int Damage = 1;
        [Export] public float Lifetime = 0.3f;
        [Export] public Node2D Shooter;

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
