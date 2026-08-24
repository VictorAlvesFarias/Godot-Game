using Godot;
using Jogo25D.Core;

namespace Jogo25D.UI
{
	public partial class StartUI : ScreenUI
	{
		#region Godot implementation

		public override void _Ready()
		{
			Game.WhenReady(Initialize);
		}

		#endregion

		#region Core - Setup

		private void Initialize()
		{
			// Sessao acabou (saiu do mundo ou o servidor caiu): o menu inicial volta.
			Game.Managers.SessionManager.Node.SessionEnded += OnSessionEnded;

			Game.Ui.StartUI.PlayButton.Node.Pressed += OnPlayPressed;
			Game.Ui.StartUI.ExitButton.Node.Pressed += OnExitPressed;
		}

		#endregion

		#region UI - Actions

		public void OnPlayPressed()
		{
			Game.Managers.RouterManager.Node.Open(Game.Ui.WorldSelectUI.Node);
		}

		private void OnSessionEnded()
		{
			GetTree().Paused = false;

			Game.Managers.RouterManager.Node.Replace(this);
		}

		public void OnExitPressed()
		{
			GetTree().Quit();
		}

		#endregion
	}
}
