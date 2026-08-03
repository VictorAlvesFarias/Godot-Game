using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;

namespace Jogo25D.Systems
{
	public partial class CameraController : Camera2D
	{
		#region Dinamic properties

		public Node2D PlayerRef { get; set; }

		#endregion

		#region Node references

		public WorldManager WorldManager { get; set; }

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			Enabled = true;

			WorldManager = GetTree().Root.GetNodeOrNull<WorldManager>(StaticNodePathsConstants.WorldManager);

            PlayerRef = WorldManager?.GetLocalPlayer();
        }

        public override void _PhysicsProcess(double delta)
		{
			if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			{
                PlayerRef = WorldManager?.GetLocalPlayer();
            }

            if (PlayerRef != null && IsInstanceValid(PlayerRef))
			{
				GlobalPosition = PlayerRef.GlobalPosition;
			}
		}

		#endregion
	}
}
