using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class PauseUI : ScreenUI
    {
        #region Godot implementation

        public override bool IsOverlay => true;

        public override void _Ready()
        {
            Game.WhenReady(Initialize);
        }

        #endregion

        #region Core - Setup

        private void Initialize()
        {
            Game.Ui.PauseUI.ResumeButton.Node.Pressed += OnResumePressed;
            Game.Ui.PauseUI.ExitButton.Node.Pressed += OnExitPressed;
            Game.Ui.PauseUI.HostButton.Node.Pressed += OnHostPressed;
            Game.Ui.PauseUI.PvpButton.Node.Pressed += OnPvpPressed;
            Game.Ui.PauseUI.MenuButton.Node.Pressed += OnMenuPressed;

            foreach (var botao in new[]
            {
                Game.Ui.PauseUI.ResumeButton.Node,
                Game.Ui.PauseUI.HostButton.Node,
                Game.Ui.PauseUI.PvpButton.Node,
                Game.Ui.PauseUI.MenuButton.Node,
                Game.Ui.PauseUI.ExitButton.Node
            })
            {
                LigarMarcador(botao);
            }
        }

        // O triangulo vem da cena, dentro do proprio botao. Aqui so nasce escondido e passa a
        // seguir o mouse, como o marcador do slot selecionado na hotbar.
        private void LigarMarcador(Button botao)
        {
            var marcador = botao?.GetNodeOrNull<Control>("HoverMarker");

            if (marcador == null)
            {
                return;
            }

            marcador.Visible = false;

            botao.MouseEntered += delegate { marcador.Visible = true; };
            botao.MouseExited += delegate { marcador.Visible = false; };
        }

        public override void _Input(InputEvent @event)
        {
            if (@event.IsActionPressed("pause") && !@event.IsEcho())
            {
                var input = Game.Managers.WorldManager.Node?.GetLocalPlayer()?.Input;

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
                Game.Managers.RouterManager.Node.Close(this);
            }
            else
            {
                Game.Managers.RouterManager.Node.Open(this);
            }

            if (!IsMultiplayerActive())
            {
                GetTree().Paused = Visible;
            }

            var input = Game.Managers.WorldManager.Node?.GetLocalPlayer()?.Input;

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
            Game.Managers.RouterManager.Node.Close(this);
            GetTree().Paused = false;

            Game.Managers.WorldManager.Node?.GetLocalPlayer()?.Input?.RemoveBlocker("pause");
        }

        public void OnMenuPressed()
        {
            Game.Managers.RouterManager.Node.Close(this);
            GetTree().Paused = false;

            Game.Managers.WorldManager.Node?.GetLocalPlayer()?.Input?.RemoveBlocker("pause");
            Game.Managers.SessionManager.Node.LeaveWorld();
        }

        #endregion

        #region Core - Network

        public void OnHostPressed()
        {
            if (Game.Managers.WorldManager.Node == null)
            {
                return;
            }

            if (Game.Managers.NetworkManager.Node.IsConnected())
            {
                Game.Managers.NetworkManager.Node.Disconnect();
            }
            else
            {
                Game.Ui.HostModalUI.Node.Abrir();

                return;
            }

            UpdateNetworkStatus();
        }

        public void UpdateNetworkStatus()
        {
            if (Game.Managers.WorldManager.Node == null)
            {
                return;
            }

            bool connected = Game.Managers.NetworkManager.Node.IsConnected();
            bool isServer = Multiplayer.IsServer();

            Game.Ui.PauseUI.HostButton.Node.Visible = !connected || isServer;

            Game.Ui.PauseUI.HostButton.Node.Text = connected && isServer
                ? $"Hosting {Game.Managers.NetworkManager.Node.CurrentPort}"
                : "Host";
        }

        #endregion

        #region Core - Pvp

        public void OnPvpPressed()
        {
            var localPlayer = Game.Managers.WorldManager.Node?.GetLocalPlayer();

            if (localPlayer == null)
            {
                return;
            }

            localPlayer.SetPvpEnabledRequest(!localPlayer.PvpEnabled);

            UpdatePvpStatus();
        }

        public void UpdatePvpStatus()
        {
            var localPlayer = Game.Managers.WorldManager.Node?.GetLocalPlayer();

            Game.Ui.PauseUI.PvpButton.Node.Text = localPlayer != null && localPlayer.PvpEnabled ? "PvP" : "PvE";
        }

        #endregion
    }
}
