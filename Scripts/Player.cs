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
	
	// Multiplayer
	private MultiplayerSynchronizer sync;
	
	// Efeitos visuais
	private CpuParticles2D dashParticles;
	private Line2D sprite;
	private float dashFlashTimer = 0.0f;

	public override void _Ready()
	{
		// Salvar posição inicial
		initialPosition = GlobalPosition;
		
		// Obtém a gravidade das configurações do projeto
		gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
		
		// Carrega a cena do projétil
		BulletScene = GD.Load<PackedScene>("res://Scenes/Bullet.tscn");
		
		// Obter referências dos efeitos visuais
		dashParticles = GetNodeOrNull<CpuParticles2D>("DashParticles");
		sprite = GetNodeOrNull<Line2D>("Sprite/Border");
		
		// Configurar multiplayer
		SetupMultiplayer();
	}
	
	private void SetupMultiplayer()
	{
		// Criar e configurar MultiplayerSynchronizer
		sync = new MultiplayerSynchronizer();
		AddChild(sync);
		
		// Configurar propriedades replicadas
		sync.SetMultiplayerAuthority(GetMultiplayerAuthority());
		sync.ReplicationConfig = new SceneReplicationConfig();
		
		// Adicionar propriedades para sincronização
		sync.ReplicationConfig.AddProperty(".:position");
		sync.ReplicationConfig.AddProperty(".:rotation");
		
		// Desabilitar processamento se não for o dono
		SetPhysicsProcess(IsMultiplayerAuthority());
		SetProcess(IsMultiplayerAuthority());
	}

	public override void _PhysicsProcess(double delta)
	{
		// Só processar física se for o dono deste player
		if (!IsMultiplayerAuthority())
			return;
			
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
			// Determinar direção do dash (horizontal e vertical)
			float horizontalDir = Input.GetAxis("move_left", "move_right");
			float verticalDir = 0;
			
			if (Input.IsActionPressed("move_up"))
			{
				verticalDir = -1;
			}
			else if (Input.IsActionPressed("move_down"))
			{
				verticalDir = 1;
			}
			
			// Se não estiver pressionando nenhuma direção, dash para a direita por padrão
			if (horizontalDir == 0 && verticalDir == 0)
			{
				horizontalDir = 1;
			}

			dashDirection = new Vector2(horizontalDir, verticalDir).Normalized();
			isDashing = true;
			canDash = false;
			dashTimer = 0.0f;
			
			// Ativar efeitos visuais do dash
			ActivateDashEffects();
		}

		// Controlar duração do dash
		if (isDashing)
		{
			dashTimer += (float)delta;
			if (dashTimer >= DashDuration)
			{
				isDashing = false;
				DeactivateDashEffects();
			}
		}
		
		// Atualizar efeito de flash
		if (dashFlashTimer > 0)
		{
			dashFlashTimer -= (float)delta;
			if (dashFlashTimer <= 0 && sprite != null)
			{
				sprite.DefaultColor = Colors.White;
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
			// Obter posição do mouse no mundo
			Vector2 mousePos = GetGlobalMousePosition();

			// Calcular direção do player para o mouse
			Vector2 direction = (mousePos - GlobalPosition).Normalized();

			// Chamar RPC para spawnar a bala em todos os clientes
			Rpc(nameof(SpawnBullet), GlobalPosition, direction);
		}
	}
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SpawnBullet(Vector2 spawnPosition, Vector2 direction)
	{
		if (BulletScene != null)
		{
			// Criar instância do projétil
			Bullet bullet = BulletScene.Instantiate<Bullet>();

			// Configurar posição do projétil (50 pixels à frente do player)
			bullet.GlobalPosition = spawnPosition + (direction * 50);
			bullet.Direction = direction;
			bullet.Shooter = this;

			// Adicionar o projétil à cena principal
			Owner.AddChild(bullet);
		}
	}

	public void ResetPosition()
	{
		if (!IsMultiplayerAuthority())
			return;
			
		GlobalPosition = initialPosition;
		Velocity = Vector2.Zero;
	}
	
	private void ActivateDashEffects()
	{
		// Emitir partículas
		if (dashParticles != null)
		{
			dashParticles.Emitting = true;
		}
		
		// Flash no sprite
		if (sprite != null)
		{
			sprite.DefaultColor = new Color(0.5f, 1f, 1f); // Ciano claro
			dashFlashTimer = DashDuration;
		}
	}
	
	private void DeactivateDashEffects()
	{
		// Restaurar cor do sprite
		if (sprite != null)
		{
			sprite.DefaultColor = Colors.White;
			dashFlashTimer = 0.0f;
		}
	}
}
