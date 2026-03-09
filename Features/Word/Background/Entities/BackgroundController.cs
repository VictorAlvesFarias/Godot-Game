using Godot;

namespace Jogo25D.Systems
{
	public partial class BackgroundController : Node2D
	{
		[Export] public float SwapDistance = 1000f;
		[Export] public float FadeRange = 500f;

		private Parallax2D _horizon;
		private Parallax2D _horizonFlipped;
		private Parallax2D _horizonAlt;
		private Parallax2D _horizonAltFlipped;

		private Camera2D _camera;

		public override void _Ready()
		{
			_horizon = GetNode<Parallax2D>("Horizon");
			_horizonFlipped = GetNode<Parallax2D>("HorizonFlipped");
			_horizonAlt = GetNode<Parallax2D>("HorizonAlt");
			_horizonAltFlipped = GetNode<Parallax2D>("HorizonAltFlipped");

			_camera = GetParent().GetNodeOrNull<Camera2D>("Camera2D");
		}

		public override void _Process(double delta)
		{
			if (_camera == null)
			{
				return;
			}

			float dist = Mathf.Abs(_camera.GlobalPosition.X);
			float fadeStart = SwapDistance - FadeRange * 0.5f;
			float fadeEnd = SwapDistance + FadeRange * 0.5f;

			float t = Mathf.Clamp((dist - fadeStart) / FadeRange, 0f, 1f);

			float defaultAlpha = 1f - t;
			float altAlpha = t;

			SetGroupAlpha(_horizon, defaultAlpha);
			SetGroupAlpha(_horizonFlipped, defaultAlpha);
			SetGroupAlpha(_horizonAlt, altAlpha);
			SetGroupAlpha(_horizonAltFlipped, altAlpha);
		}

		private static void SetGroupAlpha(Parallax2D group, float alpha)
		{
			group.Visible = alpha > 0f;
			group.Modulate = new Color(1f, 1f, 1f, alpha);
		}
	}
}
