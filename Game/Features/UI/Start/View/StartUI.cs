using Godot;

namespace Jogo25D.UI
{
	public partial class StartUI : CanvasLayer
	{
		#region Node references

		public Button PlayButton { get; set; }
		public Button ExitButton { get; set; }

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			Layer = 20;

			PlayButton = GetNode<Button>("MarginContainer/Root/MenuColumn/PlayButton");
			ExitButton = GetNode<Button>("MarginContainer/Root/MenuColumn/ExitButton");

			PlayButton.Pressed += OnPlayPressed;
			ExitButton.Pressed += OnExitPressed;
		}

		#endregion

		#region Core - Actions

		public void OnPlayPressed()
		{
			Visible = false;

			GetTree().Root.GetNodeOrNull<WorldSelectUI>("Main/Ui/WorldSelectUI")?.Open();
		}

		public void OnExitPressed()
		{
			GetTree().Quit();
		}

		#endregion
	}
}
