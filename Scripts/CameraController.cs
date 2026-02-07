using Godot;

public partial class CameraController : Camera2D
{
	[Export] public NodePath PlayerPath;
	private Node2D player;

	public override void _Ready()
	{
		// Encontrar o player
		if (PlayerPath != null && !PlayerPath.IsEmpty)
		{
			player = GetNode<Node2D>(PlayerPath);
		}
		else
		{
			player = GetTree().Root.FindChild("Player", true, false) as Node2D;
		}
		
		// Garantir que a câmera está ativa
		Enabled = true;
	}

	public override void _Process(double delta)
	{
		if (player != null)
		{
			// Seguir o player (a suavização está configurada na cena)
			GlobalPosition = player.GlobalPosition;
		}
	}
}
