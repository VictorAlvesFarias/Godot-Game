using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class AddConnectionUI : ScreenUI
    {
        #region Node children references

        public LineEdit DescriptionInput { get; private set; }
        public LineEdit IpInput { get; private set; }
        public LineEdit PortInput { get; private set; }
        public Label StatusLabel { get; private set; }
        public Button SaveButton { get; private set; }
        public Button BackButton { get; private set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            ResolveChildren();
            Initialize();
        }

        #endregion

        #region ScreenUI implementation

        public override void OnOpened()
        {
            DescriptionInput.Text = "";
            IpInput.Text = "";
            PortInput.Text = "";
            StatusLabel.Text = "";
        }

        #endregion

        #region Core - Setup

        private void ResolveChildren()
        {
            DescriptionInput = GetNode<LineEdit>("MarginContainer/Root/DescriptionInput");
            IpInput = GetNode<LineEdit>("MarginContainer/Root/IpInput");
            PortInput = GetNode<LineEdit>("MarginContainer/Root/PortInput");
            StatusLabel = GetNode<Label>("MarginContainer/Root/StatusLabel");
            SaveButton = GetNode<Button>("MarginContainer/Root/ButtonRow/SaveButton");
            BackButton = GetNode<Button>("MarginContainer/Root/ButtonRow/BackButton");
        }

        private void Initialize()
        {
            SaveButton.Pressed += OnSavePressed;
            BackButton.Pressed += OnBackPressed;
        }

        #endregion

        #region UI - Events

        public void OnSavePressed()
        {
            var ip = IpInput.Text.Trim();
            var portText = PortInput.Text.Trim();
            var description = DescriptionInput.Text.Trim();

            if (string.IsNullOrEmpty(ip))
            {
                StatusLabel.Text = "Informe o IP do servidor.";

                return;
            }

            var port = NetworkingConstants.DEFAULT_PORT;

            if (!string.IsNullOrEmpty(portText) && (!int.TryParse(portText, out port) || port < 1 || port > 65535))
            {
                StatusLabel.Text = "Porta invalida. Use um numero entre 1 e 65535.";

                return;
            }

            SaveStorage.CreateConnection(string.IsNullOrEmpty(description) ? ip : description, ip, port);

            OnBackPressed();
        }

        public void OnBackPressed()
        {
            RouterContext.Close(this);
            RouterContext.Open(Ui.Get<MultiplayerUI>());
        }

        #endregion
    }
}
