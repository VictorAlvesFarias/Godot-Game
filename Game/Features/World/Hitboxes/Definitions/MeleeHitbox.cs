using Godot;
using Jogo25D.Characters;

namespace Jogo25D.Hitboxes
{
	public partial class MeleeHitbox : BaseHitbox
	{
        #region Properties

        public float Lifetime { get; set; } = 0.2f;
		public Vector2 Offset { get; set; } = Vector2.Zero;
		public float Timer { get; set; }

		#endregion

        #region Godot implementation

        public override void _PhysicsProcess(double delta)
		{
			if (Owner == null)
			{
				QueueFree();
				return;
			}

			GlobalPosition = Owner.GlobalPosition + Offset;

			Timer += (float)delta;

			if (Timer >= Lifetime)
			{
				QueueFree();
			}
		}

		public override void _Ready()
		{
			base._Ready();

			if (Sprite is not null)
			{
				var threshold = 1.5f;

				GD.Print(Rotation >= -threshold && Rotation <= threshold, Rotation);

				var facingLeft = !(Rotation >= -threshold && Rotation <= threshold);

				Scale = new Vector2(1f, facingLeft ? -1f : 1f);
			}
		}

        #endregion
    }
}
