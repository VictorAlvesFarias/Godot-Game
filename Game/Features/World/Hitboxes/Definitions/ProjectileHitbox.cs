using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Hitboxes
{
    public partial class ProjectileHitbox : BaseHitbox
    {
        #region Properties

        public float Timer { get; set; }
        public Vector2 Direction { get; set; }

        #endregion

        #region Godot implementation

        public override void _PhysicsProcess(double delta)
        {
            Position += Direction * Speed * (float)delta;
            Timer += (float)delta;

            if (Timer >= Lifetime)
            {
                QueueFree();
            }
        }

        #endregion
    }
}