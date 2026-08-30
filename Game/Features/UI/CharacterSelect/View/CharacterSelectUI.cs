using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Systems;
using System.Collections.Generic;

namespace Jogo25D.UI
{
    public partial class CharacterSelectUI : ScreenUI
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
            if (Game.Managers.SessionManager.Node.SelectionContext == CharacterSelectContext.PeerJoinServer)
            {
                ShowServer();

                return;
            }

            ShowLocal();
        }

        public override bool CanOpen()
        {
            var session = Game.Managers.SessionManager.Node;

            return session.SelectionContext != CharacterSelectContext.OwnWorld || session.PendingWorld != null;
        }

        #endregion

        #region Core - Setup

        private void Initialize()
        {
            Game.Ui.CharacterSelectUI.BackButton.Node.Pressed += OnBackPressed;
            Game.Ui.CharacterSelectUI.CreateCharacterButton.Node.Pressed += OnCreateCharacterPressed;
        }

        private void ShowLocal()
        {
            ClearList();

            var characters = Game.Managers.SaveManager.Node?.ListLocalCharacters() ?? new List<CharacterSaveData>();

            foreach (var character in characters)
            {
                var row = CreateCharacterRow(
                    character.Name,
                    () => {
                        SelectLocal(character);
                    },
                    () =>
                    {
                        Game.Managers.SaveManager.Node?.DeleteLocalCharacter(character.CharacterId);

                        if (Game.Managers.SessionManager.Node.PendingCharacter?.CharacterId == character.CharacterId)
                        {
                            Game.Managers.SessionManager.Node.PendingCharacter = null;
                        }

                        ShowLocal();
                    }
                );

                if (row != null)
                {
                    Game.Ui.CharacterSelectUI.ListContainer.Node.AddChild(row);
                }
            }
        }

        private void ShowServer()
        {
            ClearList();

            foreach (var entry in Game.Managers.SessionManager.Node.ServerCharacterSummaries)
            {
                var dict = entry.AsGodotDictionary();
                var characterId = dict["CharacterId"].AsString();
                var name = dict["Name"].AsString();
                var row = CreateCharacterRow(
                    name,
                    () =>
                    {
                        Game.Managers.SessionManager.Node.SelectServerCharacterRequest(characterId);
                        Game.Managers.RouterManager.Node.Close(this);
                    },
                    () =>
                    {
                        Game.Managers.SessionManager.Node.DeleteCharacter(characterId);
                    }
                );

                if (row != null)
                {
                    Game.Ui.CharacterSelectUI.ListContainer.Node.AddChild(row);
                }
            }
        }

        private void SelectLocal(CharacterSaveData character)
        {
            if (character == null)
            {
                return;
            }

            Game.Managers.SessionManager.Node.SelectCharacter(character);
            Game.Managers.RouterManager.Node.Close(this);
        }

        private void ClearList()
        {
            foreach (var child in Game.Ui.CharacterSelectUI.ListContainer.Node.GetChildren())
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
            var template = Game.Ui.CharacterSelectUI.CharacterRowTemplate.Node;

            if (template == null)
            {
                GD.PushError("CharacterSelectUI: CharacterRowTemplate não encontrado em Game.Ui.CharacterSelectUI.ListContainer.Node.");

                return null;
            }

            var row = (HBoxContainer)template.Duplicate();

            row.Visible = true;

            var selectButton = row.GetNode<Button>("SelectButton");

            selectButton.Text = title;
            selectButton.Pressed += onSelect;

            var deleteButton = row.GetNode<Button>("DeleteButton");

            deleteButton.Pressed += onDelete;

            return row;
        }

        #endregion

        #region UI - Events

        public void OnCreateCharacterPressed()
        {
            Game.Managers.RouterManager.Node.Open(Game.Ui.CreateCharacterUI.Node);
        }

        public void OnBackPressed()
        {
            if (Game.Managers.SessionManager.Node.SelectionContext == CharacterSelectContext.OwnWorld)
            {
                Game.Managers.SessionManager.Node.PendingWorld = null;

                Game.Managers.RouterManager.Node.Open(Game.Ui.WorldSelectUI.Node);

                return;
            }

            Game.Managers.NetworkManager.Node.Disconnect();
            Game.Managers.RouterManager.Node.Open(Game.Ui.MultiplayerUI.Node);
        }

        #endregion
    }
}
