using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Network;
using Jogo25D.Save.Resources;
using Jogo25D.Save.Types;
using Jogo25D.Session;
using Jogo25D.Systems;
using System.Collections.Generic;

namespace Jogo25D.UI
{
    public partial class MultiplayerUI : ScreenUI
    {
        #region Node children references

        public LineEdit SearchInput { get; private set; }
        public VBoxContainer ListContainer { get; private set; }
        public PanelContainer ServerRowTemplate { get; private set; }
        public Button AddConnectionButton { get; private set; }
        public Button BackButton { get; private set; }
        public Label StatusLabel { get; private set; }

        #endregion

        #region Dinamic properties

        public Timer ConnectTimeoutTimer { get; set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            ResolveChildren();
            ConnectTimeoutTimer = new Timer();
            ConnectTimeoutTimer.OneShot = true;
            ConnectTimeoutTimer.WaitTime = 8f;

            ConnectTimeoutTimer.Timeout += OnConnectTimeout;

            AddChild(ConnectTimeoutTimer);

            Initialize();
        }

        #endregion

        #region ScreenUI implementation

        public override void OnOpened()
        {
            StopWaitingForConnection();

            StatusLabel.Text = "";

            PopulateConnectionRows();
        }

        #endregion

        #region Core - Setup

        private void ResolveChildren()
        {
            SearchInput = GetNode<LineEdit>("MarginContainer/Root/SearchInput");
            ListContainer = GetNode<VBoxContainer>("MarginContainer/Root/ListScroll/ListContainer");
            ServerRowTemplate = GetNode<PanelContainer>("MarginContainer/Root/ListScroll/ListContainer/ServerRowTemplate");
            AddConnectionButton = GetNode<Button>("MarginContainer/Root/ButtonRow/AddConnectionButton");
            BackButton = GetNode<Button>("MarginContainer/Root/ButtonRow/BackButton");
            StatusLabel = GetNode<Label>("MarginContainer/Root/StatusLabel");
        }

        private void Initialize()
        {
            AddConnectionButton.Pressed += OnAddConnectionPressed;
            BackButton.Pressed += OnBackPressed;

            SessionContext.CharacterSelectionRequired += OnCharacterSelectionRequired;

            ServerRowTemplate.Visible = false;

            PopulateConnectionRows();
        }

        #endregion

        #region Core - Lista de conexoes

        public void PopulateConnectionRows()
        {
            var list = ListContainer;
            var template = ServerRowTemplate;

            if (list == null || template == null)
            {
                return;
            }

            foreach (var child in list.GetChildren())
            {
                if (child == template)
                {
                    template.Visible = false;

                    continue;
                }

                child.QueueFree();
            }

            foreach (var connection in SaveStorage.ListConnections())
            {
                list.AddChild(CreateConnectionRow(connection));
            }
        }

        private Control CreateConnectionRow(ServerConnectionData connection)
        {
            var row = (Control)ServerRowTemplate.Duplicate();

            row.Visible = true;

            var name = row.GetNode<Label>("MarginContainer/HBoxContainer/WorldNameLabel");
            var connectButton = row.GetNode<Button>("MarginContainer/HBoxContainer/ConnectButton");
            var deleteButton = row.GetNode<Button>("MarginContainer/HBoxContainer/DeleteButton");

            name.Text = $"{connection.Description}\n{connection.Ip}:{connection.Port}";

            connectButton.Pressed += () => Connect($"{connection.Ip}:{connection.Port}");

            deleteButton.Pressed += () =>
            {
                SaveStorage.DeleteConnection(connection.ConnectionId);

                PopulateConnectionRows();
            };

            return row;
        }

        #endregion

        #region UI - Events

        public void Connect(string target)
        {
            var address = SessionContext.SpawnWorldAndJoin(target);

            if (string.IsNullOrEmpty(address))
            {
                Ui.Get<ErrorModalUI>()?.ShowError(NetworkContext.LastJoinError ?? "Não foi possível conectar.");

                return;
            }

            StatusLabel.Text = $"Conectando em {address}...";

            Multiplayer.ConnectedToServer += OnConnectionSucceeded;
            NetworkContext.ConnectionAttemptFailed += OnConnectionAttemptFailed;

            ConnectTimeoutTimer.Start();
        }

        public void OnBackPressed()
        {
            RouterContext.Close(this);

            var startUi = Ui.Get<StartUI>();

            if (startUi != null)
            {
                RouterContext.Open(startUi);
            }
        }

        public void OnAddConnectionPressed()
        {
            RouterContext.Close(this);
            RouterContext.Open(Ui.Get<AddConnectionUI>());
        }

        #endregion

        #region Managers - Events

        private void OnConnectionSucceeded()
        {
            StopWaitingForConnection();

            RpcId(1, nameof(RequestJoinInfoServerReceive));
        }

        private void OnConnectionAttemptFailed()
        {
            StopWaitingForConnection();

            StatusLabel.Text = "";

            Ui.Get<ErrorModalUI>()?.ShowError("Falha ao conectar. Verifique o IP:Porta, e se a porta está liberada no firewall/roteador de quem está hospedando.");
        }

        private void OnConnectTimeout()
        {
            NetworkContext.Disconnect();

            StopWaitingForConnection();

            StatusLabel.Text = "";

            Ui.Get<ErrorModalUI>()?.ShowError("Tempo esgotado tentando conectar. Verifique o IP:Porta, e se a porta está liberada no firewall/roteador de quem está hospedando.");
        }

        private void StopWaitingForConnection()
        {
            ConnectTimeoutTimer.Stop();

            Multiplayer.ConnectedToServer -= OnConnectionSucceeded;
            NetworkContext.ConnectionAttemptFailed -= OnConnectionAttemptFailed;
        }

        private void OnCharacterSelectionRequired()
        {
            RouterContext.Open(Ui.Get<CharacterSelectUI>());
        }

        #endregion

        #region Core - Rpc - Entrada

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void RequestJoinInfoServerReceive()
        {
            var mode = SessionContext.HostCharacterMode();

            if (mode == null)
            {
                return;
            }

            RpcId(Multiplayer.GetRemoteSenderId(), nameof(JoinInfoReceive), mode.Value);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void JoinInfoReceive(int modeInt)
        {
            SessionContext.ApplyJoinInfo(modeInt);

            if (SessionContext.CharacterMode == WorldCharacterMode.ServerCharacters)
            {
                Ui.Get<CharacterSelectUI>()?.RequestServerList();
            }
        }

        #endregion
    }
}
