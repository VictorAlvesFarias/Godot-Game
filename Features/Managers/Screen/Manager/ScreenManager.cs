using Godot;

namespace Jogo25D.UI
{
	public partial class ScreenManager : Node
	{
		public static Vector2I DesignResolution { get; set; } = new Vector2I(1920, 1080);
		public static bool AutoScale { get; set; } = true;

		public static float CurrentScale { get; set; } = 0.5f;

		public override void _Ready()
		{
			var root = GetTree().Root;

			root.ContentScaleSize = DesignResolution;
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

		public void SetWindowed()
		{
			ApplyScale();
		}

		public void SetFullscreen()
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
		}

		public void ToggleFullscreen()
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

		public void ApplyScale()
		{
			if (!AutoScale)
			{
				ApplyWindowSize(CurrentScale);
			}
			else
			{
				ApplyWindowSize(ResolveScaleFromScreen());
			}
		}

		public void ApplyWindowSize(float scale)
		{
			CurrentScale = scale;

			var screenSize = DisplayServer.ScreenGetSize();
			var desired = new Vector2I(
				(int)(DesignResolution.X * scale),
				(int)(DesignResolution.Y * scale)
			);
			var windowSize = new Vector2I(
				Mathf.Min(desired.X, screenSize.X),
				Mathf.Min(desired.Y, screenSize.Y)
			);

			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
			DisplayServer.WindowSetSize(windowSize);
			DisplayServer.WindowSetPosition(DisplayServer.ScreenGetPosition() + (screenSize - windowSize) / 2);
		}

		public float ResolveScaleFromScreen()
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