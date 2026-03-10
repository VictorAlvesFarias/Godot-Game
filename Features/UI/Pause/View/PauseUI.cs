using Godot;
using Jogo25D.Characters;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
	public partial class PauseUI : CanvasLayer
	{
		public Button ResetButton { get; set; }
		public Button ResumeButton { get; set; }
		public Button ExitButton { get; set; }
		public Button HostButton { get; set; }
		public Button ConnectButton { get; set; }
		public Button TradeDimension { get; set; }
		public LineEdit AddressInput { get; set; }
		public LineEdit PortInput { get; set; }
		public Label StatusLabel { get; set; }
		public Player PlayerNode { get; set; }
		public WorldManager NetworkManager { get; set; }

        public override void _Ready()
		{
			Visible = false;
		
			ResetButton = GetNode<Button>("Panel/VBoxContainer/ResetButton");
			ResumeButton = GetNode<Button>("Panel/VBoxContainer/NetworkContainer/ResumeButton");
			TradeDimension = GetNode<Button>("Panel/VBoxContainer/TradeDimension");
			ExitButton = GetNode<Button>("Panel/VBoxContainer/NetworkContainer/ExitButton");
			HostButton = GetNode<Button>("Panel/VBoxContainer/NetworkContainer/HostButton");
			ConnectButton = GetNode<Button>("Panel/VBoxContainer/NetworkContainer/ConnectButton");
			PortInput = GetNode<LineEdit>("Panel/VBoxContainer/NetworkContainer/PortInput");
			AddressInput = GetNode<LineEdit>("Panel/VBoxContainer/NetworkContainer/AddressInput");
			StatusLabel = GetNode<Label>("Panel/VBoxContainer/NetworkContainer/StatusLabel");
			NetworkManager = GetTree().Root.GetNode<WorldManager>(WorldManager.DEFAULT_NODE_PATH);

            ResetButton.Pressed += OnResetPressed;
			ResumeButton.Pressed += OnResumePressed;
			ExitButton.Pressed += OnExitPressed;
			HostButton.Pressed += OnHostPressed;
			ConnectButton.Pressed += OnConnectPressed;
			TradeDimension.Pressed += OnTradeDimension;

			PlayerNode = GetTree().Root.FindChild("Player", true, false) as Player;

			PortInput.PlaceholderText = "Port";
			AddressInput.PlaceholderText = "IP:Port";
		}

		public override void _Input(InputEvent @event)
		{
			if (@event.IsActionPressed("pause") && !@event.IsEcho())
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

		public void TogglePause()
		{
			Visible = !Visible;
			GetTree().Paused = Visible;
		}
	
		public void OnResetPressed()
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
				NetworkManager.ResetPlayerClientRequest();
			}
		
			TogglePause();
		}

		public void OnExitPressed()
		{
			GetTree().Quit();
		}

		public void OnResumePressed()
		{
			Visible = false;
			GetTree().Paused = false;
		}

		public void OnHostPressed()
		{
			if (NetworkManager == null)
			{
				StatusLabel.Text = "NetworkManager nÃ£o encontrado!";

				return;
			}
		
			if (NetworkManager.IsConnected())
			{
				NetworkManager.Disconnect();

				StatusLabel.Text = "Desconectado";
			}
			else
			{
				var portText = PortInput.Text.Trim();
				var port = NetworkManager.CreateServer(portText);

				StatusLabel.Text = $"Servidor criado na porta {port ?? "Porta nÃ£o encontrada"}.";
			}
		
			UpdateNetworkStatus();
		}
	
		public void OnTradeDimension()
		{
			NetworkManager.TradeDimensionClientRequest();
		}

		public void OnConnectPressed()
		{
			if (NetworkManager == null)
			{
				StatusLabel.Text = "NetworkManager nÃ£o encontrado!";
				return;
			}
		
			if (NetworkManager.IsConnected())
			{
				NetworkManager.Disconnect();
				StatusLabel.Text = "Desconectado";
			}
			else
			{
				var textAddress = AddressInput.Text.Trim();
				var address = NetworkManager.JoinServer(textAddress);

				StatusLabel.Text = $"Conectando a {address}.";
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
		
			HostButton.Text = connected && Multiplayer.IsServer() ? "STOP SERVER" : "HOST";
			ConnectButton.Text = connected && !Multiplayer.IsServer() ? "DISCONNECT" : "CONNECT";
		
			if (connected)
			{
				if (Multiplayer.IsServer())
				{
					StatusLabel.Text = "Status: SERVIDOR";
					StatusLabel.Modulate = Colors.Green;
				}
				else
				{
					StatusLabel.Text = "Status: CONECTADO";
					StatusLabel.Modulate = Colors.Green;
				}
			}
			else
			{
				StatusLabel.Text = "Status: Desconectado";
				StatusLabel.Modulate = Colors.White;
			}
		}
	}
}