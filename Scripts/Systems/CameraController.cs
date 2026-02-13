using Godot;

namespace Jogo25D.Systems
{
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

		// Verificar se multiplayer está realmente ativo e pronto
		int localPeerId = 1;
		bool hasMultiplayer = false;
		
		// Verificar múltiplas condições antes de tentar acessar GetUniqueId
		if (Multiplayer != null && 
		    Multiplayer.MultiplayerPeer != null && 
		    Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected)
		{
			try
			{
				localPeerId = Multiplayer.GetUniqueId();
				hasMultiplayer = true;
			}
			catch
			{
				// Multiplayer ainda não está pronto
				hasMultiplayer = false;
			}
		}

		var players = GetTree().GetNodesInGroup("players");
		foreach (Node node in players)
		{
			if (node is Node2D player2D)
			{
				// Se não tem multiplayer, pegar o primeiro player
				// Se tem multiplayer, pegar apenas o player que pertence a este peer
				if (!hasMultiplayer || player2D.GetMultiplayerAuthority() == localPeerId)
				{
					player = player2D;
					return;
				}
			}

			// Fallback: procurar qualquer player na cena (para modo single player)
			player = GetTree().Root.FindChild("Player", true, false) as Node2D;
		}
	}	
    }
}