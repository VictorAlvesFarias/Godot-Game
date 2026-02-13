using Godot;
using Jogo25D.Characters;
using Jogo25D.Systems;
using Jogo25D.Items;
using Jogo25D.Weapons;
using System;
using System.Globalization;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;

namespace Jogo25D.Characters
{
    public partial class Player : CharacterBody2D
    {
        #region Base stats
    
        [Export] public float Speed { get; set; } = 300.0f;
        [Export] public float JumpVelocity { get; set; } = -750.0f;
        [Export] public float Gravity { get; set; }
        [Export] public int MaxHealth { get; set; } = 50;
        [Export] public int CurrentHealth { get; set; }

        #endregion

        #region Dash

        [Export] public bool IsDashing { get; set; }
        [Export] public bool CanDash { get; set; } = true;
        [Export] public float DashSpeed { get; set; } = 800.0f;
        [Export] public float DashDuration { get; set; } = 0.2f;
        [Export] public float DashCooldown { get; set; } = 0.5f;
        [Export] public float DashTimer { get; set; }
        [Export] public float DashCooldownTimer { get; set; }
        [Export] public Vector2 DashDirection { get; set; }

        #endregion

        #region Inventory System

        public Inventory Inventory { get; private set; }
        private Weapon currentWeaponSystem;
        private Node2D weaponHolder;
        private Vector2 lastAttackDirection = Vector2.Right;
    
        [Export] public float WeaponOffset { get; set; } = 25.0f;

        #endregion

        #region Aim Indicator

        private Line2D aimIndicator;
        [Export] public float AimIndicatorLength { get; set; } = 25.0f;
        [Export] public float AimIndicatorWidth { get; set; } = 3.0f;
        [Export] public Color AimIndicatorColor { get; set; } = new Color(1f, 1f, 1f, 0.7f);
        [Export] public float AimIndicatorOffset { get; set; } = 40.0f;

        #endregion

        public float inputX;
        public float inputY;
        public bool inputJump;
        public bool inputDash;
        public bool inputAttack;
        public Vector2 mousePosition;
        public bool isOwner;
        public Vector2 InitialPosition;

        public CpuParticles2D dashParticles;
        public Line2D sprite;
        public float DamageEffectTimer { get; set; } = 0f;
        public float DamageColorDuration { get; set; } = 0.3f;

        public override void _Ready()
        {
            AddToGroup("players");

            Gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
            dashParticles = GetNodeOrNull<CpuParticles2D>("DashParticles");
            sprite = GetNodeOrNull<Line2D>("Sprite/Border");
                InitialPosition=GlobalPosition;

                // Inicializar inventário
                Inventory = GetNodeOrNull<Inventory>("Inventory");
                if (Inventory == null)
                {
                    Inventory = new Inventory();
                    AddChild(Inventory);
                    Inventory.Name = "Inventory";
                }
            
                // Criar weapon holder para posicionamento das armas
                weaponHolder = new Node2D();
                weaponHolder.Name = "WeaponHolder";
                AddChild(weaponHolder);
            
                // Conectar ao sinal de equipar item
                Inventory.ItemEquipped += OnItemEquipped;
            
                // Adicionar armas iniciais
                InitializeStartingWeapons();

                // Criar indicador de mira
                aimIndicator = new Line2D();
                aimIndicator.Width = AimIndicatorWidth;
                aimIndicator.DefaultColor = AimIndicatorColor;
                aimIndicator.ZIndex = 10;
                AddChild(aimIndicator);

            Rpc(nameof(ResetPlayer));
        }

        public override void _ExitTree()
        {
            // Desconectar sinais
            if (Inventory != null)
            {
                Inventory.ItemEquipped -= OnItemEquipped;
            }
            
            // Limpar arma atual
            if (currentWeaponSystem != null && IsInstanceValid(currentWeaponSystem))
            {
                currentWeaponSystem.OnUnequip();
                currentWeaponSystem.QueueFree();
                currentWeaponSystem = null;
            }
            
            base._ExitTree();
        }

        public override void _PhysicsProcess(double delta)
        {
            isOwner = GetMultiplayerAuthority() == Multiplayer.GetUniqueId();

            HandleInput();
            HandleMovement((float)delta);
            HandleAttack((float)delta);
            UpdateWeaponPosition();
            UpdateAimIndicator();
            //HandleLogs();
        
            if (DamageEffectTimer > 0)
            {
                DamageEffectTimer -= (float)delta;

                if (DamageEffectTimer <= 0 && sprite != null && !IsDashing)
                {
                    sprite.DefaultColor = Colors.White;
                }
            }
        }

