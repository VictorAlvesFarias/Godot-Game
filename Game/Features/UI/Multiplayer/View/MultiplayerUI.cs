using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
	public partial class MultiplayerUI : ScreenUI
	{
		#region Node references


		#endregion

		#region Systems

		public Timer ConnectTimeoutTimer { get; set; }

		#endregion

		#region Godot implementation

		public override void _Ready()
		{

			ConnectTimeoutTimer = new Timer();
			ConnectTimeoutTimer.OneShot = true;
			ConnectTimeoutTimer.WaitTime = 8f;
			ConnectTimeoutTimer.Timeout += OnConnectTimeout;
			AddChild(ConnectTimeoutTimer);

			Game.WhenReady(Initialize);
		}

		#endregion

		#region Core - Setup

		private void Initialize()
		{
			Game.Ui.MultiplayerUI.ConnectButton.Node.Pressed += OnConnectPressed;
			Game.Ui.MultiplayerUI.WorldsButton.Node.Pressed += OnWorldsPressed;
			Game.Ui.MultiplayerUI.BackButton.Node.Pressed += OnBackPressed;

			Game.Managers.SaveManager.Node.ServerCharacterListAvailable += OnServerCharacterListAvailable;

			PopulateMockList();
		}

		#endregion

		#region Core - Setup

		private void PopulateMockList()
		{
			var template = Game.Ui.MultiplayerUI.ServerRowTemplate.Node;

			if (template == null)
			{
				GD.PushError("MultiplayerUI: ServerRowTemplate não encontrado em Game.Ui.MultiplayerUI.ListContainer.Node.");

				return;
			}

			template.Visible = false;

			foreach (var worldName in new[] { "Servidor da Guilda", "Mundo dos Amigos", "Arena PvP" })
			{
				var row = (PanelContainer)template.Duplicate();
				row.Visible = true;

				var label = row.GetNode<Label>("MarginContainer/HBoxContainer/WorldNameLabel");
				label.Text = worldName;

				Game.Ui.MultiplayerUI.ListContainer.Node.AddChild(row);
			}
		}

		#endregion

		#region Public API

		public override void OnOpened()
		{

			StopWaitingForConnection();

			Game.Ui.MultiplayerUI.StatusLabel.Node.Text = "";
		}

		public override void OnClosed()
		{
		}

		#endregion

		#region Core - Actions

		public void OnConnectPressed()
		{
			if (Game.Managers.WorldManager.Node == null)
			{
				return;
			}

			var address = Game.Managers.SessionManager.Node.SpawnWorldAndJoin(Game.Ui.MultiplayerUI.AddressInput.Node.Text.Trim());

			if (string.IsNullOrEmpty(address))
			{
				var reason = string.IsNullOrEmpty(Game.Managers.NetworkManager.Node.LastJoinError)
					? "Não foi possível conectar."
					: Game.Managers.NetworkManager.Node.LastJoinError;

				Game.Ui.ErrorModalUI.Node?.ShowError(reason);

				return;
			}

			Game.Ui.MultiplayerUI.ConnectButton.Node.Disabled = true;
			Game.Ui.MultiplayerUI.StatusLabel.Node.Text = "Conectando...";

			Game.Managers.NetworkManager.Node.ConnectionSucceeded += OnConnectionSucceeded;
			Game.Managers.NetworkManager.Node.ConnectionAttemptFailed += OnConnectionAttemptFailed;

			ConnectTimeoutTimer.Start();
		}

		private void OnConnectionSucceeded()
		{
			StopWaitingForConnection();

			Game.Managers.RouterManager.Node.Close(this);
		}

		private void OnConnectionAttemptFailed()
		{
			StopWaitingForConnection();

			Game.Ui.MultiplayerUI.StatusLabel.Node.Text = "";

			Game.Ui.ErrorModalUI.Node?.ShowError("Falha ao conectar. Verifique o IP:Porta, e se a porta está liberada no firewall/roteador de quem está hospedando.");
		}

		private void OnConnectTimeout()
		{
			Game.Managers.NetworkManager.Node?.Disconnect();

			StopWaitingForConnection();

			Game.Ui.MultiplayerUI.StatusLabel.Node.Text = "";

			Game.Ui.ErrorModalUI.Node?.ShowError("Tempo esgotado tentando conectar. Verifique o IP:Porta, e se a porta está liberada no firewall/roteador de quem está hospedando.");
		}

		private void StopWaitingForConnection()
		{
			ConnectTimeoutTimer.Stop();

			if (Game.Managers.WorldManager.Node != null)
			{
				Game.Managers.NetworkManager.Node.ConnectionSucceeded -= OnConnectionSucceeded;
				Game.Managers.NetworkManager.Node.ConnectionAttemptFailed -= OnConnectionAttemptFailed;
			}

			Game.Ui.MultiplayerUI.ConnectButton.Node.Disabled = false;
		}

		public void OnWorldsPressed()
		{
			Game.Managers.RouterManager.Node.Open(Game.Ui.WorldSelectUI.Node);
		}

		private void OnServerCharacterListAvailable(string multiplayerKey, Godot.Collections.Array summaries)
		{
			Game.Ui.CharacterSelectUI.Node.CurrentContext = CharacterSelectContext.PeerJoinServer;
			Game.Ui.CharacterSelectUI.Node.LastMultiplayerKey = multiplayerKey;
			Game.Ui.CharacterSelectUI.Node.LastServerSummaries = summaries;

			Game.Managers.RouterManager.Node.Open(Game.Ui.CharacterSelectUI.Node);
		}

		public void OnBackPressed()
		{
			Game.Managers.RouterManager.Node.Close(this);

			var startUi = Game.Ui.StartUI.Node;

			if (startUi != null)
			{
				Game.Managers.RouterManager.Node.Open(startUi);
			}
		}

		#endregion
	}
}
