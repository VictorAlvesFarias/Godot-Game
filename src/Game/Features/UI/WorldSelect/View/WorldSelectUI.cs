using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Save.Resources;
using Jogo25D.Save.Types;
using Jogo25D.Session;
using Jogo25D.Systems;
using System.Collections.Generic;

namespace Jogo25D.UI
{
    public partial class WorldSelectUI : ScreenUI
    {
        #region Node children references

        public LineEdit SearchInput { get; private set; }
        public VBoxContainer ListContainer { get; private set; }
        public Button CreateWorldButton { get; private set; }
        public Button MultiplayerButton { get; private set; }
        public Button BackButton { get; private set; }
        public PanelContainer WorldRowWithDeleteTemplate { get; private set; }

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
            PopulateWorldRows();
        }

        #endregion

        #region Core - Setup

        private void ResolveChildren()
        {
            SearchInput = GetNode<LineEdit>("MarginContainer/Root/SearchInput");
            ListContainer = GetNode<VBoxContainer>("MarginContainer/Root/ListScroll/ListContainer");
            CreateWorldButton = GetNode<Button>("MarginContainer/Root/ButtonRow/CreateWorldButton");
            MultiplayerButton = GetNode<Button>("MarginContainer/Root/ButtonRow/MultiplayerButton");
            BackButton = GetNode<Button>("MarginContainer/Root/ButtonRow/BackButton");
            WorldRowWithDeleteTemplate = GetNode<PanelContainer>("MarginContainer/Root/ListScroll/ListContainer/WorldRowWithDeleteTemplate");
        }

        private void Initialize()
        {
            CreateWorldButton.Pressed += OnCreateWorldPressed;
            MultiplayerButton.Pressed += OnMultiplayerPressed;
            BackButton.Pressed += OnBackPressed;
        }

        private Control CreateWorldRow(string title, string subtitle, System.Action onSelect, System.Action onDelete)
        {
            var template = WorldRowWithDeleteTemplate;

            if (template == null)
            {
                GD.PushError("WorldSelectUI: WorldRowWithDeleteTemplate não encontrado em ListContainer.");

                return null;
            }

            var wrapper = (Control)template.Duplicate();

            wrapper.Visible = true;

            var nameLabel = wrapper.GetNode<Label>("MarginContainer/HBoxContainer/NameLabel");

            nameLabel.Text = subtitle == null ? title : $"{title}\n{subtitle}";

            wrapper.GetNode<Button>("MarginContainer/HBoxContainer/SelectButton").Pressed += onSelect;
            wrapper.GetNode<Button>("MarginContainer/HBoxContainer/DeleteButton").Pressed += onDelete;

            return wrapper;
        }

        private void PopulateWorldRows()
        {
            foreach (var child in ListContainer.GetChildren())
            {
                if (child.Name == "WorldRowWithDeleteTemplate")
                {
                    ((Control)child).Visible = false;

                    continue;
                }

                child.QueueFree();
            }

            var worlds = SaveStorage.ListWorlds() ?? new List<WorldSaveData>();

            foreach (var world in worlds)
            {
                var modeLabel = $"Servidor (chave: {world.MultiplayerKey})";

                if (world.CharacterMode != WorldCharacterMode.ServerCharacters)
                {
                    modeLabel = "Local";
                }

                var row = CreateWorldRow(
                    world.Name,
                    $"{modeLabel} · autosave a cada {world.AutosaveIntervalMinutes} min",
                    () =>{
                        OnWorldRowPressed(world);
                    },
                    () =>
                    {
                        SaveStorage.DeleteWorld(world.WorldId);

                        PopulateWorldRows();
                    }
                );

                if (row != null)
                {
                    ListContainer.AddChild(row);
                }
            }
        }

        #endregion

        #region UI - Events

        public void OnWorldRowPressed(WorldSaveData world)
        {
            SessionContext.SetPendingWorld(world);

            RouterContext.Open(Ui.Get<CharacterSelectUI>());
        }

        public void OnCreateWorldPressed()
        {
            RouterContext.Open(Ui.Get<CreateWorldUI>());
        }

        public void OnMultiplayerPressed()
        {
            RouterContext.Open(Ui.Get<MultiplayerUI>());
        }

        public void OnBackPressed()
        {
            RouterContext.Back();
        }

        #endregion
    }
}
