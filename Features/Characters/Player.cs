using Godot;
using Jogo25D.Characters;
using Jogo25D.Systems;
using Jogo25D.Items;
using Jogo25D.Weapons;
using Jogo25D.Scripts.Actions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using Jogo25D.Scripts.Weapons;
using Jogo25D.Constants;

namespace Jogo25D.Characters
{
	public partial class Player : CharacterBody2D
	{
        #region Properties

        [Export] public float Speed { get; set; } = 300.0f;
		[Export] public float JumpVelocity { get; set; } = -750.0f;
		[Export] public float Gravity { get; set; }
		[Export] public int MaxHealth { get; set; } = 50;
		[Export] public int CurrentHealth { get; set; } = 50;
        [Export] public bool CanUpdateMovement { get; set; } = true;

		#endregion

		#region Systems

		public DashAction DashAction { get; private set; }
		public FireballAction FireballAction { get; private set; }
		public List<PlayerAction> UnlockedAbilities { get; private set; } = new List<PlayerAction>();
		public Inventory Inventory { get; private set; }
		public Combat CurrentWeaponSystem { get; private set; }
		public AimIndicator AimIndicator { get; private set; }
		public InputControls Controls { get; set; }

		#endregion

		#region Player effetcs

		public Line2D Sprite { get; private set; }
		public float DamageEffectTimer { get; set; } = 0f;
		public float DamageColorDuration { get; set; } = 0.3f;

		#endregion

		#region CharacterBody2D

		public override void _Ready()
		{
			AddToGroup("players");

			Controls = new InputControls();
			Gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
			Sprite = GetNodeOrNull<Line2D>(NodePaths.Player.SpriteBorder);
			DashAction = new DashAction(this);
			FireballAction = new FireballAction(this);


			UnlockedAbilities.Add(DashAction);
			UnlockedAbilities.Add(FireballAction);

            Controls.IsOwner = GetMultiplayerAuthority() == Multiplayer.GetUniqueId();
            Inventory = GetNodeOrNull<Inventory>(NodePaths.Player.Inventory);

			if (Inventory == null)
			{
				Inventory = new Inventory();

				AddChild(Inventory);

				Inventory.Name = "Inventory";
			}

			Inventory.ItemEquipped += OnItemEquipped;

			InitializeStartingWeapons();

			AimIndicator = new AimIndicator(this);

			Rpc(nameof(ResetPlayer));
		}

		public override void _ExitTree()
		{
			if (Inventory != null)
			{
				Inventory.ItemEquipped -= OnItemEquipped;
			}

			if (CurrentWeaponSystem != null && IsInstanceValid(CurrentWeaponSystem))
			{
				CurrentWeaponSystem.OnUnequip();
				CurrentWeaponSystem.QueueFree();
				CurrentWeaponSystem = null;
			}

			AimIndicator?.Cleanup();

			base._ExitTree();
		}


		public override void _PhysicsProcess(double delta)
		{
            if (Multiplayer.IsServer())
            {
                Rpc(nameof(SyncPosition), GlobalPosition);
            }

			DashAction.Update((float)delta);
			FireballAction.Update((float)delta);

			HandleInput();
			HandleMovement((float)delta);
			HandleAttack((float)delta);
			HandleReload((float)delta);
			
			AimIndicator.Update(Controls.MousePosition, GlobalPosition);

			if (DamageEffectTimer > 0)
			{
				DamageEffectTimer -= (float)delta;

				if (DamageEffectTimer <= 0 && Sprite != null && !DashAction.IsActive)
				{
					Sprite.DefaultColor = Colors.White;
				}
			}
		}

		#endregion

