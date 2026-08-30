using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class CreateWorldUI : ScreenUI
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
            Game.Ui.CreateWorldUI.NameInput.Node.Text = "";
            Game.Ui.CreateWorldUI.AutosaveInput.Node.Value = 5;
            Game.Ui.CreateWorldUI.KeyInput.Node.Text = "";
            Game.Ui.CreateWorldUI.ProceduralCheck.Node.ButtonPressed = true;
            Game.Ui.CreateWorldUI.KeyLabel.Node.Visible = false;
            Game.Ui.CreateWorldUI.KeyInput.Node.Visible = false;

            Game.Ui.CreateWorldUI.ModeOption.Node.Select(0);
        }

        #endregion

        #region Core - Setup

        private void Initialize()
        {
            Game.Ui.CreateWorldUI.ModeOption.Node.Clear();
            Game.Ui.CreateWorldUI.ModeOption.Node.AddItem("Personagem Local", (int)WorldCharacterMode.LocalCharacters);
            Game.Ui.CreateWorldUI.ModeOption.Node.AddItem("Personagem de Servidor", (int)WorldCharacterMode.ServerCharacters);

            Game.Ui.CreateWorldUI.ModeOption.Node.ItemSelected += OnModeSelected;
            Game.Ui.CreateWorldUI.BackButton.Node.Pressed += OnBackPressed;
            Game.Ui.CreateWorldUI.CreateButton.Node.Pressed += OnCreatePressed;
        }

        #endregion

        #region UI - Events

        private void OnModeSelected(long index)
        {
            var isServerMode = (WorldCharacterMode)Game.Ui.CreateWorldUI.ModeOption.Node.GetItemId((int)index) == WorldCharacterMode.ServerCharacters;

            Game.Ui.CreateWorldUI.KeyLabel.Node.Visible = isServerMode;
            Game.Ui.CreateWorldUI.KeyInput.Node.Visible = isServerMode;
        }

        private void OnCreatePressed()
        {
            var name = string.IsNullOrWhiteSpace(Game.Ui.CreateWorldUI.NameInput.Node.Text) ? "Mundo sem nome" : Game.Ui.CreateWorldUI.NameInput.Node.Text.Trim();
            var mode = (WorldCharacterMode)Game.Ui.CreateWorldUI.ModeOption.Node.GetSelectedId();
            var key = mode == WorldCharacterMode.ServerCharacters ? Game.Ui.CreateWorldUI.KeyInput.Node.Text.Trim() : "";
            var isProcedural = Game.Ui.CreateWorldUI.ProceduralCheck.Node.ButtonPressed;
            var world = Game.Managers.SaveManager.Node?.CreateWorld(name, (long)GD.Randi(), mode, key, (int)Game.Ui.CreateWorldUI.AutosaveInput.Node.Value, isProcedural);

            if (world == null)
            {
                return;
            }

            Game.Managers.SessionManager.Node.PendingWorld = world;

            Game.Managers.RouterManager.Node.Open(Game.Ui.CharacterSelectUI.Node);
        }

        private void OnBackPressed()
        {
            Game.Managers.RouterManager.Node.Open(Game.Ui.WorldSelectUI.Node);
        }

        #endregion
    }
}
