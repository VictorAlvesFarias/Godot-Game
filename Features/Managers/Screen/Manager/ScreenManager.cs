using Godot;

namespace Jogo25D.UI
{
	public partial class ScreenManager : Node
	{
		private static readonly Vector2I DESIGN_RESOLUTION = new Vector2I(1920, 1080);
		private static bool AUTOSCALE { get; set; } = true;

		private static float CURRENT_SCALE = 0.5f;

		public override void _Ready()
		{
			var root = GetTree().Root;

			root.ContentScaleSize = DESIGN_RESOLUTION;
			root.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
			root.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;

			ApplyScale();
		}

		public override void _Input(InputEvent @event)
		{
			if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
			{
				if (keyEvent.Keycode == Key.F11)
				{
					ToggleFullscreen();
					GetViewport().SetInputAsHandled();
				}
				else if (keyEvent.Keycode == Key.F12)
				{
					SetFullscreen();
					GetViewport().SetInputAsHandled();
				}
			}
		}

		private void SetWindowed()
		{
			ApplyScale();
		}

		private void SetFullscreen()
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
		}

		private void ToggleFullscreen()
		{
			var currentMode = DisplayServer.WindowGetMode();

			if (currentMode == DisplayServer.WindowMode.Fullscreen || currentMode == DisplayServer.WindowMode.ExclusiveFullscreen)
			{
				SetWindowed();
			}
			else
			{
				SetFullscreen();
			}
		}

		private void ApplyScale()
		{
			if (!AUTOSCALE)
			{
				ApplyWindowSize(CURRENT_SCALE);
			}
			else
			{
				ApplyWindowSize(ResolveScaleFromScreen());
			}
		}

		private void ApplyWindowSize(float scale)
		{
			CURRENT_SCALE = scale;

			var screenSize = DisplayServer.ScreenGetSize();
			var desired = new Vector2I(
				(int)(DESIGN_RESOLUTION.X * scale),
				(int)(DESIGN_RESOLUTION.Y * scale)
			);
			var windowSize = new Vector2I(
				Mathf.Min(desired.X, screenSize.X),
				Mathf.Min(desired.Y, screenSize.Y)
			);

			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
			DisplayServer.WindowSetSize(windowSize);
			DisplayServer.WindowSetPosition(DisplayServer.ScreenGetPosition() + (screenSize - windowSize) / 2);
		}

		private float ResolveScaleFromScreen()
		{
			int screenWidth = DisplayServer.ScreenGetSize().X;

			return screenWidth switch
			{
				>= 3840 => 1.00f,  // 4K
				>= 2560 => 0.75f,  // 1440p / QHD
				>= 1920 => 0.50f,  // 1080p
				>= 1280 => 0.50f,  // 720p
			};
		}
	}
}
