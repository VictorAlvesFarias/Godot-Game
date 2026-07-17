using Godot;
using Jogo25D.Characters;

namespace Jogo25D.Systems
{
	public partial class AimIndicator : Node2D
	{
		public float Length { get; set; } = 25.0f;
		public float Width { get; set; } = 3.0f;
		public Color Color { get; set; } = new Color(1f, 1f, 1f, 0.7f);
		public float Offset { get; set; } = 40.0f;
		public bool IsActive { get; set; } = false;

		public Line2D _line;

		public override void _Ready()
		{
			_line = new Line2D();
			_line.Width = Width;
			_line.DefaultColor = Color;
			_line.ZIndex = 10;
			_line.Visible = false;
			AddChild(_line);
		}

		public override void _PhysicsProcess(double delta)
		{
			if (_line == null)
			{
				return;
			}

			var player = GetParent()?.GetParent() as Player;
			if (player == null || !IsActive)
			{
				_line.Visible = false;
				return;
			}

			var dir = player.Input.MousePosition - player.GlobalPosition;
			if (dir.LengthSquared() < 0.01f)
			{
				_line.Visible = false;
				return;
			}

			var d = dir.Normalized();
			_line.ClearPoints();
			_line.AddPoint(d * Offset);
			_line.AddPoint(d * (Offset + Length));
			_line.Visible = true;
		}

		public new void Hide()
		{
			IsActive = false;

			if (_line != null)
			{
				_line.Visible = false;
			}
		}
	}
}
