using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
	public partial class CreateWorldUI : CanvasLayer
	{
		#region Node references


		#endregion

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
			Game.Ui.CreateWorldUI.ModeOption.Node.Clear();
			Game.Ui.CreateWorldUI.ModeOption.Node.AddItem("Personagem Local", (int)WorldCharacterMode.LocalCharacters);
			Game.Ui.CreateWorldUI.ModeOption.Node.AddItem("Personagem de Servidor", (int)WorldCharacterMode.ServerCharacters);
			Game.Ui.CreateWorldUI.ModeOption.Node.ItemSelected += OnModeSelected;

			Game.Ui.CreateWorldUI.BackButton.Node.Pressed += OnBackPressed;
			Game.Ui.CreateWorldUI.CreateButton.Node.Pressed += OnCreatePressed;
		}

		#endregion

		#region Core - Tela

		public void Open()
		{
			Visible = true;

			Game.Ui.CreateWorldUI.NameInput.Node.Text = "";
			Game.Ui.CreateWorldUI.AutosaveInput.Node.Value = 5;
			Game.Ui.CreateWorldUI.KeyInput.Node.Text = "";

			Game.Ui.CreateWorldUI.ModeOption.Node.Select(0);

			Game.Ui.CreateWorldUI.KeyLabel.Node.Visible = false;
			Game.Ui.CreateWorldUI.KeyInput.Node.Visible = false;
		}

		public void Close()
		{
			Visible = false;
		}

		#endregion

		#region Core - Actions

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

			var world = Game.Managers.SaveManager.Node?.CreateWorld(name, (long)GD.Randi(), mode, key, (int)Game.Ui.CreateWorldUI.AutosaveInput.Node.Value);

			if (world == null)
			{
				return;
			}

			Game.Managers.WorldManager.Node.PendingWorld = world;
			Game.Managers.WorldManager.Node.PendingWorldIsDefault = false;

			Close();

			Game.Ui.CharacterSelectUI.Node?.OpenForOwnWorld();
		}

		private void OnBackPressed()
		{
			Close();

			Game.Ui.WorldSelectUI.Node?.Open();
		}

		#endregion
	}
}
