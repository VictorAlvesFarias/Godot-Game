using Godot;
using Jogo25D.Core;
using Jogo25D.Network;
using Jogo25D.Session;

namespace Jogo25D.UI
{
    public partial class HostModalUI : ScreenUI
    {
        #region Node children references

        public LineEdit PortInput { get; private set; }
        public Button ConfirmButton { get; private set; }
        public Button CancelButton { get; private set; }

        #endregion

        #region Godot implementation

        public override bool IsOverlay => true;

        public override void _Ready()
        {
            ResolveChildren();
            Initialize();
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
            var field = PortInput;

            field.Text = "";

            RouterContext.Open(this);

            field.GrabFocus();
        }

        #endregion

        #region Core - Setup

        private void ResolveChildren()
        {
            PortInput = GetNode<LineEdit>("Background/CenterContainer/Panel/MarginContainer/Root/PortInput");
            ConfirmButton = GetNode<Button>("Background/CenterContainer/Panel/MarginContainer/Root/Buttons/ConfirmButton");
            CancelButton = GetNode<Button>("Background/CenterContainer/Panel/MarginContainer/Root/Buttons/CancelButton");
        }

        private void Initialize()
        {
            ConfirmButton.Pressed += OnConfirmPressed;
            CancelButton.Pressed += OnCancelPressed;
            PortInput.TextSubmitted += OnPortSubmitted;
        }

        #endregion

        #region UI - Events

        private void OnConfirmPressed()
        {
            SessionContext.HostWorld(PortInput.Text.Trim());

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
            RouterContext.Close(this);
        }

        #endregion
    }
}
