using Godot;
using Jogo25D.Constants;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
	public partial class CreateCharacterUI : CanvasLayer
	{
		public bool IsServerMode { get; set; }

		#region Node references

		public LineEdit NameInput { get; set; }
		public Button BackButton { get; set; }
		public Button CreateButton { get; set; }
		public WorldManager NetworkManager { get; set; }
		public SaveManager Saves { get; set; }

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			Layer = 20;
			Visible = false;

			NameInput = GetNode<LineEdit>("MarginContainer/Root/NameInput");
			BackButton = GetNode<Button>("MarginContainer/Root/ButtonRow/BackButton");
			CreateButton = GetNode<Button>("MarginContainer/Root/ButtonRow/CreateButton");
			NetworkManager = GetTree().Root.GetNodeOrNull<WorldManager>(StaticNodePathsConstants.WorldManager);
			Saves = GetTree().Root.GetNodeOrNull<SaveManager>(StaticNodePathsConstants.SaveManager);

			BackButton.Pressed += OnBackPressed;
			CreateButton.Pressed += OnCreatePressed;
		}

		#endregion

		#region Public API

		public void OpenLocal()
		{
			IsServerMode = false;

			ShowScreen();
		}

		public void OpenServer()
		{
			IsServerMode = true;

			ShowScreen();
		}

		public void Close()
		{
			Visible = false;
		}

		#endregion

		#region Core - Actions

		private void ShowScreen()
		{
			Visible = true;

			NameInput.Text = "";
		}

		private void OnCreatePressed()
		{
			var name = string.IsNullOrWhiteSpace(NameInput.Text) ? "Sem nome" : NameInput.Text.Trim();

			Close();

			if (IsServerMode)
			{
				NetworkManager.CreateServerCharacterRequest(name);

				return;
			}

			var character = Saves?.CreateLocalCharacter(name);

			GetTree().Root.GetNodeOrNull<CharacterSelectUI>("Main/Ui/CharacterSelectUI")?.CompleteLocalCreation(character);
		}

		private void OnBackPressed()
		{
			Close();

			var characterSelect = GetTree().Root.GetNodeOrNull<CharacterSelectUI>("Main/Ui/CharacterSelectUI");

			if (IsServerMode)
			{
				characterSelect?.ReopenServer();
			}
			else
			{
				characterSelect?.ReopenLocal();
			}
		}

		#endregion
	}
}
