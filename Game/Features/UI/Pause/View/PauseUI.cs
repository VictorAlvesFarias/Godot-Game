using Godot;
using Jogo25D.Characters;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
	public partial class PauseUI : CanvasLayer
	{
		public Button ResumeButton { get; set; }
		public Button ExitButton { get; set; }
		public Button HostButton { get; set; }
		public Button ConnectButton { get; set; }
		public LineEdit PortInput { get; set; }
		public WorldManager NetworkManager { get; set; }

		public override void _Ready()
		{
			Visible = false;

			ResumeButton = GetNode<Button>("MarginContainer/Root/MenuColumn/ResumeButton");
			ExitButton = GetNode<Button>("MarginContainer/Root/MenuColumn/ExitButton");
			HostButton = GetNode<Button>("MarginContainer/Root/MenuColumn/HostButton");
			ConnectButton = GetNode<Button>("MarginContainer/Root/MenuColumn/ConnectButton");
			PortInput = GetNode<LineEdit>("MarginContainer/Root/MenuColumn/PortInput");
			NetworkManager = GetTree().Root.GetNode<WorldManager>(WorldManager.DEFAULT_NODE_PATH);

			ResumeButton.Pressed += OnResumePressed;
			ExitButton.Pressed += OnExitPressed;
			HostButton.Pressed += OnHostPressed;
			ConnectButton.Pressed += OnConnectPressed;
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
			}
		}

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

		public void OnConnectPressed()
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
				NetworkManager.JoinServer(PortInput.Text.Trim());
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

			HostButton.Text = connected && Multiplayer.IsServer() ? "Stop server" : "Host";
			ConnectButton.Text = connected && !Multiplayer.IsServer() ? "Disconnect" : "Connect";
		}
	}
}
