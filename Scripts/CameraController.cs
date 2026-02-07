using Godot;

public partial class CameraController : Camera2D
{
	[Export] public NodePath PlayerPath;
	private Node2D player;

	public override void _Ready()
	{
		// Garantir que a câmera está ativa
		Enabled = true;
		
		// Tentar encontrar o player
		FindLocalPlayer();
	}

	public override void _Process(double delta)
	{
		// Se não temos player, tentar encontrar
		if (player == null || !IsInstanceValid(player))
		{
			FindLocalPlayer();
		}
		
		if (player != null && IsInstanceValid(player))
		{
			// Seguir o player (a suavização está configurada na cena)
			GlobalPosition = player.GlobalPosition;
		}
	}
	
	private void FindLocalPlayer()
	{
		// Primeiro tentar usar o caminho exportado
		if (PlayerPath != null && !PlayerPath.IsEmpty)
		{
			player = GetNodeOrNull<Node2D>(PlayerPath);
			if (player != null)
				return;
		}
		
		// Procurar por um player com autoridade de multiplayer (player local)
		var players = GetTree().GetNodesInGroup("players");
		foreach (Node node in players)
		{
			if (node is Node2D player2D && player2D.IsMultiplayerAuthority())
			{
				player = player2D;
				GD.Print($"Câmera seguindo: {player.Name}");
				return;
			}
		}
		
		// Fallback: procurar qualquer player na cena (para modo single player)
		player = GetTree().Root.FindChild("Player", true, false) as Node2D;
	}
}
