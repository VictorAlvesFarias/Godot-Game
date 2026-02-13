using Godot;
using System;

[Tool] // Permite que o código rode no Editor
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
		// 1. Sincroniza o Sprite2D (Textura)
		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite != null)
		{
			sprite.RegionEnabled = true;
			// Define o retângulo da textura para o novo tamanho
			sprite.RegionRect = new Rect2(Vector2.Zero, _size);
		}

		// 2. Sincroniza o CollisionShape2D
		var collision = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (collision != null && collision.Shape is RectangleShape2D rectShape)
		{
			// No Godot, o size do RectangleShape2D é o tamanho total
			rectShape.Size = _size;
		}
	}
}
