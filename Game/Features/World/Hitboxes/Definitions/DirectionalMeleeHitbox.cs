using Godot;

namespace Jogo25D.Hitboxes
{
    public partial class DirectionalMeleeHitbox : MeleeHitbox
    {
        private Node2D _origin;

        public override void _Ready()
        {
            base._Ready();

            _origin = GetNodeOrNull<Node2D>("Origin");

            SnapToCardinalDirection();
            AlignToOwnerOrigin();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (Owner == null)
            {
                QueueFree();

                return;
            }

            AlignToOwnerOrigin();

            Timer += (float)delta;

            if (Timer >= Lifetime)
            {
                QueueFree();
            }
        }

        private void SnapToCardinalDirection()
        {
            var dir = Vector2.Right.Rotated(DirectionAngle);

            if (Mathf.Abs(dir.X) >= Mathf.Abs(dir.Y))
            {
                Rotation = 0f;
                Scale = new Vector2(dir.X < 0f ? -1f : 1f, 1f);
            }
            else
            {
                Rotation = Mathf.Pi / 2f;
                Scale = new Vector2(dir.Y < 0f ? -1f : 1f, 1f);
            }
        }

        private void AlignToOwnerOrigin()
        {
            var ownerOrigin = Owner.GetNodeOrNull<Node2D>("Visuals/AttackOrigin");
            var targetGlobal = ownerOrigin != null ? ownerOrigin.GlobalPosition : Owner.GlobalPosition;

            if (_origin == null)
            {
                GlobalPosition = targetGlobal;

                return;
            }

            var scaledLocal = new Vector2(_origin.Position.X * Scale.X, _origin.Position.Y * Scale.Y);
            var rotatedOffset = scaledLocal.Rotated(Rotation);

            GlobalPosition = targetGlobal - rotatedOffset;
        }
    }
}
