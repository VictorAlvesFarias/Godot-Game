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
		GD.Print($"Modo atual: {currentMode}");
		
		if (currentMode == DisplayServer.WindowMode.Fullscreen || currentMode == DisplayServer.WindowMode.ExclusiveFullscreen)
		{
			GD.Print("Tentando mudar para Windowed");
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		}
		else
		{
			GD.Print("Tentando mudar para Fullscreen");
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
		}
		
		// Verificar o novo modo
		var newMode = DisplayServer.WindowGetMode();
		GD.Print($"Novo modo: {newMode}");
	}
    }
}
