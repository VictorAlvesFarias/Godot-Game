using Godot;

public partial class DisplaySettings : Node
{
	// Resoluções comuns suportadas
	public static readonly Vector2I[] CommonResolutions = new[]
	{
		new Vector2I(1280, 720),   // HD
		new Vector2I(1920, 1080),  // Full HD
		new Vector2I(2560, 1440),  // 2K
		new Vector2I(3840, 2160),  // 4K
		new Vector2I(1366, 768),   // Laptop comum
		new Vector2I(1600, 900),   // HD+
		new Vector2I(1024, 768),   // XGA
		new Vector2I(1440, 900),   // WXGA+
	};

	public static void SetResolution(Vector2I resolution, bool fullscreen = false)
	{
		if (fullscreen)
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
		}
		else
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
			DisplayServer.WindowSetSize(resolution);
			
			// Centralizar a janela
			var screenSize = DisplayServer.ScreenGetSize();
			var windowPos = (screenSize - resolution) / 2;
			DisplayServer.WindowSetPosition(windowPos);
		}
	}

	public static void ToggleFullscreen()
	{
		var currentMode = DisplayServer.WindowGetMode();
		if (currentMode == DisplayServer.WindowMode.Fullscreen || currentMode == DisplayServer.WindowMode.ExclusiveFullscreen)
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
			// Restaurar tamanho padrão da janela
			DisplayServer.WindowSetSize(new Vector2I(1920, 1080));
			// Centralizar
			var screenSize = DisplayServer.ScreenGetSize();
			var windowPos = (screenSize - new Vector2I(1920, 1080)) / 2;
			DisplayServer.WindowSetPosition(windowPos);
		}
		else
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
		}
	}

	public static Vector2I GetCurrentResolution()
	{
		return DisplayServer.WindowGetSize();
	}

	public static bool IsFullscreen()
	{
		return DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
	}
}
