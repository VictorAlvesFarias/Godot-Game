using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
	public partial class PauseMenuUI : CanvasLayer
	{
		private Button resetButton;
		private Button resumeButton;
		private Button exitButton;
		private Button hostButton;
		private Button connectButton;
		private Button tradeDimension;
		private LineEdit addressInput;
		private LineEdit portInput;
		private Label statusLabel;
		private Player player;
		private WorldManager networkManager;

		public override void _Ready()
		{
			Visible = false;
		
			resetButton = GetNode<Button>(NodePaths.PauseMenu.ResetButton);
			resumeButton = GetNode<Button>(NodePaths.PauseMenu.ResumeButton);
			tradeDimension = GetNode<Button>("Panel/VBoxContainer/TradeDimension");
			exitButton = GetNode<Button>(NodePaths.PauseMenu.ExitButton);
			hostButton = GetNode<Button>(NodePaths.PauseMenu.HostButton);
			connectButton = GetNode<Button>(NodePaths.PauseMenu.ConnectButton);
			portInput = GetNode<LineEdit>(NodePaths.PauseMenu.PortInput);
			addressInput = GetNode<LineEdit>(NodePaths.PauseMenu.AddressInput);
			statusLabel = GetNode<Label>(NodePaths.PauseMenu.StatusLabel);
            networkManager = GetTree().Root.GetNode<WorldManager>(WorldManager.DEFAULT_NODE_PATH);
		
			resetButton.Pressed += OnResetPressed;
			resumeButton.Pressed += OnResumePressed;
			exitButton.Pressed += OnExitPressed;
			hostButton.Pressed += OnHostPressed;
			connectButton.Pressed += OnConnectPressed;
			tradeDimension.Pressed += OnTradeDimension;

			player = GetTree().Root.FindChild("Player", true, false) as Player;


            portInput.PlaceholderText = "Port";
			addressInput.PlaceholderText = "IP:Port";

			UpdateNetworkStatus();
		}

		public override void _Input(InputEvent @event)
		{
			if (Input.IsActionJustPressed("pause"))
			{
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

		private void TogglePause()
		{
			Visible = !Visible;
			GetTree().Paused = Visible;
		}
	
		private void OnResetPressed()
		{
			Player localPlayer = null;
			bool hasMultiplayer = Multiplayer.HasMultiplayerPeer();
			int localPeerId = hasMultiplayer ? Multiplayer.GetUniqueId() : 0;
			var players = GetTree().GetNodesInGroup("players");

			foreach (Node node in players)
			{
				if (node is Player p)
				{
					if (!hasMultiplayer || p.GetMultiplayerAuthority() == localPeerId)
					{
						localPlayer = p;
	
						break;
					}
				}
			}
		
			if (localPlayer != null && IsInstanceValid(localPlayer))
			{
				networkManager.ResetPlayerClientRequest();
            }
		
			TogglePause();
		}

		private void OnExitPressed()
		{
			GetTree().Quit();
		}

		private void OnResumePressed()
		{
			Visible = false;
			GetTree().Paused = false;
		}

		private void OnHostPressed()
		{
			if (networkManager == null)
			{
				statusLabel.Text = "NetworkManager não encontrado!";

				return;
			}
		
			if (networkManager.IsConnected())
			{
				networkManager.Disconnect();

				statusLabel.Text = "Desconectado";
			}
			else
			{
				var portText = portInput.Text.Trim();
				var port = networkManager.CreateServer(portText);

				statusLabel.Text = $"Servidor criado na porta {port ?? "Porta não encontrada"}.";
			}
		
			UpdateNetworkStatus();
		}
	
		private void OnTradeDimension()
		{
			networkManager.TradeDimensionClientRequest();
		}

		private void OnConnectPressed()
		{
			if (networkManager == null)
			{
				statusLabel.Text = "NetworkManager não encontrado!";
				return;
			}
		
			if (networkManager.IsConnected())
			{
				networkManager.Disconnect();
				statusLabel.Text = "Desconectado";
			}
			else
			{
				var textAddress = addressInput.Text.Trim();
				var address = networkManager.JoinServer(textAddress);

				statusLabel.Text = $"Conectando a {address}.";
			}

			UpdateNetworkStatus();
		}
	
		private void UpdateNetworkStatus()
		{
			if (networkManager == null)
			{ 
				return;
			}
			
			bool connected = networkManager.IsConnected();
		
			hostButton.Text = connected && Multiplayer.IsServer() ? "STOP SERVER" : "HOST";
			connectButton.Text = connected && !Multiplayer.IsServer() ? "DISCONNECT" : "CONNECT";
		
			if (connected)
			{
				if (Multiplayer.IsServer())
				{
					statusLabel.Text = "Status: SERVIDOR";
					statusLabel.Modulate = Colors.Green;
				}
				else
				{
					statusLabel.Text = "Status: CONECTADO";
					statusLabel.Modulate = Colors.Green;
				}
			}
			else
			{
				statusLabel.Text = "Status: Desconectado";
				statusLabel.Modulate = Colors.White;
			}
		}
	}
}
