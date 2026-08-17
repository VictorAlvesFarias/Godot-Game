using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
	public partial class CreateCharacterUI : ScreenUI
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
            Game.Ui.CreateCharacterUI.NameInput.Node.Text = "";
        }

        #endregion

        #region Core - Setup

        private void Initialize()
		{
			Game.Ui.CreateCharacterUI.BackButton.Node.Pressed += OnBackPressed;
			Game.Ui.CreateCharacterUI.CreateButton.Node.Pressed += OnCreatePressed;
		}

        #endregion

		#region Core - Actions

		private void OnCreatePressed()
		{
			var name = string.IsNullOrWhiteSpace(Game.Ui.CreateCharacterUI.NameInput.Node.Text) ? "Sem nome" : Game.Ui.CreateCharacterUI.NameInput.Node.Text.Trim();

			Game.Managers.SaveManager.Node.CreateCharacter(name);
		}

		private void OnBackPressed()
		{
			Game.Managers.RouterManager.Node.Open(Game.Ui.CharacterSelectUI.Node);
		}

		#endregion
	}
}
