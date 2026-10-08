using Godot;
using Jogo25D.Core;

namespace Jogo25D.UI
{
    public partial class ErrorModalUI : ScreenUI
    {
        #region Node children references

        public Panel Background { get; private set; }
        public Label MessageLabel { get; private set; }
        public Button OkButton { get; private set; }

        #endregion

        #region Godot implementation

        public override bool IsOverlay => true;

        public override void _Ready()
        {
            ResolveChildren();
            Initialize();
        }

        #endregion

        #region Public API

        public void ShowError(string message)
        {
            MessageLabel.Text = message;

            RouterContext.Open(this);
        }

        #endregion

        #region Core - Setup

        private void ResolveChildren()
        {
            Background = GetNode<Panel>("Background");
            MessageLabel = GetNode<Label>("Background/CenterContainer/Panel/MarginContainer/Root/MessageScroll/MessageLabel");
            OkButton = GetNode<Button>("Background/CenterContainer/Panel/MarginContainer/Root/OkButton");
        }

        private void Initialize()
        {
            OkButton.Pressed += OnOkPressed;
        }

        #endregion

        #region UI - Events

        private void OnOkPressed()
        {
            RouterContext.Close(this);
        }

        #endregion
    }
}
