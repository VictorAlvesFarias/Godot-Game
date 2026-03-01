using Godot;

namespace Jogo25D.Hitboxes
{
    public partial class ProjectileHitbox : BaseHitbox
    {
        public float Speed { get; set; } = 600f;
        public Vector2 Direction { get; set; }
        public float Lifetime { get; set; } = 2f;
        private float _timer;

        public override void _PhysicsProcess(double delta)
        {
            Position += Direction * Speed * (float)delta;

            _timer += (float)delta;

            if (_timer >= Lifetime)
            {
                QueueFree();
            }
        }
    }
}
