using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Save.Types;
using Jogo25D.Session;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class CreateWorldUI : ScreenUI
    {
        #region Node children references

        public LineEdit NameInput { get; private set; }
        public SpinBox AutosaveInput { get; private set; }
        public CheckBox ProceduralCheck { get; private set; }
        public OptionButton ModeOption { get; private set; }
        public Label KeyLabel { get; private set; }
        public LineEdit KeyInput { get; private set; }
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
            AutosaveInput.Value = 5;
            KeyInput.Text = "";
            ProceduralCheck.ButtonPressed = true;
            KeyLabel.Visible = false;
            KeyInput.Visible = false;

            ModeOption.Select(0);
        }

        #endregion

        #region Core - Setup

        private void ResolveChildren()
        {
            NameInput = GetNode<LineEdit>("MarginContainer/Root/NameInput");
            AutosaveInput = GetNode<SpinBox>("MarginContainer/Root/AutosaveInput");
            ProceduralCheck = GetNode<CheckBox>("MarginContainer/Root/ProceduralCheck");
            ModeOption = GetNode<OptionButton>("MarginContainer/Root/ModeOption");
            KeyLabel = GetNode<Label>("MarginContainer/Root/KeyLabel");
            KeyInput = GetNode<LineEdit>("MarginContainer/Root/KeyInput");
            BackButton = GetNode<Button>("MarginContainer/Root/ButtonRow/BackButton");
            CreateButton = GetNode<Button>("MarginContainer/Root/ButtonRow/CreateButton");
        }

        private void Initialize()
        {
            ModeOption.Clear();
            ModeOption.AddItem("Personagem Local", (int)WorldCharacterMode.LocalCharacters);
            ModeOption.AddItem("Personagem de Servidor", (int)WorldCharacterMode.ServerCharacters);

            ModeOption.ItemSelected += OnModeSelected;
            BackButton.Pressed += OnBackPressed;
            CreateButton.Pressed += OnCreatePressed;
        }

        #endregion

        #region UI - Events

        private void OnModeSelected(long index)
        {
            var isServerMode = (WorldCharacterMode)ModeOption.GetItemId((int)index) == WorldCharacterMode.ServerCharacters;

            KeyLabel.Visible = isServerMode;
            KeyInput.Visible = isServerMode;
        }

        private void OnCreatePressed()
        {
            var name = string.IsNullOrWhiteSpace(NameInput.Text) ? "Mundo sem nome" : NameInput.Text.Trim();
            var mode = (WorldCharacterMode)ModeOption.GetSelectedId();
            var key = mode == WorldCharacterMode.ServerCharacters ? KeyInput.Text.Trim() : "";
            var isProcedural = ProceduralCheck.ButtonPressed;
            var world = SaveStorage.CreateWorld(name, (long)GD.Randi(), mode, key, (int)AutosaveInput.Value, isProcedural);

            if (world == null)
            {
                return;
            }

            SessionContext.SetPendingWorld(world);

            RouterContext.Open(Ui.Get<CharacterSelectUI>());
        }

        private void OnBackPressed()
        {
            RouterContext.Open(Ui.Get<WorldSelectUI>());
        }

        #endregion
    }
}
