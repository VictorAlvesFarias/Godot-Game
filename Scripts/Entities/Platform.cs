using Godot;
using System;

[Tool]
public partial class Platform : StaticBody2D
{
	private Vector2 _size = new Vector2(100, 20);

	[Export]
	public Vector2 Size
	{
		get => _size;
		set
		{
			_size = value;
			UpdatePlatform();
		}
	}

	public override void _Ready()
	{
		UpdatePlatform();
	}

	private void UpdatePlatform()
	{
		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite != null)
		{
			sprite.RegionEnabled = true;
			sprite.RegionRect = new Rect2(Vector2.Zero, _size);
		}

		var collision = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (collision != null && collision.Shape is RectangleShape2D rectShape)
		{
			rectShape.Size = _size;
		}
	}
}
