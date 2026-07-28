using Godot;

namespace Jogo25D.UI
{
	public partial class ErrorModalUI : CanvasLayer
	{
		#region Node references

		public Panel Background { get; set; }
		public Label MessageLabel { get; set; }
		public Button OkButton { get; set; }

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			Layer = 30;
			Visible = false;

			Background = GetNode<Panel>("Background");
			MessageLabel = GetNode<Label>("Background/CenterContainer/Panel/MarginContainer/Root/MessageScroll/MessageLabel");
			OkButton = GetNode<Button>("Background/CenterContainer/Panel/MarginContainer/Root/OkButton");

			OkButton.Pressed += OnOkPressed;
		}

		#endregion

		#region Public API

		public void ShowError(string message)
		{
			MessageLabel.Text = message;
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
