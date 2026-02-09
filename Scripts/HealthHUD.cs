using Godot;

public partial class HealthHUD : Label
{
	private Player localPlayer;

	public override void _Ready()
	{
		// Nada a fazer aqui - configurações visuais já estão no .tscn
	}

	public override void _Process(double delta)
	{
		// Tentar encontrar o player local se ainda não foi encontrado
		if (localPlayer == null || !IsInstanceValid(localPlayer))
		{
			FindLocalPlayer();
		}

		// Atualizar display de corações
		if (localPlayer != null && IsInstanceValid(localPlayer))
		{
			UpdateHealthDisplay();
		}
		else
		{
			Text = "";
		}
	}

	private void FindLocalPlayer()
	{
		var players = GetTree().GetNodesInGroup("players");
		
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
		
		foreach (Node node in players)
		{
			if (node is Player player)
			{
				// Se não tem multiplayer, pegar o primeiro player encontrado
				// Se tem multiplayer, pegar apenas o player que pertence a este peer
				if (!hasMultiplayer || player.GetMultiplayerAuthority() == localPeerId)
				{
					localPlayer = player;
					break;
				}
			}
		}
	}

	private void UpdateHealthDisplay()
	{
		// Desenhar corações cheios baseado na vida atual
		string hearts = "";
		for (int i = 0; i < localPlayer.CurrentHealth; i++)
		{
			hearts += "♥ ";
		}
		
		// Desenhar corações vazios para vida perdida
		int lostHealth = localPlayer.MaxHealth - localPlayer.CurrentHealth;
		for (int i = 0; i < lostHealth; i++)
		{
			hearts += "♡ ";
		}
		
		Text = hearts.Trim();
	}
}
