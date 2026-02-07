using Godot;

public partial class Player : CharacterBody2D
{
	// Variáveis de movimento
	[Export] public float Speed = 300.0f;
	[Export] public float JumpVelocity = -750.0f;

	// Variáveis de dash
	[Export] public float DashSpeed = 800.0f;
	[Export] public float DashDuration = 0.2f;
	[Export] public float DashCooldown = 0.5f;
	private bool isDashing = false;
	private bool canDash = true;
	private float dashTimer = 0.0f;
	private float dashCooldownTimer = 0.0f;
	private Vector2 dashDirection = Vector2.Zero;

	// Variáveis de tiro
	[Export] public PackedScene BulletScene;
	[Export] public float FireRate = 0.2f;  // Tempo entre tiros
	private bool canShoot = true;
	private float shootTimer = 0.0f;

	// Posição inicial para reset
	private Vector2 initialPosition;

	// Gravidade do projeto
	private float gravity;

	public override void _Ready()
	{
		// Salvar posição inicial
		initialPosition = GlobalPosition;
		
		// Obtém a gravidade das configurações do projeto
		gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
		
		// Carrega a cena do projétil
		BulletScene = GD.Load<PackedScene>("res://Scenes/Bullet.tscn");
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		// Sistema de dash
		HandleDash(delta);

		// Se estiver em dash, aplicar movimento de dash
		if (isDashing)
		{
			velocity = dashDirection * DashSpeed;
		}
		else
		{
			// Aplicar gravidade
			if (!IsOnFloor())
			{
				velocity.Y += gravity * (float)delta;
			}

			// Pulo
			if (Input.IsActionJustPressed("move_up") && IsOnFloor())
			{
				velocity.Y = JumpVelocity;
			}

			// Movimento horizontal
			float direction = Input.GetAxis("move_left", "move_right");
			if (direction != 0)
			{
				velocity.X = direction * Speed;
			}
			else
			{
				velocity.X = Mathf.MoveToward(velocity.X, 0, Speed);
			}
		}

		Velocity = velocity;
		MoveAndSlide();

		// Sistema de tiro (não pode atirar durante dash)
		if (!isDashing)
		{
			HandleShooting(delta);
		}
	}

	private void HandleDash(double delta)
	{
		// Timer de cooldown
		if (!canDash)
		{
			dashCooldownTimer += (float)delta;
			if (dashCooldownTimer >= DashCooldown)
			{
				canDash = true;
				dashCooldownTimer = 0.0f;
			}
		}

		// Iniciar dash
		if (Input.IsActionJustPressed("dash") && canDash && !isDashing)
		{
			// Determinar direção do dash
			float horizontalDir = Input.GetAxis("move_left", "move_right");
			
			// Se não estiver pressionando nenhuma direção, dash para a direita por padrão
			if (horizontalDir == 0)
			{
				horizontalDir = 1;
			}

			dashDirection = new Vector2(horizontalDir, 0).Normalized();
			isDashing = true;
			canDash = false;
			dashTimer = 0.0f;
		}

		// Controlar duração do dash
		if (isDashing)
		{
			dashTimer += (float)delta;
			if (dashTimer >= DashDuration)
			{
				isDashing = false;
			}
		}
	}

	private void HandleShooting(double delta)
	{
		// Timer para controlar taxa de disparo
		if (!canShoot)
		{
			shootTimer += (float)delta;
			if (shootTimer >= FireRate)
			{
				canShoot = true;
				shootTimer = 0.0f;
			}
		}

		// Disparar ao clicar com o mouse
		if (Input.IsActionPressed("shoot") && canShoot)
		{
			Shoot();
			canShoot = false;
		}
	}

	private void Shoot()
	{
		if (BulletScene != null)
		{
			// Criar instância do projétil
			Bullet bullet = BulletScene.Instantiate<Bullet>();

			// Obter posição do mouse no mundo
			Vector2 mousePos = GetGlobalMousePosition();

			// Calcular direção do player para o mouse
			Vector2 direction = (mousePos - GlobalPosition).Normalized();

			// Configurar posição do projétil (50 pixels à frente do player)
			bullet.GlobalPosition = GlobalPosition + (direction * 50);
			bullet.Direction = direction;
			bullet.Shooter = this;

			// Adicionar o projétil à cena principal
			Owner.AddChild(bullet);
		}
	}

	public void ResetPosition()
	{
		GlobalPosition = initialPosition;
		Velocity = Vector2.Zero;
	}
}
