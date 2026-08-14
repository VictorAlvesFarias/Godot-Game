using Godot;
using Jogo25D.Constants;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
	public partial class MultiplayerUI : CanvasLayer
	{
		#region Node references

		public LineEdit SearchInput { get; set; }
		public VBoxContainer ListContainer { get; set; }
		public LineEdit AddressInput { get; set; }
		public Button ConnectButton { get; set; }
		public Button WorldsButton { get; set; }
		public Button BackButton { get; set; }
		public Label StatusLabel { get; set; }
		public WorldManager NetworkManager { get; set; }
		public ErrorModalUI ErrorModal { get; set; }

		#endregion

		#region Systems

		public Timer ConnectTimeoutTimer { get; set; }

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			Layer = 20;
			Visible = false;

			SearchInput = GetNode<LineEdit>("MarginContainer/Root/SearchInput");
			ListContainer = GetNode<VBoxContainer>("MarginContainer/Root/ListScroll/ListContainer");
			AddressInput = GetNode<LineEdit>("MarginContainer/Root/ConnectRow/AddressInput");
			ConnectButton = GetNode<Button>("MarginContainer/Root/ConnectRow/ConnectButton");
			WorldsButton = GetNode<Button>("MarginContainer/Root/ButtonRow/WorldsButton");
			BackButton = GetNode<Button>("MarginContainer/Root/ButtonRow/BackButton");
			StatusLabel = GetNode<Label>("MarginContainer/Root/StatusLabel");
			NetworkManager = GetTree().Root.GetNodeOrNull<WorldManager>(StaticNodePathsConstants.WorldManager);
			ErrorModal = GetTree().Root.GetNodeOrNull<ErrorModalUI>("Main/Ui/ErrorModalUI");

			ConnectButton.Pressed += OnConnectPressed;
			WorldsButton.Pressed += OnWorldsPressed;
			BackButton.Pressed += OnBackPressed;

			NetworkManager.ServerCharacterListAvailable += OnServerCharacterListAvailable;

			ConnectTimeoutTimer = new Timer();
			ConnectTimeoutTimer.OneShot = true;
			ConnectTimeoutTimer.WaitTime = 8f;
			ConnectTimeoutTimer.Timeout += OnConnectTimeout;
			AddChild(ConnectTimeoutTimer);

			PopulateMockList();
		}

		#endregion

		#region Core - Setup

		private void PopulateMockList()
		{
			var template = ListContainer.GetNodeOrNull<PanelContainer>("ServerRowTemplate");

			if (template == null)
			{
				GD.PushError("MultiplayerUI: ServerRowTemplate não encontrado em ListContainer.");

				return;
			}

			template.Visible = false;

			foreach (var worldName in new[] { "Servidor da Guilda", "Mundo dos Amigos", "Arena PvP" })
			{
				var row = (PanelContainer)template.Duplicate();
				row.Visible = true;

				var label = row.GetNode<Label>("MarginContainer/HBoxContainer/WorldNameLabel");
				label.Text = worldName;

				ListContainer.AddChild(row);
			}
		}

		#endregion

		#region Public API

		public void Open()
		{
			Visible = true;

			StopWaitingForConnection();

			if (StatusLabel != null)
			{
				StatusLabel.Text = "";
			}
		}

		public void Close()
		{
			Visible = false;
		}

		#endregion

		#region Core - Actions

		public void OnConnectPressed()
		{
			if (NetworkManager == null)
			{
				return;
			}

			var address = NetworkManager.SpawnWorldAndJoin(AddressInput.Text.Trim());

			if (string.IsNullOrEmpty(address))
			{
				var reason = string.IsNullOrEmpty(NetworkManager.LastJoinError)
					? "Não foi possível conectar."
					: NetworkManager.LastJoinError;

				ErrorModal?.ShowError(reason);

				return;
			}

			ConnectButton.Disabled = true;
			StatusLabel.Text = "Conectando...";

			NetworkManager.ConnectionSucceeded += OnConnectionSucceeded;
			NetworkManager.ConnectionAttemptFailed += OnConnectionAttemptFailed;

			ConnectTimeoutTimer.Start();
		}

		private void OnConnectionSucceeded()
		{
			StopWaitingForConnection();

			Close();
		}

		private void OnConnectionAttemptFailed()
		{
			StopWaitingForConnection();

			StatusLabel.Text = "";

			ErrorModal?.ShowError("Falha ao conectar. Verifique o IP:Porta, e se a porta está liberada no firewall/roteador de quem está hospedando.");
		}

		private void OnConnectTimeout()
		{
			NetworkManager?.Disconnect();

			StopWaitingForConnection();

			StatusLabel.Text = "";

			ErrorModal?.ShowError("Tempo esgotado tentando conectar. Verifique o IP:Porta, e se a porta está liberada no firewall/roteador de quem está hospedando.");
		}

		private void StopWaitingForConnection()
		{
			ConnectTimeoutTimer.Stop();

			if (NetworkManager != null)
			{
				NetworkManager.ConnectionSucceeded -= OnConnectionSucceeded;
				NetworkManager.ConnectionAttemptFailed -= OnConnectionAttemptFailed;
			}

			ConnectButton.Disabled = false;
		}

		public void OnWorldsPressed()
		{
			Close();

			GetTree().Root.GetNodeOrNull<WorldSelectUI>("Main/Ui/WorldSelectUI")?.Open();
		}

		private void OnServerCharacterListAvailable(string multiplayerKey, Godot.Collections.Array summaries)
		{
			Close();

			GetTree().Root.GetNodeOrNull<CharacterSelectUI>("Main/Ui/CharacterSelectUI")?.OpenServer(multiplayerKey, summaries);
		}

		public void OnBackPressed()
		{
			Close();

			var startUi = GetTree().Root.GetNodeOrNull<StartUI>("Main/Ui/StartUI");

			if (startUi != null)
			{
				startUi.Visible = true;
			}
		}

		#endregion
	}
}
