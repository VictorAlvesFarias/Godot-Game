using Godot;
using Jogo25D.Core;

namespace Jogo25D.UI
{
    public partial class HostModalUI : ScreenUI
    {
        #region Godot implementation

        public override bool IsOverlay => true;

        public override void _Ready()
        {
            Game.WhenReady(Initialize);
        }

        public override void _Input(InputEvent @event)
        {
            if (Visible && @event.IsActionPressed("ui_cancel") && !@event.IsEcho())
            {
                Fechar();

                GetViewport().SetInputAsHandled();
            }
        }

        #endregion

        #region Public API

        public void Abrir()
        {
            var campo = Game.Ui.HostModalUI.PortInput.Node;

            campo.Text = "";

            Game.Managers.RouterManager.Node.Open(this);

            campo.GrabFocus();
        }

        #endregion

        #region Core - Setup

        private void Initialize()
        {
            Game.Ui.HostModalUI.ConfirmButton.Node.Pressed += OnConfirmPressed;
            Game.Ui.HostModalUI.CancelButton.Node.Pressed += OnCancelPressed;
            Game.Ui.HostModalUI.PortInput.Node.TextSubmitted += OnPortSubmitted;
        }

        #endregion

        #region UI - Events

        private void OnConfirmPressed()
        {
            // O NetworkManager ja resolve porta vazia ou invalida para a padrao.
            Game.Managers.NetworkManager.Node?.CreateServer(Game.Ui.HostModalUI.PortInput.Node.Text.Trim());

            Fechar();
        }

        private void OnPortSubmitted(string _)
        {
            OnConfirmPressed();
        }

        private void OnCancelPressed()
        {
            Fechar();
        }

        private void Fechar()
        {
            Game.Managers.RouterManager.Node.Close(this);
        }

        #endregion
    }
}
