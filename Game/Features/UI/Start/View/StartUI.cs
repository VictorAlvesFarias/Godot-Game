using Godot;
using Jogo25D.Core;

namespace Jogo25D.UI
{
	public partial class StartUI : CanvasLayer
	{
		#region Godot implementation

		public override void _Ready()
		{
			Layer = 20;

			// Fica escondida ate o Bootstrap validar todos os nodes estaticos - e ele quem abre.
			Visible = false;

			Game.WhenReady(Initialize);
		}

		#endregion

		#region Core - Setup

		private void Initialize()
		{
			Game.Ui.StartUI.PlayButton.Node.Pressed += OnPlayPressed;
			Game.Ui.StartUI.ExitButton.Node.Pressed += OnExitPressed;
		}

		#endregion

		#region Core - Actions

		public void OnPlayPressed()
		{
			Visible = false;

			Game.Ui.WorldSelectUI.Node.Open();
		}

		public void OnExitPressed()
		{
			GetTree().Quit();
		}

		#endregion
	}
}
