using Godot;
using Jogo25D.Core;

namespace Jogo25D.UI
{
    public partial class ErrorModalUI : ScreenUI
    {
        #region Godot implementation

        public override bool IsOverlay => true;

        public override void _Ready()
        {
            Game.WhenReady(Initialize);
        }

        #endregion

        #region Public API

        public void ShowError(string message)
        {
            Game.Ui.ErrorModalUI.MessageLabel.Node.Text = message;

            Game.Managers.RouterManager.Node.Open(this);
        }

        #endregion

        #region Core - Setup

        private void Initialize()
        {
            Game.Ui.ErrorModalUI.OkButton.Node.Pressed += OnOkPressed;
        }

        #endregion

        #region UI - Events

        private void OnOkPressed()
        {
            Game.Managers.RouterManager.Node.Close(this);
        }

        #endregion
    }
}
