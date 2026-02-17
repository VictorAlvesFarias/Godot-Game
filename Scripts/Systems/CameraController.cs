using Godot;

namespace Jogo25D.Systems
{
    public partial class CameraController : Camera2D
{
	[Export] public NodePath PlayerPath;
	private Node2D player;

	public override void _Ready()
	{
		Enabled = true;

		FindLocalPlayer();
	}

	public override void _Process(double delta)
	{
		if (player == null || !IsInstanceValid(player))
		{
			FindLocalPlayer();
		}

		if (player != null && IsInstanceValid(player))
		{
			GlobalPosition = player.GlobalPosition;
		}
	}

	private void FindLocalPlayer()
	{
		if (PlayerPath != null && !PlayerPath.IsEmpty)
		{
			player = GetNodeOrNull<Node2D>(PlayerPath);
			if (player != null)
				return;
		}

		int localPeerId = 1;
		bool hasMultiplayer = false;
		
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
				hasMultiplayer = false;
			}
		}

		var players = GetTree().GetNodesInGroup("players");
		foreach (Node node in players)
		{
			if (node is Node2D player2D)
			{
				if (!hasMultiplayer || player2D.GetMultiplayerAuthority() == localPeerId)
				{
					player = player2D;
					return;
				}
			}

			player = GetTree().Root.FindChild("Player", true, false) as Node2D;
		}
	}	
    }
}
