using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Systems;
using System.Collections.Generic;

namespace Jogo25D.UI
{
    public partial class WorldSelectUI : ScreenUI
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
            PopulateWorldRows();
        }

        #endregion

        #region Core - Setup

        private void Initialize()
        {
            Game.Ui.WorldSelectUI.CreateWorldButton.Node.Pressed += OnCreateWorldPressed;
            Game.Ui.WorldSelectUI.MultiplayerButton.Node.Pressed += OnMultiplayerPressed;
            Game.Ui.WorldSelectUI.BackButton.Node.Pressed += OnBackPressed;
        }

        private Control CreateWorldRow(string title, string subtitle, System.Action onSelect, System.Action onDelete)
        {
            var template = Game.Ui.WorldSelectUI.WorldRowWithDeleteTemplate.Node;

            if (template == null)
            {
                GD.PushError("WorldSelectUI: WorldRowWithDeleteTemplate não encontrado em Game.Ui.WorldSelectUI.ListContainer.Node.");

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
            foreach (var child in Game.Ui.WorldSelectUI.ListContainer.Node.GetChildren())
            {
                if (child.Name == "WorldRowWithDeleteTemplate")
                {
                    ((Control)child).Visible = false;

                    continue;
                }

                child.QueueFree();
            }

            var worlds = Game.Managers.SaveManager.Node?.ListWorlds() ?? new List<WorldSaveData>();

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
                        Game.Managers.SaveManager.Node?.DeleteWorld(world.WorldId);

                        PopulateWorldRows();
                    }
                );

                if (row != null)
                {
                    Game.Ui.WorldSelectUI.ListContainer.Node.AddChild(row);
                }
            }
        }

        #endregion

        #region UI - Events

        public void OnWorldRowPressed(WorldSaveData world)
        {
            Game.Managers.SessionManager.Node.PendingWorld = world;

            Game.Managers.RouterManager.Node.Open(Game.Ui.CharacterSelectUI.Node);
        }

        public void OnCreateWorldPressed()
        {
            Game.Managers.RouterManager.Node.Open(Game.Ui.CreateWorldUI.Node);
        }

        public void OnMultiplayerPressed()
        {
            Game.Managers.RouterManager.Node.Open(Game.Ui.MultiplayerUI.Node);
        }

        public void OnBackPressed()
        {
            Game.Managers.RouterManager.Node.Back();
        }

        #endregion
    }
}
