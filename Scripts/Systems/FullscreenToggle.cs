using Godot;

namespace Jogo25D.UI
{
    public partial class FullscreenToggle : Node
{
	private bool isProcessing = false;

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Keycode == Key.F11 && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (!isProcessing)
			{
				isProcessing = true;
				ToggleFullscreen();
				GetViewport().SetInputAsHandled();
				// Reset flag after a small delay
				GetTree().CreateTimer(0.2).Timeout += () => isProcessing = false;
			}
		}
	}

	private void ToggleFullscreen()
	{
		var currentMode = DisplayServer.WindowGetMode();
		
		if (currentMode == DisplayServer.WindowMode.Fullscreen || currentMode == DisplayServer.WindowMode.ExclusiveFullscreen)
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		}
		else
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
		}
	}
    }
}
