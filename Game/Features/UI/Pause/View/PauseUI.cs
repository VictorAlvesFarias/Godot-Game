using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
	public partial class PauseUI : CanvasLayer
	{
		#region Node references

		public WorldManager NetworkManager { get; set; }

		#endregion

		#region Node children references

		public Button ResumeButton { get; set; }
		public Button ExitButton { get; set; }
		public Button HostButton { get; set; }
		public Button PvpButton { get; set; }
		public Button MenuButton { get; set; }
		public LineEdit PortInput { get; set; }

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			Visible = false;

			ResumeButton = GetNode<Button>("MarginContainer/Root/MenuColumn/ResumeButton");
			ExitButton = GetNode<Button>("MarginContainer/Root/MenuColumn/ExitButton");
			HostButton = GetNode<Button>("MarginContainer/Root/MenuColumn/HostButton");
			PvpButton = GetNode<Button>("MarginContainer/Root/MenuColumn/PvpButton");
			MenuButton = GetNode<Button>("MarginContainer/Root/MenuColumn/MenuButton");
			PortInput = GetNode<LineEdit>("MarginContainer/Root/MenuColumn/PortInput");
			NetworkManager = GetTree().Root.GetNodeOrNull<WorldManager>(StaticNodePathsConstants.WorldManager);

			ResumeButton.Pressed += OnResumePressed;
			ExitButton.Pressed += OnExitPressed;
			HostButton.Pressed += OnHostPressed;
			PvpButton.Pressed += OnPvpPressed;
			MenuButton.Pressed += OnMenuPressed;
		}

		public override void _Input(InputEvent @event)
		{
			if (@event.IsActionPressed("pause") && !@event.IsEcho())
			{
				var input = NetworkManager?.GetLocalPlayer()?.Input;

				if (!Visible && (input?.IsBlockedByOther("pause") ?? false))
				{
					return;
				}

				TogglePause();
			}
		}

		public override void _Process(double delta)
		{
			if (Visible)
			{
				UpdateNetworkStatus();
				UpdatePvpStatus();
			}
		}

		#endregion

		#region Core - Pause

		public void TogglePause()
		{
			Visible = !Visible;

			if (!IsMultiplayerActive())
			{
				GetTree().Paused = Visible;
			}

			var input = NetworkManager?.GetLocalPlayer()?.Input;

			if (Visible)
			{
				input?.AddBlocker("pause");
			}
			else
			{
				input?.RemoveBlocker("pause");
			}
		}

		public bool IsMultiplayerActive()
		{
			return Multiplayer != null && Multiplayer.HasMultiplayerPeer();
		}

		public void OnExitPressed()
		{
			GetTree().Quit();
		}

		public void OnResumePressed()
		{
			Visible = false;
			GetTree().Paused = false;

			NetworkManager?.GetLocalPlayer()?.Input?.RemoveBlocker("pause");
		}

		public void OnMenuPressed()
		{
			Visible = false;
			GetTree().Paused = false;

			NetworkManager?.GetLocalPlayer()?.Input?.RemoveBlocker("pause");
			NetworkManager?.LeaveWorld();

			var startUi = GetTree().Root.GetNodeOrNull<StartUI>("Main/Ui/StartUI");

			if (startUi != null)
			{
				startUi.Visible = true;
			}
		}

		#endregion

		#region Core - Network

		public void OnHostPressed()
		{
			if (NetworkManager == null)
			{
				return;
			}

			if (NetworkManager.IsConnected())
			{
				NetworkManager.Disconnect();
			}
			else
			{
				var portText = PortInput.Text.Trim();

				NetworkManager.CreateServer(portText);
			}

			UpdateNetworkStatus();
		}

		public void UpdateNetworkStatus()
		{
			if (NetworkManager == null)
			{
				return;
			}

			bool connected = NetworkManager.IsConnected();
			bool isServer = Multiplayer.IsServer();

			HostButton.Visible = !connected || isServer;
			PortInput.Visible = HostButton.Visible;

			HostButton.Text = connected && isServer ? "Stop server" : "Host";
		}

		#endregion

		#region Core - Pvp

		public void OnPvpPressed()
		{
			var localPlayer = NetworkManager?.GetLocalPlayer();

			if (localPlayer == null)
			{
				return;
			}

			localPlayer.SetPvpEnabledRequest(!localPlayer.Data.PvpEnabled);

			UpdatePvpStatus();
		}

		public void UpdatePvpStatus()
		{
			var localPlayer = NetworkManager?.GetLocalPlayer();

			PvpButton.Text = localPlayer != null && localPlayer.Data.PvpEnabled ? "PvP" : "PvE";
		}

		#endregion
	}
}
