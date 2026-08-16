using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
	public partial class CreateCharacterUI : CanvasLayer
	{
		public bool IsServerMode { get; set; }

		#region Godot implementation

		public override void _Ready()
		{
			Layer = 20;
			Visible = false;

			Game.WhenReady(Initialize);
		}

		#endregion

		#region Core - Setup

		private void Initialize()
		{
			Game.Ui.CreateCharacterUI.BackButton.Node.Pressed += OnBackPressed;
			Game.Ui.CreateCharacterUI.CreateButton.Node.Pressed += OnCreatePressed;
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

			Game.Ui.CreateCharacterUI.NameInput.Node.Text = "";
		}

		private void OnCreatePressed()
		{
			var name = string.IsNullOrWhiteSpace(Game.Ui.CreateCharacterUI.NameInput.Node.Text) ? "Sem nome" : Game.Ui.CreateCharacterUI.NameInput.Node.Text.Trim();

			Close();

			if (IsServerMode)
			{
				Game.Managers.WorldManager.Node.CreateServerCharacterRequest(name);

				return;
			}

			var character = Game.Managers.SaveManager.Node?.CreateLocalCharacter(name);

			Game.Ui.CharacterSelectUI.Node?.CompleteLocalCreation(character);
		}

		private void OnBackPressed()
		{
			Close();

			var characterSelect = Game.Ui.CharacterSelectUI.Node;

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
