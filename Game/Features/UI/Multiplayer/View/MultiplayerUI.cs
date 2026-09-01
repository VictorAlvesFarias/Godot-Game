using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Systems;
using System.Collections.Generic;

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

            PopulateConnectionRows();
        }

        #endregion

        #region Core - Setup

        private void Initialize()
        {
            Game.Ui.MultiplayerUI.AddConnectionButton.Node.Pressed += OnAddConnectionPressed;
            Game.Ui.MultiplayerUI.BackButton.Node.Pressed += OnBackPressed;

            Game.Managers.SessionManager.Node.CharacterSelectionRequired += OnCharacterSelectionRequired;

            Game.Ui.MultiplayerUI.ServerRowTemplate.Node.Visible = false;

            PopulateConnectionRows();
        }

        #endregion

        #region Core - Lista de conexoes

        public void PopulateConnectionRows()
        {
            var lista = Game.Ui.MultiplayerUI.ListContainer.Node;
            var template = Game.Ui.MultiplayerUI.ServerRowTemplate.Node;

            if (lista == null || template == null)
            {
                return;
            }

            foreach (var filho in lista.GetChildren())
            {
                if (filho == template)
                {
                    // O template fica na cena para poder ser editado, mas nunca aparece em jogo.
                    template.Visible = false;

                    continue;
                }

                filho.QueueFree();
            }

            foreach (var conexao in SaveStorage.ListConnections())
            {
                lista.AddChild(CreateConnectionRow(conexao));
            }
        }

        private Control CreateConnectionRow(ServerConnectionData conexao)
        {
            var linha = (Control)Game.Ui.MultiplayerUI.ServerRowTemplate.Node.Duplicate();

            linha.Visible = true;

            var nome = linha.GetNode<Label>("MarginContainer/HBoxContainer/WorldNameLabel");
            var conectar = linha.GetNode<Button>("MarginContainer/HBoxContainer/ConnectButton");
            var excluir = linha.GetNode<Button>("MarginContainer/HBoxContainer/DeleteButton");

            nome.Text = $"{conexao.Description}\n{conexao.Ip}:{conexao.Port}";

            conectar.Pressed += delegate { Connect($"{conexao.Ip}:{conexao.Port}"); };

            excluir.Pressed += delegate
            {
                SaveStorage.DeleteConnection(conexao.ConnectionId);

                PopulateConnectionRows();
            };

            return linha;
        }

        #endregion

        #region UI - Events

        public void Connect(string endereco)
        {
            if (Game.Managers.WorldManager.Node == null)
            {
                return;
            }

            var address = Game.Managers.SessionManager.Node.SpawnWorldAndJoin(endereco);

            if (string.IsNullOrEmpty(address))
            {
                Game.Ui.ErrorModalUI.Node?.ShowError(Game.Managers.NetworkManager.Node.LastJoinError ?? "Não foi possível conectar.");

                return;
            }

            Game.Ui.MultiplayerUI.StatusLabel.Node.Text = $"Conectando em {endereco}...";

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

        public void OnAddConnectionPressed()
        {
            Game.Managers.RouterManager.Node.Close(this);
            Game.Managers.RouterManager.Node.Open(Game.Ui.AddConnectionUI.Node);
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
        }

        private void OnCharacterSelectionRequired()
        {
            Game.Managers.RouterManager.Node.Open(Game.Ui.CharacterSelectUI.Node);
        }

        #endregion
    }
}
