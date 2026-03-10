using Godot;
using Jogo25D.Characters;

namespace Jogo25D.Systems
{
	public partial class CameraController : Camera2D
	{
		public NodePath PlayerPath;
		public Node2D PlayerRef { get; set; }

		public override void _Ready()
		{
			Enabled = true;

			FindLocalPlayer();
		}

		public override void _PhysicsProcess(double delta)
		{
			if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			{
				FindLocalPlayer();
			}

			if (PlayerRef != null && IsInstanceValid(PlayerRef))
			{
				GlobalPosition = PlayerRef.GlobalPosition;
			}
		}

		public void FindLocalPlayer()
		{
			if (PlayerPath != null && !PlayerPath.IsEmpty)
			{
				PlayerRef = GetNodeOrNull<Node2D>(PlayerPath);
				if (PlayerRef != null)
				{
					return;
				}
			}

			var worldManager = GetTree().Root.GetNodeOrNull<WorldManager>(WorldManager.DEFAULT_NODE_PATH);

			if (worldManager != null)
			{
				var local = worldManager.GetLocalPlayer();

				if (local != null)
				{
					PlayerRef = local;
					return;
				}
			}
		}	
	}
}
