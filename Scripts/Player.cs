using Godot;

public partial class Player : CharacterBody2D
{
    [Export] public float Speed { get; set; } = 300.0f;
    [Export] public float JumpVelocity { get; set; } = -750.0f;
    [Export] public float DashSpeed { get; set; } = 800.0f;
    [Export] public float DashDuration { get; set; } = 0.2f;
    [Export] public float DashCooldown { get; set; } = 0.5f;
    [Export] public float FireRate { get; set; } = 0.2f;
    [Export] public int MaxHealth { get; set; } = 5;

    public int CurrentHealth { get; set; }
    public Vector2 InitialPosition { get; set; }
    public float Gravity { get; set; }

    private bool isDashing;
    private bool canDash = true;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector2 dashDirection = Vector2.Zero;

    private bool canShoot = true;
    private float shootTimer;
    
    private float damageColorTimer = 0f;
    private const float DamageColorDuration = 0.3f;

    private float inputX;
    private float inputY;
    private bool inputJump;
    private bool inputDash;

    private CpuParticles2D dashParticles;
    private Line2D sprite;
    private PackedScene bulletScene;

    public override void _Ready()
    {
        AddToGroup("players");
        InitialPosition = GlobalPosition;
        CurrentHealth = MaxHealth;
        Gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();

        bulletScene = GD.Load<PackedScene>("res://Scenes/Bullet.tscn");
        dashParticles = GetNodeOrNull<CpuParticles2D>("DashParticles");
        sprite = GetNodeOrNull<Line2D>("Sprite/Border");
    }

    public override void _PhysicsProcess(double delta)
    {
        bool isServer = GetMultiplayerAuthority() == 1;
        bool isOwner = GetMultiplayerAuthority() == Multiplayer.GetUniqueId();

        if (isOwner)
        {
            HandleInput();
            Rpc(nameof(ServerReceiveInput), inputX, inputY, inputJump, inputDash);
        }

        if (isServer)
        {
            HandleMovement((float)delta, inputX, inputJump, inputDash);
        }
        else if (isOwner)
        {
            HandleMovement((float)delta, inputX, inputJump, inputDash);
        }

        if (!isDashing && isOwner)
        {
            HandleShooting((float)delta);
        }
        
        // Timer para voltar a cor ao normal após dano
        if (damageColorTimer > 0)
        {
            damageColorTimer -= (float)delta;
            if (damageColorTimer <= 0 && sprite != null && !isDashing)
            {
                sprite.DefaultColor = Colors.White;
            }
        }
    }

    public void HandleInput()
    {
        inputX = Input.GetAxis("move_left", "move_right");
        inputY = Input.GetAxis("move_up", "move_down");
        inputJump = Input.IsActionJustPressed("move_up");
        inputDash = Input.IsActionJustPressed("dash");
    }

    public void HandleMovement(float delta, float x, bool jump, bool dash)
    {
        Vector2 v = Velocity;

        if (!canDash)
        {
            dashCooldownTimer += delta;
            if (dashCooldownTimer >= DashCooldown)
            {
                canDash = true;
                dashCooldownTimer = 0;
            }
        }

        if (dash && canDash && !isDashing)
        {
            Vector2 direction = new Vector2(x, inputY);
            if (direction.Length() == 0)
                direction = Vector2.Right;
            dashDirection = direction.Normalized();
            isDashing = true;
            canDash = false;
            dashTimer = 0;
            Rpc(nameof(ActivateDashEffectsRpc));
        }

        if (isDashing)
        {
            dashTimer += delta;
            v = dashDirection * DashSpeed;

            if (dashTimer >= DashDuration)
            {
                isDashing = false;
                Rpc(nameof(DeactivateDashEffectsRpc));
            }
        }
        else
        {
            if (!IsOnFloor())
                v.Y += Gravity * delta;

            if (jump && IsOnFloor())
                v.Y = JumpVelocity;

            if (x != 0)
                v.X = x * Speed;
            else
                v.X = Mathf.MoveToward(v.X, 0, Speed);
        }

        Velocity = v;
        MoveAndSlide();
    }

    public void HandleResetPosition()
    {
        Rpc(nameof(ResetPosition));
        Rpc(nameof(RestoreHealthRpc));
    }

    public void HandleShooting(float delta)
    {
        if (!canShoot)
        {
            shootTimer += delta;
            if (shootTimer >= FireRate)
            {
                canShoot = true;
                shootTimer = 0;
            }
        }

        if (Input.IsActionPressed("shoot") && canShoot && bulletScene != null)
        {
            Vector2 dir = (GetGlobalMousePosition() - GlobalPosition).Normalized();
            Rpc(nameof(SpawnBullet), GlobalPosition, dir);
            canShoot = false;
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    public void ServerReceiveInput(float x, float y, bool jump, bool dash)
    {
        inputX = x;
        inputY = y;
        inputJump = jump;
        inputDash = dash;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void SpawnBullet(Vector2 spawnPosition, Vector2 direction)
    {
        Bullet bullet = bulletScene.Instantiate<Bullet>();
        bullet.GlobalPosition = spawnPosition + (direction * 50);
        bullet.Direction = direction;
        bullet.Shooter = this;

        Node mainScene = GetTree().Root.GetNodeOrNull("Main");
        if (mainScene != null)
            mainScene.AddChild(bullet);
        else
            bullet.QueueFree();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void ResetPosition()
    {
        GlobalPosition = InitialPosition;
        Velocity = Vector2.Zero;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RestoreHealthRpc()
    {
        CurrentHealth = MaxHealth;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void ActivateDashEffectsRpc()
    { 
        if (dashParticles != null)
            dashParticles.Emitting = true;

        if (sprite != null)
            sprite.DefaultColor = new Color(0.5f, 1f, 1f);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void DeactivateDashEffectsRpc()
    {
        if (sprite != null)
            sprite.DefaultColor = Colors.White;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void TakeDamage(int damage)
    {
        // Apenas o servidor processa o dano
        if (!Multiplayer.IsServer())
            return;

        if (CurrentHealth <= 0)
            return;

        CurrentHealth -= damage;
        
        // Sincronizar a vida e efeito visual com todos os clientes
        Rpc(nameof(SyncHealth), CurrentHealth);
        Rpc(nameof(ShowDamageEffect));

        if (CurrentHealth <= 0)
            HandleResetPosition();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void ShowDamageEffect()
    {
        if (sprite != null)
            sprite.DefaultColor = new Color(1f, 0.3f, 0.3f);
        damageColorTimer = DamageColorDuration;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void SyncHealth(int health)
    {
        CurrentHealth = health;
    }
}
