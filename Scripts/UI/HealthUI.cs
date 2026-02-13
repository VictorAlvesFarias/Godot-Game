using Godot;
using Jogo25D.Characters;

namespace Jogo25D.UI
{
    public partial class HealthUI : Label
{
	private Player localPlayer;

	public override void _Ready()
	{
		return;
	}

	public override void _Process(double delta)
	{
		if (localPlayer == null || !IsInstanceValid(localPlayer))
		{
			FindLocalPlayer();
		}

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
		var localPeerId = 1;
		var hasMultiplayer = false;
		

		if (Multiplayer != null && Multiplayer.MultiplayerPeer != null && Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected)
		{
			try
			{
				localPeerId = Multiplayer.GetUniqueId();
				hasMultiplayer = true;
			}
			catch
			{
				hasMultiplayer = false;
			}
		}
		
		foreach (Node node in players)
		{
			if (node is Player player)
			{
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
		var hearts = "";

		for (int i = 0; i < localPlayer.CurrentHealth; i++)
		{
			hearts += "♥ ";
		}
		
		var lostHealth = localPlayer.MaxHealth - localPlayer.CurrentHealth;

		for (int i = 0; i < lostHealth; i++)
		{
			hearts += "♡ ";
		}
		
		Text = hearts.Trim();
	}
    }
}
