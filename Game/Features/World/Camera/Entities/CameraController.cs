using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;

namespace Jogo25D.Systems
{
	public partial class CameraController : Camera2D
	{
		#region Dinamic properties

		public Node2D PlayerRef { get; set; }
		public bool FreeCameraEnabled { get; set; }
		public float FreeCameraSpeed { get; set; } = 800f;
		public float FreeCameraZoomStep { get; set; } = 0.1f;

		#endregion

		#region Node references

		public WorldManager WorldManager { get; set; }

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			Enabled = true;

			AddToGroup("cameras");

			WorldManager = GetTree().Root.GetNodeOrNull<WorldManager>(StaticNodePathsConstants.WorldManager);

            PlayerRef = WorldManager?.GetLocalPlayer();
        }

		public override void _Input(InputEvent @event)
		{
			if (!FreeCameraEnabled)
			{
				return;
			}

			if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
			{
				if (mouseButton.ButtonIndex == MouseButton.WheelUp)
				{
					ApplyZoom(1f + FreeCameraZoomStep);
					GetViewport().SetInputAsHandled();
				}
				else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
				{
					ApplyZoom(1f / (1f + FreeCameraZoomStep));
					GetViewport().SetInputAsHandled();
				}
			}
		}

        public override void _PhysicsProcess(double delta)
		{
			if (FreeCameraEnabled)
			{
				var direction = new Vector2(
					Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left"),
					Input.GetActionStrength("move_down") - Input.GetActionStrength("move_up"));

				if (direction != Vector2.Zero)
				{
					// Quanto mais afastado (zoom out), mais rapido move a camera - senao fica
					// lento demais pra atravessar o mapa vendo uma area grande.
					GlobalPosition += direction.Normalized() * FreeCameraSpeed * (1f / Zoom.X) * (float)delta;
				}

				return;
			}

			if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			{
                PlayerRef = WorldManager?.GetLocalPlayer();
            }

            if (PlayerRef != null && IsInstanceValid(PlayerRef))
			{
				GlobalPosition = PlayerRef.GlobalPosition;
			}
		}

		private void ApplyZoom(float factor)
		{
			var newZoom = Zoom * factor;

			// Sem limite minimo (zoom out infinito) - so evita o zoom chegar em zero/negativo,
			// o que quebraria a renderizacao.
			newZoom.X = Mathf.Max(newZoom.X, 0.0001f);
			newZoom.Y = Mathf.Max(newZoom.Y, 0.0001f);

			Zoom = newZoom;
		}

		#endregion
	}
}
