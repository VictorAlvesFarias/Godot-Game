using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Save.Types;
using Jogo25D.Session;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class CreateCharacterUI : ScreenUI
    {
        #region Node children references

        public LineEdit NameInput { get; private set; }
        public Button BackButton { get; private set; }
        public Button CreateButton { get; private set; }

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
            NameInput.Text = "";
        }

        #endregion

        #region Core - Setup

        private void ResolveChildren()
        {
            NameInput = GetNode<LineEdit>("MarginContainer/Root/NameInput");
            BackButton = GetNode<Button>("MarginContainer/Root/ButtonRow/BackButton");
            CreateButton = GetNode<Button>("MarginContainer/Root/ButtonRow/CreateButton");
        }

        private void Initialize()
        {
            BackButton.Pressed += OnBackPressed;
            CreateButton.Pressed += OnCreatePressed;
        }

        #endregion

        #region UI - Events

        private void OnCreatePressed()
        {
            var name = string.IsNullOrWhiteSpace(NameInput.Text) ? "Sem nome" : NameInput.Text.Trim();

            if (SessionContext.CharacterMode == WorldCharacterMode.ServerCharacters)
            {
                RpcId(1, nameof(CreateServerCharacterServerReceive), name);

                return;
            }

            Ui.Get<CharacterSelectUI>()?.UseCharacter(SaveStorage.CreateLocalCharacter(name));
        }

        private void OnBackPressed()
        {
            RouterContext.Open(Ui.Get<CharacterSelectUI>());
        }

        #endregion

        #region Core - Rpc - Criacao no servidor

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void CreateServerCharacterServerReceive(string name)
        {
            SessionContext.ApplyServerCharacterCreate(Multiplayer.GetRemoteSenderId(), name);
        }

        #endregion
    }
}
