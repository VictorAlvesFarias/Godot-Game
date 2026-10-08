using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Network;
using Jogo25D.Session;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class PauseUI : ScreenUI
    {
        #region Node children references

        public Button ResumeButton { get; private set; }
        public Button ExitButton { get; private set; }
        public Button HostButton { get; private set; }
        public Button PvpButton { get; private set; }
        public Button MenuButton { get; private set; }

        #endregion

        #region Godot implementation

        public override bool IsOverlay => true;

        public override void _Ready()
        {
            ResolveChildren();
            Initialize();
        }

        #endregion

        #region Core - Setup

        private void ResolveChildren()
        {
            ResumeButton = GetNode<Button>("MarginContainer/Root/MenuColumn/ResumeButton");
            ExitButton = GetNode<Button>("MarginContainer/Root/MenuColumn/ExitButton");
            HostButton = GetNode<Button>("MarginContainer/Root/MenuColumn/HostButton");
            PvpButton = GetNode<Button>("MarginContainer/Root/MenuColumn/PvpButton");
            MenuButton = GetNode<Button>("MarginContainer/Root/MenuColumn/MenuButton");
        }

        private void Initialize()
        {
            ResumeButton.Pressed += OnResumePressed;
            ExitButton.Pressed += OnExitPressed;
            HostButton.Pressed += OnHostPressed;
            PvpButton.Pressed += OnPvpPressed;
            MenuButton.Pressed += OnMenuPressed;

            foreach (var button in new[]
            {
                ResumeButton,
                HostButton,
                PvpButton,
                MenuButton,
                ExitButton
            })
            {
                BindHoverMarker(button);
            }
        }

        private void BindHoverMarker(Button button)
        {
            var marker = button?.GetNodeOrNull<Control>("HoverMarker");

            if (marker == null)
            {
                return;
            }

            marker.Visible = false;

            button.MouseEntered += () => marker.Visible = true;
            button.MouseExited += () => marker.Visible = false;
        }

        public override void _Input(InputEvent @event)
        {
            if (@event.IsActionPressed("pause") && !@event.IsEcho())
            {
                var input = Players.GetLocal()?.Input;

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
            if (Visible)
            {
                RouterContext.Close(this);
            }
            else
            {
                RouterContext.Open(this);
            }

            if (!IsMultiplayerActive())
            {
                GetTree().Paused = Visible;
            }

            var input = Players.GetLocal()?.Input;

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
            RouterContext.Close(this);
            GetTree().Paused = false;

            Players.GetLocal()?.Input?.RemoveBlocker("pause");
        }

        public void OnMenuPressed()
        {
            RouterContext.Close(this);
            GetTree().Paused = false;

            Players.GetLocal()?.Input?.RemoveBlocker("pause");
            SessionContext.LeaveWorld();
        }

        #endregion

        #region Core - Network

        public void OnHostPressed()
        {
            if (NetworkContext.IsConnected())
            {
                NetworkContext.Disconnect();
            }
            else
            {
                Ui.Get<HostModalUI>().Abrir();

                return;
            }

            UpdateNetworkStatus();
        }

        public void UpdateNetworkStatus()
        {
            bool connected = NetworkContext.IsConnected();
            bool isServer = Multiplayer.IsServer();

            HostButton.Visible = !connected || isServer;

            HostButton.Text = connected && isServer
                ? $"Hosting {NetworkContext.CurrentPort}"
                : "Host";
        }

        #endregion

        #region Core - Pvp

        public void OnPvpPressed()
        {
            var localPlayer = Players.GetLocal();

            if (localPlayer == null)
            {
                return;
            }

            localPlayer.SetPvpEnabledRequest(!localPlayer.PvpEnabled);

            UpdatePvpStatus();
        }

        public void UpdatePvpStatus()
        {
            var localPlayer = Players.GetLocal();

            PvpButton.Text = localPlayer != null && localPlayer.PvpEnabled ? "PvP" : "PvE";
        }

        #endregion
    }
}
