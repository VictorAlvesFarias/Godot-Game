using Godot;

public partial class FpsCounter : Label
{
	public override void _Process(double delta)
	{
		// Atualizar o texto com os FPS atuais
		Text = $"FPS: {Engine.GetFramesPerSecond()}";
	}
}
