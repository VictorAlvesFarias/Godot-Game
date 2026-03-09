using Godot;

namespace Jogo25D.Systems
{
	public partial class CameraController : Camera2D
	{
		public NodePath PlayerPath;
		public Node2D Player { get; set; }

		public override void _Ready()
		{
			Enabled = true;

			FindLocalPlayer();
		}

		public override void _PhysicsProcess(double delta)
		{
			if (Player == null || !IsInstanceValid(Player))
			{
				FindLocalPlayer();
			}

			if (Player != null && IsInstanceValid(Player))
			{
				GlobalPosition = Player.GlobalPosition;
			}
		}

		public void FindLocalPlayer()
		{
			if (PlayerPath != null && !PlayerPath.IsEmpty)
			{
				Player = GetNodeOrNull<Node2D>(PlayerPath);
				if (Player != null)
				{
					return;
				}
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
						Player = player2D;
						return;
					}
				}

				Player = GetTree().Root.FindChild("Player", true, false) as Node2D;
			}
		}	
	}
}