        #region Server methods

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
        public void SetServerInput(float x, float y, bool jump, bool dash, bool attack)
        {
            inputX = x;
            inputY = y;
            inputJump = jump;
            inputDash = dash;
            inputAttack = attack;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
        public void SetServerMousePosition(Vector2 pos)
        {
            mousePosition = pos;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
        public void ResetPlayer()
        {
                GlobalPosition = InitialPosition;
            Velocity = Vector2.Zero;
            CurrentHealth = MaxHealth;
        }

        #endregion

        #region Local methods

        public void HandleInput()
        {
            if (!isOwner)
            {
                return;
            }

            inputX = Input.GetAxis("move_left", "move_right");
            inputY = Input.GetAxis("move_up", "move_down");
            inputJump = Input.IsActionJustPressed("move_up");
            inputDash = Input.IsActionJustPressed("dash");
            inputAttack = Input.IsActionPressed("shoot");

            Rpc(nameof(SetServerInput), inputX, inputY, inputJump, inputDash, inputAttack);
            Rpc(nameof(SetServerMousePosition), GetGlobalMousePosition());
        }

        public void HandleMovement(float delta)
        {
            var v = Velocity;
            if (!CanDash)
            {
                DashCooldownTimer += delta;

                if (DashCooldownTimer >= DashCooldown)
                {
                    CanDash = true;
                    DashCooldownTimer = 0;
                }
            }

            if (inputDash && CanDash && !IsDashing)
            {
                Vector2 direction = new Vector2(inputX, inputY);

                if (direction.Length() == 0)
                {
                    DashDirection = Vector2.Up;
                }

                DashDirection = direction.Normalized();

                IsDashing = true;
                CanDash = false;
                DashTimer = 0;

                if (dashParticles != null)
                {
                    dashParticles.Emitting = true;
                }

                if (sprite != null)
                {
                    sprite.DefaultColor = new Color(0.5f, 1f, 1f);
                }
            }

            if (IsDashing)
            {
                DashTimer += delta;

                v = DashDirection * DashSpeed;

                if (DashTimer >= DashDuration)
                {
                    IsDashing = false;

                    if (sprite != null)
                    {
                        sprite.DefaultColor = Colors.White;
                    }
                }
            }
            else
            {
                if (!IsOnFloor())
                {
                    v.Y += Gravity * delta;
                }

                if (inputJump && IsOnFloor())
                {
                    v.Y = JumpVelocity;
                }

                if (inputX != 0)
                {
                    v.X = inputX * Speed;
                }
                else
                {
                    v.X = Mathf.MoveToward(v.X, 0, Speed);
                }
            }

            Velocity = v;

            MoveAndSlide();
        }

        public void HandleAttack(float delta)
        {
            if (currentWeaponSystem == null || !currentWeaponSystem.CanAttack)
                return;

            if (inputAttack)
            {
                // Direção do ataque baseada na posição do mouse
                var direction = (mousePosition - GlobalPosition).Normalized();
                lastAttackDirection = direction;
                currentWeaponSystem.Attack(direction);
            }
        }
    
        public void HandleLogs()
        {
            Console.Clear();
        }

        public void TakeDamage(int damage)
        {
            if (CurrentHealth <= 0)
            {
                return;
            }

            CurrentHealth -= damage;

            if (sprite != null)
            {
                sprite.DefaultColor = new Color(1f, 0.3f, 0.3f);
            }

            DamageEffectTimer = DamageColorDuration;

            if (CurrentHealth <= 0)
            {
                Rpc(nameof(ResetPlayer));
            }
        }

        private void UpdateWeaponPosition()
        {
            if (weaponHolder == null || lastAttackDirection.LengthSquared() <= 0.01f)
                return;
            
            // Rotacionar para a direção do ataque
            weaponHolder.Rotation = lastAttackDirection.Angle();
        
            // Inverter posição horizontal se estiver mirando para a esquerda
            if (lastAttackDirection.X < 0)
            {
                // Lado esquerdo
                weaponHolder.Position = new Vector2(-WeaponOffset, 0);
                weaponHolder.Scale = new Vector2(1, -1); // Flip vertical da arma
            }
            else
            {
                // Lado direito
                weaponHolder.Position = new Vector2(WeaponOffset, 0);
                weaponHolder.Scale = new Vector2(1, 1);
            }
        }

        private void UpdateAimIndicator()
        {
            if (aimIndicator == null)
                return;

            // Calcular direção do mouse
            var direction = (mousePosition - GlobalPosition).Normalized();
            
            if (direction.LengthSquared() > 0.01f)
            {
                aimIndicator.ClearPoints();
                
                // Ponto inicial: na borda do player com offset configurável
                var startOffset = direction * AimIndicatorOffset;
                var startPoint = startOffset;
                
                // Ponto final: na direção do mouse
                var endPoint = startOffset + (direction * AimIndicatorLength);
                
                aimIndicator.AddPoint(startPoint);
                aimIndicator.AddPoint(endPoint);
                
                aimIndicator.Visible = true;
            }
            else
            {
                aimIndicator.Visible = false;
            }
        }
    
        private void OnItemEquipped(Item item, int slotIndex)
        {
            if (currentWeaponSystem != null)
            {
                currentWeaponSystem.OnUnequip();
                currentWeaponSystem.QueueFree();
                currentWeaponSystem = null;
            }
        
            if (item == null || item.Type != ItemType.Weapon)
            {
                return;
            }
        
        Weapon weaponInstance = null;
    
        if (item.WeaponType == WeaponType.Melee)
        {
            var meleeWeapon = new MeleeWeapon();
            meleeWeapon.Range = item.AttackRange;
            weaponInstance = meleeWeapon;
        }
        else if (item.WeaponType == WeaponType.Ranged)
        {
            var rangedWeapon = new RangedWeapon();
            rangedWeapon.Range = item.AttackRange;
            rangedWeapon.Area = item.AttackArea;
            rangedWeapon.BulletScene = item.ProjectileScene;
            rangedWeapon.BulletSpeed = item.ProjectileSpeed;
            weaponInstance = rangedWeapon;
        }
    
        if (weaponInstance != null)
        {
            weaponInstance.WeaponName = item.ItemName;
            weaponInstance.Damage = item.Damage;
            weaponInstance.AttackCooldown = item.AttackCooldown;
            weaponInstance.Icon = item.Icon;
        
            weaponHolder.AddChild(weaponInstance);
            currentWeaponSystem = weaponInstance;
            currentWeaponSystem.OnEquip();
        }
    }

        private void InitializeStartingWeapons()
        {
            var meleeWeapon = new Item("Espada", ItemType.Weapon);

            meleeWeapon.Description = "Uma espada básica para combate corpo a corpo";
            meleeWeapon.IsEquippable = true;
            meleeWeapon.WeaponType = WeaponType.Melee;
            meleeWeapon.Damage = 1;
            meleeWeapon.AttackCooldown = 0.5f;
            meleeWeapon.AttackRange = 80.0f;
            meleeWeapon.KnockbackForce = 200f;
    
            var rangedWeapon = new Item("Arco", ItemType.Weapon);

            rangedWeapon.Description = "Um arco para ataques à distância";
            rangedWeapon.IsEquippable = true;
            rangedWeapon.WeaponType = WeaponType.Ranged;
            rangedWeapon.Damage = 1;
            rangedWeapon.AttackCooldown = 0.8f;
            rangedWeapon.AttackRange = 1500f; // Alcance máximo: 1500 unidades
            rangedWeapon.AttackArea = 50f; // Tamanho do projétil
            rangedWeapon.ProjectileSpeed = 750f; // Velocidade: 750 u/s → Lifetime = 1500/750 = 2s

            var rangedWeapon2 = new Item("Arco2", ItemType.Weapon);

            rangedWeapon2.Description = "Um arco melhorado para ataques à distância";
            rangedWeapon2.IsEquippable = true;
            rangedWeapon2.WeaponType = WeaponType.Ranged;
            rangedWeapon2.Damage = 1;
            rangedWeapon2.AttackCooldown = 0.01f;
            rangedWeapon2.AttackRange = 2000f; // Alcance máximo: 2000 unidades
            rangedWeapon2.AttackArea = 15f; // Tamanho do projétil maior
            rangedWeapon2.ProjectileSpeed = 1200f; // Velocidade: 1000 u/s → Lifetime = 2000/1000 = 2s

            var projectileScene = GD.Load<PackedScene>("res://Scenes/Entities/Projectile.tscn");
    
            rangedWeapon.ProjectileScene = projectileScene;
            rangedWeapon2.ProjectileScene = projectileScene;
    
            Inventory.AddItem(meleeWeapon, 1);
            Inventory.AddItem(rangedWeapon, 1);
            Inventory.AddItem(rangedWeapon2, 1);
            Inventory.EquipItem(0);
        }

        #endregion
}

public abstract class PlayerAction
{
    public Player Player { get; set; }
    public bool CanUse { get; set; } = true;
    public bool Cooldown { get; set; } = false;

    public PlayerAction(Player player)
    {
        Player = player;
    }

    public abstract void Activate();
    public abstract void Deactivate();
    public abstract void Reset();
    public abstract void ActivateEffects();
    public abstract void DeactivateEffects();
}

public class InputControls { 
}

public class DashAction : PlayerAction
{
    public DashAction(Player player) : base(player)
    {

    }

    public override void Activate()
    {
        throw new NotImplementedException();
    }

    public override void ActivateEffects()
    {
        throw new NotImplementedException();
    }

    public override void Deactivate()
    {
        throw new NotImplementedException();
    }

    public override void DeactivateEffects()
    {
        throw new NotImplementedException();
    }

    public override void Reset()
    {
        throw new NotImplementedException();
    }
}
}