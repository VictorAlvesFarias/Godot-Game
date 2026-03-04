using Godot;
using Jogo25D.Characters;

namespace Jogo25D.Hitboxes
{
    public partial class MeleeHitbox : BaseHitbox
    {
        public float Lifetime { get; set; } = 0.2f;
        public Vector2 Offset { get; set; } = Vector2.Zero;
        private float _timer;

        public override void _PhysicsProcess(double delta)
        {
            if (Owner == null)
            {
                QueueFree();
                return;
            }

            GlobalPosition = Owner.GlobalPosition + Offset;

            _timer += (float)delta;

            if (_timer >= Lifetime)
            {
                QueueFree();
            }
        }
    }
}