		#region Public server methods

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
		public void SetServerInput(float x, float y, bool jump, bool dash, bool attack, bool reload, bool inputAbility)
		{
			Controls.InputX = x;
			Controls.InputY = y;
			Controls.InputJump = jump;
			Controls.InputDash = dash;
			Controls.InputAttack = attack;
			Controls.InputReload = reload;
			Controls.InputAbility = inputAbility;
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
		public void SetServerMousePosition(Vector2 pos)
		{
			Controls.MousePosition = pos;
		}

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
        public void SyncPosition(Vector2 pos)
		{
			GlobalPosition = pos;
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
		public void ResetPlayer()
		{
			GlobalPosition = Vector2.Zero;
			Velocity = Vector2.Zero;
			CurrentHealth = MaxHealth;
		}

		#endregion

		#region Public local methods

		public void TakeDamage(int damage)
		{
			if (CurrentHealth <= 0)
			{
				return;
			}

			CurrentHealth -= damage;

			if (Sprite != null)
			{
				Sprite.DefaultColor = new Color(1f, 0.3f, 0.3f);
			}

			DamageEffectTimer = DamageColorDuration;

			if (CurrentHealth <= 0)
			{
				ResetPlayer();
			}
		}

		#endregion

		#region Public local methods

		private void HandleInput()
		{
			if (!Controls.IsOwner)
			{
				return;
			}

			Controls.InputX = Input.GetAxis("move_left", "move_right");
			Controls.InputY = Input.GetAxis("move_up", "move_down");
			Controls.InputJump = Input.IsActionJustPressed("move_up");
			Controls.InputDash = Input.IsActionJustPressed("dash");
			Controls.InputAttack = Input.IsActionPressed("shoot");
			Controls.InputReload = Input.IsActionJustPressed("reload");
			Controls.InputAbility = Input.IsActionJustPressed("ability");

			Rpc(nameof(SetServerInput), Controls.InputX, Controls.InputY, Controls.InputJump, Controls.InputDash, Controls.InputAttack, Controls.InputReload, Controls.InputAbility);
			Rpc(nameof(SetServerMousePosition), GetGlobalMousePosition());
		}

		private void HandleAttack(float delta)
		{
			if (CurrentWeaponSystem == null || !CurrentWeaponSystem.CanAttack())
				return;

			if (Controls.InputAttack)
			{
				var direction = (Controls.MousePosition - GlobalPosition).Normalized();
				CurrentWeaponSystem.Attack(direction);
			}
		}

		private void HandleReload(float delta)
		{
			if (CurrentWeaponSystem == null || !CurrentWeaponSystem.CanReload())
				return;

			if (Controls.InputReload)
			{
				CurrentWeaponSystem.Reload();
			}
		}

		private void HandleMovement(float delta)
		{
			if (!CanUpdateMovement)
			{
				MoveAndSlide();
			 
				return;
			}

			var v = Velocity;

			if (!IsOnFloor())
			{
				v.Y += Gravity * delta;
			}

			if (Controls.InputJump && IsOnFloor())
			{
				v.Y = JumpVelocity;
			}

			if (Controls.InputX != 0)
			{
				v.X = Controls.InputX * Speed;
			}
			else
			{
				v.X = Mathf.MoveToward(v.X, 0, Speed);
			}

			Velocity = v;

			MoveAndSlide();
		}
		
		private void OnItemEquipped(Item item, int slotIndex)
		{
			if (CurrentWeaponSystem != null)
			{
				CurrentWeaponSystem.OnUnequip();
				CurrentWeaponSystem.QueueFree();

				CurrentWeaponSystem = null;
			}

			var weaponInstance = CombatFactory.Use(item, this);

			CurrentWeaponSystem = weaponInstance;

			AddChild(weaponInstance);

			CurrentWeaponSystem.OnEquip();
		}

		private void InitializeStartingWeapons()
		{
			var meleeWeapon = new Item("Sword", ItemType.WeaponMelee);

			meleeWeapon.Description = "Uma espada básica para combate corpo a corpo";
			meleeWeapon.IsEquippable = true;
			meleeWeapon.Damage = 1;
			meleeWeapon.AttackCooldown = 0.5f;
			meleeWeapon.AttackRange = 80.0f;
			meleeWeapon.KnockbackForce = 200f;

			var rangedWeapon = new Item("Arco", ItemType.WeaponRanged);

			rangedWeapon.Description = "Um arco para ataques à distância";
			rangedWeapon.IsEquippable = true;
			rangedWeapon.Damage = 1;
			rangedWeapon.InfiniteCharges = false;
			rangedWeapon.MaxCharges = 10;
			rangedWeapon.AttackCooldown = 0.8f;
			rangedWeapon.AttackRange = 1500f; // Alcance máximo: 1500 unidades
			rangedWeapon.AttackArea = 50f; // Tamanho do projétil
			rangedWeapon.ProjectileSpeed = 750f; // Velocidade: 750 u/s → Lifetime = 1500/750 = 2s

			var rangedWeapon2 = new Item("Arco2", ItemType.WeaponRanged);

			rangedWeapon2.Description = "Um arco melhorado para ataques à distância";
			rangedWeapon2.IsEquippable = true;
			rangedWeapon2.InfiniteCharges = true;
			rangedWeapon2.MaxCharges = 1;
			rangedWeapon2.ChargeType = "none";
			rangedWeapon2.ReloadCooldown = 1.5f;
			rangedWeapon2.Damage = 1;
			rangedWeapon2.AttackCooldown = 0.01f;
			rangedWeapon2.AttackRange = 2000f; // Alcance máximo: 2000 unidades
			rangedWeapon2.AttackArea = 15f; // Tamanho do projétil maior
			rangedWeapon2.ProjectileSpeed = 1200f; // Velocidade: 1000 u/s → Lifetime = 2000/1000 = 2s

			var projectileScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Projectile.tscn");

			rangedWeapon.ProjectileScene = projectileScene;
			rangedWeapon2.ProjectileScene = projectileScene;

			var arrowAmmo = new Item("Flecha", ItemType.Consumable);
			arrowAmmo.Description = "Munição para arcos";
			arrowAmmo.ChargeType = "arrow";
			arrowAmmo.IsStackable = true;
			arrowAmmo.MaxStackSize = 9999;

			Inventory.AddItem(meleeWeapon, 1);
			Inventory.AddItem(rangedWeapon, 1);
			Inventory.AddItem(rangedWeapon2, 1);
			Inventory.AddItem(arrowAmmo, 1000);
			Inventory.EquipItem(0);
		}

		#endregion
	}
}
