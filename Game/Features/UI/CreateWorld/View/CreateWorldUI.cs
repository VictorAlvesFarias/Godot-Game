using Godot;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
	public partial class CreateWorldUI : CanvasLayer
	{
		#region Node references

		public LineEdit NameInput { get; set; }
		public SpinBox AutosaveInput { get; set; }
		public OptionButton ModeOption { get; set; }
		public Label KeyLabel { get; set; }
		public LineEdit KeyInput { get; set; }
		public Button BackButton { get; set; }
		public Button CreateButton { get; set; }
		public WorldManager NetworkManager { get; set; }
		public Jogo25D.Systems.SaveManager Saves { get; set; }

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			Layer = 20;
			Visible = false;

			NameInput = GetNode<LineEdit>("MarginContainer/Root/NameInput");
			AutosaveInput = GetNode<SpinBox>("MarginContainer/Root/AutosaveInput");
			ModeOption = GetNode<OptionButton>("MarginContainer/Root/ModeOption");
			KeyLabel = GetNode<Label>("MarginContainer/Root/KeyLabel");
			KeyInput = GetNode<LineEdit>("MarginContainer/Root/KeyInput");
			BackButton = GetNode<Button>("MarginContainer/Root/ButtonRow/BackButton");
			CreateButton = GetNode<Button>("MarginContainer/Root/ButtonRow/CreateButton");
			NetworkManager = GetTree().Root.GetNode<WorldManager>(WorldManager.DEFAULT_NODE_PATH);
			Saves = GetTree().Root.GetNodeOrNull<Jogo25D.Systems.SaveManager>(Jogo25D.Systems.SaveManager.DEFAULT_NODE_PATH);

			ModeOption.Clear();
			ModeOption.AddItem("Personagem Local", (int)WorldCharacterMode.LocalCharacters);
			ModeOption.AddItem("Personagem de Servidor", (int)WorldCharacterMode.ServerCharacters);
			ModeOption.ItemSelected += OnModeSelected;

			BackButton.Pressed += OnBackPressed;
			CreateButton.Pressed += OnCreatePressed;
		}

		#endregion

		#region Public API

		public void Open()
		{
			Visible = true;

			NameInput.Text = "";
			AutosaveInput.Value = 5;
			KeyInput.Text = "";

			ModeOption.Select(0);

			KeyLabel.Visible = false;
			KeyInput.Visible = false;
		}

		public void Close()
		{
			Visible = false;
		}

		#endregion

		#region Core - Actions

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

			var world = Saves?.CreateWorld(name, (long)GD.Randi(), mode, key, (int)AutosaveInput.Value);

			if (world == null)
			{
				return;
			}

			// So guarda o mundo escolhido - quem entra de verdade e
			// CharacterSelectUI, depois do personagem escolhido (mesmo
			// fluxo "mundo -> personagem" de WorldSelectUI).
			NetworkManager.PendingWorld = world;
			NetworkManager.PendingWorldIsDefault = false;

			Close();

			GetTree().Root.GetNodeOrNull<CharacterSelectUI>("Main/Ui/CharacterSelectUI")?.OpenForOwnWorld();
		}

		private void OnBackPressed()
		{
			Close();

			GetTree().Root.GetNodeOrNull<WorldSelectUI>("Main/Ui/WorldSelectUI")?.Open();
		}

		#endregion
	}
}
