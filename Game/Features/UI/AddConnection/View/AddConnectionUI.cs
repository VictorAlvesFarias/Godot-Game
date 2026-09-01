using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class AddConnectionUI : ScreenUI
    {
        #region Godot implementation

        public override void _Ready()
        {
            Game.WhenReady(Initialize);
        }

        #endregion

        #region ScreenUI implementation

        public override void OnOpened()
        {
            Game.Ui.AddConnectionUI.DescriptionInput.Node.Text = "";
            Game.Ui.AddConnectionUI.IpInput.Node.Text = "";
            Game.Ui.AddConnectionUI.PortInput.Node.Text = "";
            Game.Ui.AddConnectionUI.StatusLabel.Node.Text = "";
        }

        #endregion

        #region Core - Setup

        private void Initialize()
        {
            Game.Ui.AddConnectionUI.SaveButton.Node.Pressed += OnSavePressed;
            Game.Ui.AddConnectionUI.BackButton.Node.Pressed += OnBackPressed;
        }

        #endregion

        #region UI - Events

        public void OnSavePressed()
        {
            var ip = Game.Ui.AddConnectionUI.IpInput.Node.Text.Trim();
            var portText = Game.Ui.AddConnectionUI.PortInput.Node.Text.Trim();
            var description = Game.Ui.AddConnectionUI.DescriptionInput.Node.Text.Trim();

            if (string.IsNullOrEmpty(ip))
            {
                Game.Ui.AddConnectionUI.StatusLabel.Node.Text = "Informe o IP do servidor.";

                return;
            }

            // Porta em branco cai na padrao, como o NetworkManager ja faz; digitada, precisa ser valida.
            var port = NetworkingConstants.DEFAULT_PORT;

            if (!string.IsNullOrEmpty(portText) && (!int.TryParse(portText, out port) || port < 1 || port > 65535))
            {
                Game.Ui.AddConnectionUI.StatusLabel.Node.Text = "Porta invalida. Use um numero entre 1 e 65535.";

                return;
            }

            SaveStorage.CreateConnection(string.IsNullOrEmpty(description) ? ip : description, ip, port);

            OnBackPressed();
        }

        public void OnBackPressed()
        {
            Game.Managers.RouterManager.Node.Close(this);
            Game.Managers.RouterManager.Node.Open(Game.Ui.MultiplayerUI.Node);
        }

        #endregion
    }
}
