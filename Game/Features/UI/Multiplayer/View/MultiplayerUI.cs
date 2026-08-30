using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class MultiplayerUI : ScreenUI
    {
        #region Dinamic properties

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

        #region ScreenUI implementation

        public override void OnOpened()
        {
            StopWaitingForConnection();

            Game.Ui.MultiplayerUI.StatusLabel.Node.Text = "";
        }

        #endregion

        #region Core - Setup

        private void Initialize()
        {
            Game.Ui.MultiplayerUI.ConnectButton.Node.Pressed += OnConnectPressed;
            Game.Ui.MultiplayerUI.WorldsButton.Node.Pressed += OnWorldsPressed;
            Game.Ui.MultiplayerUI.BackButton.Node.Pressed += OnBackPressed;

            Game.Managers.SessionManager.Node.CharacterSelectionRequired += OnCharacterSelectionRequired;
        }

        #endregion

        #region UI - Events

        public void OnConnectPressed()
        {
            if (Game.Managers.WorldManager.Node == null)
            {
                return;
            }

            var address = Game.Managers.SessionManager.Node.SpawnWorldAndJoin(Game.Ui.MultiplayerUI.AddressInput.Node.Text.Trim());

            if (string.IsNullOrEmpty(address))
            {
                Game.Ui.ErrorModalUI.Node?.ShowError(Game.Managers.NetworkManager.Node.LastJoinError ?? "Não foi possível conectar.");

                return;
            }

            Game.Ui.MultiplayerUI.ConnectButton.Node.Disabled = true;
            Game.Ui.MultiplayerUI.StatusLabel.Node.Text = "Conectando...";

            Game.Managers.NetworkManager.Node.ConnectionSucceeded += OnConnectionSucceeded;
            Game.Managers.NetworkManager.Node.ConnectionAttemptFailed += OnConnectionAttemptFailed;

            ConnectTimeoutTimer.Start();
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

        public void OnWorldsPressed()
        {
            Game.Managers.RouterManager.Node.Open(Game.Ui.WorldSelectUI.Node);
        }

        #endregion

        #region Managers - Events

        private void OnConnectionSucceeded()
        {
            StopWaitingForConnection();
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

        private void OnCharacterSelectionRequired()
        {
            Game.Managers.RouterManager.Node.Open(Game.Ui.CharacterSelectUI.Node);
        }

        #endregion
    }
}
