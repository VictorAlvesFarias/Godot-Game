using Godot;

namespace Jogo25D.Systems
{
	public partial class BackgroundController : Node2D
	{
		#region Dinamic properties

		[Export] public float SwapDistance = 1000f;
		[Export] public float FadeRange = 500f;

		#endregion

		#region Node children references

		public Parallax2D Horizon { get; set; }
		public Parallax2D HorizonFlipped { get; set; }
		public Parallax2D HorizonAlt { get; set; }
		public Parallax2D HorizonAltFlipped { get; set; }

		public Camera2D Camera { get; set; }

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			Horizon = GetNode<Parallax2D>("Horizon");
			HorizonFlipped = GetNode<Parallax2D>("HorizonFlipped");
			HorizonAlt = GetNode<Parallax2D>("HorizonAlt");
			HorizonAltFlipped = GetNode<Parallax2D>("HorizonAltFlipped");

			Camera = GetParent().GetNodeOrNull<Camera2D>("Camera2D");
		}

		public override void _Process(double delta)
		{
			if (Camera == null)
			{
				return;
			}

			var dist = Mathf.Abs(Camera.GlobalPosition.X);
			var fadeStart = SwapDistance - FadeRange * 0.5f;
			var fadeEnd = SwapDistance + FadeRange * 0.5f;
			var t = Mathf.Clamp((dist - fadeStart) / FadeRange, 0f, 1f);
			var defaultAlpha = 1f - t;
			var altAlpha = t;

			SetGroupAlpha(Horizon, defaultAlpha);
			SetGroupAlpha(HorizonFlipped, defaultAlpha);
			SetGroupAlpha(HorizonAlt, altAlpha);
			SetGroupAlpha(HorizonAltFlipped, altAlpha);
		}

		#endregion

		#region Utils

		private static void SetGroupAlpha(Parallax2D group, float alpha)
		{
			group.Visible = alpha > 0f;
			group.Modulate = new Color(1f, 1f, 1f, alpha);
		}

		#endregion
	}
}
