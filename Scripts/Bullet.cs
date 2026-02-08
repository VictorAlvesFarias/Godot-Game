using Godot;

public partial class Bullet : Area2D
{
	// Velocidade do projétil
	[Export] public float Speed = 600.0f;

	// Direção do projétil (definida pelo player)
	public Vector2 Direction = Vector2.Zero;

	// Tempo de vida do projétil
	[Export] public float Lifetime = 3.0f;
	private float timer = 0.0f;
	
	// Referência ao player que disparou
	public Node2D Shooter;

	public override void _Ready()
	{
		// Conectar sinal de colisão
		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		// Mover o projétil na direção definida
		Position += Direction * Speed * (float)delta;

		// Incrementar timer
		timer += (float)delta;

		// Destruir o projétil após o tempo de vida
		if (timer >= Lifetime)
		{
			QueueFree();
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		// Ignorar colisão com o player que disparou
		if (body == Shooter)
			return;
		
		// Verificar se colidiu com um player
		if (body is Player player)
		{
			// Causar dano ao player - enviar apenas para o dono do player atingido
			int targetPeerId = player.GetMultiplayerAuthority();
			player.RpcId(targetPeerId, nameof(Player.TakeDamage), 1);
		}
			
		// Destruir o projétil ao colidir
		QueueFree();
	}
}
