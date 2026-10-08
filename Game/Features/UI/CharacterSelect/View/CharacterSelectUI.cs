using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Network;
using Jogo25D.Save.Resources;
using Jogo25D.Save.Types;
using Jogo25D.Session;
using Jogo25D.Systems;
using Jogo25D.Utils.GodotDictionaryParser;
using System.Collections.Generic;

namespace Jogo25D.UI
{
    public partial class CharacterSelectUI : ScreenUI
    {
        #region Node children references

        public LineEdit SearchInput { get; private set; }
        public VBoxContainer ListContainer { get; private set; }
        public Button BackButton { get; private set; }
        public Button CreateCharacterButton { get; private set; }
        public PanelContainer CharacterRowTemplate { get; private set; }

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
            if (SelectionContext() == CharacterSelectContext.PeerJoinServer)
            {
                ShowServer();

                return;
            }

            ShowLocal();
        }

        public override bool CanOpen()
        {
            return SelectionContext() != CharacterSelectContext.OwnWorld || SessionContext.PendingWorld != null;
        }

        #endregion

        #region Core - Variante da tela

        private CharacterSelectContext SelectionContext()
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
            {
                return CharacterSelectContext.OwnWorld;
            }

            return SessionContext.CharacterMode == WorldCharacterMode.LocalCharacters
                ? CharacterSelectContext.PeerJoinLocal
                : CharacterSelectContext.PeerJoinServer;
        }

        #endregion

        #region Core - Setup

        private void ResolveChildren()
        {
            SearchInput = GetNode<LineEdit>("MarginContainer/Root/SearchInput");
            ListContainer = GetNode<VBoxContainer>("MarginContainer/Root/ListScroll/ListContainer");
            BackButton = GetNode<Button>("MarginContainer/Root/ButtonRow/BackButton");
            CreateCharacterButton = GetNode<Button>("MarginContainer/Root/ButtonRow/CreateCharacterButton");
            CharacterRowTemplate = GetNode<PanelContainer>("MarginContainer/Root/ListScroll/ListContainer/CharacterRowTemplate");
        }

        private void Initialize()
        {
            BackButton.Pressed += OnBackPressed;
            CreateCharacterButton.Pressed += OnCreateCharacterPressed;
        }

        private void ShowLocal()
        {
            ClearList();

            var characters = SaveStorage.ListLocalCharacters() ?? new List<CharacterSaveData>();

            foreach (var character in characters)
            {
                var row = CreateCharacterRow(
                    character.Name,
                    () => {
                        SelectLocal(character);
                    },
                    () =>
                    {
                        SaveStorage.DeleteLocalCharacter(character.CharacterId);

                        SessionContext.ForgetCharacter(character.CharacterId);

                        ShowLocal();
                    }
                );

                if (row != null)
                {
                    ListContainer.AddChild(row);
                }
            }
        }

        private void ShowServer()
        {
            ClearList();

            foreach (var entry in SessionContext.ServerCharacterSummaries)
            {
                var dict = entry.AsGodotDictionary();
                var characterId = dict["CharacterId"].AsString();
                var name = dict["Name"].AsString();
                var row = CreateCharacterRow(
                    name,
                    () =>
                    {
                        RpcId(1, nameof(SelectServerCharacterServerReceive), characterId);

                        RouterContext.Close(this);
                    },
                    () =>
                    {
                        RpcId(1, nameof(DeleteServerCharacterServerReceive), characterId);
                    }
                );

                if (row != null)
                {
                    ListContainer.AddChild(row);
                }
            }
        }

        private void SelectLocal(CharacterSaveData character)
        {
            UseCharacter(character);

            RouterContext.Close(this);
        }

        public void UseCharacter(CharacterSaveData character)
        {
            if (character == null)
            {
                return;
            }

            if (!NetworkContext.IsConnected())
            {
                SessionContext.EnterWorldWith(character);

                return;
            }

            SessionContext.PendingCharacter = character;

            RpcId(1, nameof(SubmitLocalCharacterServerReceive), LocalProfileId(), GodotDictionaryParser.ToDictionary(character));
        }

        public void RequestServerList()
        {
            RpcId(1, nameof(RequestServerCharacterListServerReceive), LocalProfileId());
        }

        private static string LocalProfileId()
        {
            return SaveStorage.GetOrCreateLocalProfile()?.ProfileId ?? "";
        }

        private void ClearList()
        {
            foreach (var child in ListContainer.GetChildren())
            {
                if (child.Name == "CharacterRowTemplate")
                {
                    ((Control)child).Visible = false;

                    continue;
                }

                child.QueueFree();
            }
        }

        private Control CreateCharacterRow(string title, System.Action onSelect, System.Action onDelete)
        {
            var template = CharacterRowTemplate;

            if (template == null)
            {
                GD.PushError("CharacterSelectUI: CharacterRowTemplate não encontrado em ListContainer.");

                return null;
            }

            var row = (Control)template.Duplicate();

            row.Visible = true;

            row.GetNode<Label>("MarginContainer/HBoxContainer/NameLabel").Text = title;

            row.GetNode<Button>("MarginContainer/HBoxContainer/SelectButton").Pressed += onSelect;
            row.GetNode<Button>("MarginContainer/HBoxContainer/DeleteButton").Pressed += onDelete;

            return row;
        }

        #endregion

        #region UI - Events

        public void OnCreateCharacterPressed()
        {
            RouterContext.Open(Ui.Get<CreateCharacterUI>());
        }

        public void OnBackPressed()
        {
            if (SelectionContext() == CharacterSelectContext.OwnWorld)
            {
                SessionContext.SetPendingWorld(null);

                RouterContext.Open(Ui.Get<WorldSelectUI>());

                return;
            }

            NetworkContext.Disconnect();
            RouterContext.Open(Ui.Get<MultiplayerUI>());
        }

        #endregion

        #region Core - Rpc - Personagens do servidor

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SubmitLocalCharacterServerReceive(string profileId, Godot.Collections.Dictionary characterDict)
        {
            SessionContext.ApplyLocalCharacterSubmit(Multiplayer.GetRemoteSenderId(), profileId, characterDict);
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void RequestServerCharacterListServerReceive(string profileId)
        {
            var senderId = Multiplayer.GetRemoteSenderId();

            if (!SessionContext.AcceptServerProfile(senderId, profileId))
            {
                return;
            }

            RpcId(senderId, nameof(ServerCharacterListReceive), SessionContext.ServerCharacterSummariesFor(senderId));
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ServerCharacterListReceive(Godot.Collections.Array summaries)
        {
            SessionContext.ApplyServerCharacterList(summaries);
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SelectServerCharacterServerReceive(string characterId)
        {
            SessionContext.ApplyServerCharacterSelect(Multiplayer.GetRemoteSenderId(), characterId);
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void DeleteServerCharacterServerReceive(string characterId)
        {
            var senderId = Multiplayer.GetRemoteSenderId();

            if (!SessionContext.DeleteServerCharacter(senderId, characterId))
            {
                return;
            }

            RpcId(senderId, nameof(ServerCharacterListReceive), SessionContext.ServerCharacterSummariesFor(senderId));
        }

        #endregion
    }
}
