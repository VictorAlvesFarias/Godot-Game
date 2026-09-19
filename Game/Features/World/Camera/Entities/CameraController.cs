using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;

namespace Jogo25D.Systems
{
	public partial class CameraController : Camera2D
	{
		#region Dinamic properties

		public Node2D PlayerRef { get; set; }
		public bool FreeCameraEnabled { get; set; }
		public float FreeCameraSpeed { get; set; } = 800f;
		public float FreeCameraZoomStep { get; set; } = 0.1f;

		// Suavizacao: a camera persegue o player em vez de ficar colada nele. O corpo do player
		// nao tem aceleracao (velocidade vai de 0 a 300 em um frame), pula a -750 e sobe degrau
		// de 64 px em ~0,14 s - cada uma dessas quebras chegava inteira na tela. Perseguindo a
		// 12/s o solavanco por frame cai de 12,1 px para 2,4 px e o atraso volta a zero assim
		// que o player para. Medido em Testing/CameraSmoothProbe.
		public float SmoothingSpeed { get; set; } = 12f;

		// Acima disso o alvo nao andou: trocou de lugar (troca de dimensao, load, respawn). Ai a
		// camera corta em vez de varrer o mundo inteiro ate alcancar.
		public float SmoothingCutDistance { get; set; } = 512f;

		#endregion

		#region Node references


		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			Enabled = true;

			PositionSmoothingEnabled = true;
			PositionSmoothingSpeed = SmoothingSpeed;

			AddToGroup("cameras");


            Game.WhenReady(() => PlayerRef = Game.Managers.WorldManager.Node.GetLocalPlayer());
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

					GlobalPosition += direction.Normalized() * FreeCameraSpeed * (1f / Zoom.X) * (float)delta;
				}

				return;
			}

			if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			{
                PlayerRef = Game.Managers.WorldManager.Node?.GetLocalPlayer();
            }

            if (PlayerRef != null && IsInstanceValid(PlayerRef))
			{
				var target = PlayerRef.GlobalPosition;
				var teleported = GlobalPosition.DistanceTo(target) > SmoothingCutDistance;

				GlobalPosition = target;

				if (teleported)
				{
					ResetSmoothing();
				}
			}
		}

		private void ApplyZoom(float factor)
		{
			var newZoom = Zoom * factor;

			newZoom.X = Mathf.Max(newZoom.X, 0.0001f);
			newZoom.Y = Mathf.Max(newZoom.Y, 0.0001f);

			Zoom = newZoom;
		}

		#endregion
	}
}
