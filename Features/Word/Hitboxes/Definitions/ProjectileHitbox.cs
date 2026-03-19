using Godot;

namespace Jogo25D.Hitboxes
{
    public partial class ProjectileHitbox : BaseHitbox
    {
        public float Speed { get; set; } = 600f;
        public Vector2 Direction { get; set; }
        public float Lifetime { get; set; } = 2f;
        public float Timer { get; set; }

        public override void _PhysicsProcess(double delta)
        {
            Position += Direction * Speed * (float)delta;
            Timer += (float)delta;

            if (Timer >= Lifetime)
            {
                QueueFree();
            }
        }
    }
}