using Godot;

public partial class PauseMenu : CanvasLayer
{
	private Panel panel;
	private Button fullscreenButton;
	private Button resetButton;
	private Button exitButton;
	private Player player;

	public override void _Ready()
	{
		// Ocultar o menu inicialmente
		Visible = false;
		
		// Obter referências dos botões
		panel = GetNode<Panel>("Panel");
		fullscreenButton = GetNode<Button>("Panel/VBoxContainer/FullscreenButton");
		resetButton = GetNode<Button>("Panel/VBoxContainer/ResetButton");
		exitButton = GetNode<Button>("Panel/VBoxContainer/ExitButton");
		
		// Conectar sinais dos botões
		fullscreenButton.Pressed += OnFullscreenPressed;
		resetButton.Pressed += OnResetPressed;
		exitButton.Pressed += OnExitPressed;
		
		// Encontrar o player na cena
		player = GetTree().Root.FindChild("Player", true, false) as Player;
	}

	public override void _Input(InputEvent @event)
	{
		if (Input.IsActionJustPressed("pause"))
		{
			TogglePause();
		}
	}

	private void TogglePause()
	{
		Visible = !Visible;
		GetTree().Paused = Visible;
	}
    
	private void OnFullscreenPressed()
	{
		DisplaySettings.ToggleFullscreen();
	}

	private void OnResetPressed()
	{
		// Resetar posição do player
		if (player != null)
		{
			player.ResetPosition();
		}
		
		// Fechar o menu
		TogglePause();
	}

	private void OnExitPressed()
	{
		// Sair do jogo
		GetTree().Quit();
	}
}
