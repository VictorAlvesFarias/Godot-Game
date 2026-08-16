using Godot;
using Jogo25D.Core;

namespace Jogo25D.UI
{
	public partial class ErrorModalUI : CanvasLayer
	{
		#region Godot implementation

		public override void _Ready()
		{
			Layer = 30;
			Visible = false;

			Game.WhenReady(Initialize);
		}

		#endregion

		#region Core - Setup

		private void Initialize()
		{
			Game.Ui.ErrorModalUI.OkButton.Node.Pressed += OnOkPressed;
		}

		#endregion

		#region Public API

		public void ShowError(string message)
		{
			Game.Ui.ErrorModalUI.MessageLabel.Node.Text = message;

			Visible = true;
		}

		#endregion

		#region Core - Actions

		private void OnOkPressed()
		{
			Visible = false;
		}

		#endregion
	}
}
