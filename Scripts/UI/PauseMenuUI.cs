using Godot;
using Jogo25D.Characters;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class PauseMenuUI : CanvasLayer
{
	private Button resetButton;
	private Button exitButton;
	private Button hostButton;
	private Button connectButton;
	private LineEdit addressInput;
	private LineEdit portInput;
	private Label statusLabel;
	private Player player;
	private NetworkManager networkManager;

	public override void _Ready()
	{
		// Ocultar o menu inicialmente
		Visible = false;
		
		// Obter referências dos botões
		resetButton = GetNode<Button>("Panel/VBoxContainer/ResetButton");
		exitButton = GetNode<Button>("Panel/VBoxContainer/ExitButton");
		hostButton = GetNode<Button>("Panel/VBoxContainer/NetworkContainer/HostButton");
		connectButton = GetNode<Button>("Panel/VBoxContainer/NetworkContainer/ConnectButton");
		portInput = GetNode<LineEdit>("Panel/VBoxContainer/NetworkContainer/PortInput");
		addressInput = GetNode<LineEdit>("Panel/VBoxContainer/NetworkContainer/AddressInput");
		statusLabel = GetNode<Label>("Panel/VBoxContainer/NetworkContainer/StatusLabel");
		
		// Conectar sinais dos botões
		resetButton.Pressed += OnResetPressed;
		exitButton.Pressed += OnExitPressed;
		hostButton.Pressed += OnHostPressed;
		connectButton.Pressed += OnConnectPressed;
		
		// Encontrar o player na cena
		player = GetTree().Root.FindChild("Player", true, false) as Player;
		
		// Encontrar o NetworkManager
		networkManager = GetNode<NetworkManager>("/root/Main/NetworkManager");
		
		// Configurar texto padrão
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
            localPlayer.Rpc(nameof(Player.ResetPlayer));
		}
		
		TogglePause();
	}

	private void OnExitPressed()
	{
		GetTree().Quit();
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
			// Obter porta do input
			string portText = portInput.Text.Trim();
			int port = 7777;
			
			if (!string.IsNullOrEmpty(portText))
			{
				if (!int.TryParse(portText, out port))
				{
					port = 7777;
				}
			}
			
			networkManager.CreateServer(port);
			statusLabel.Text = $"Servidor criado na porta {port}!";
		}
		
		UpdateNetworkStatus();
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
			// Obter IP:Porta do campo de endereço
			string address = addressInput.Text.Trim();
			
			if (string.IsNullOrEmpty(address))
			{
				address = "127.0.0.1:7777";
			}
			
			// Parsear endereço e porta
			string[] parts = address.Split(':');
			string ip = parts[0];
			int port = parts.Length > 1 && int.TryParse(parts[1], out int parsedPort) ? parsedPort : 7777;
			
			networkManager.JoinServer(ip, port);
			statusLabel.Text = $"Conectando a {ip}:{port}...";
		}
		
		UpdateNetworkStatus();
	}
	
	private void UpdateNetworkStatus()
	{
		if (networkManager == null)
			return;
			
		bool connected = networkManager.IsConnected();
		
		// Atualizar textos dos botões
		hostButton.Text = connected && networkManager.IsServer() ? "STOP SERVER" : "HOST";
		connectButton.Text = connected && !networkManager.IsServer() ? "DISCONNECT" : "CONNECT";
		
		// Atualizar status
		if (connected)
		{
			if (networkManager.IsServer())
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
