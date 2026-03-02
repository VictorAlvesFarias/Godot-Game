using Godot;
using Jogo25D.Characters;
using Jogo25D.Systems;
using Jogo25D.Items;
using Jogo25D.Properties;
using Jogo25D.Effects;
using Jogo25D.Scripts.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using Jogo25D.Constants;

namespace Jogo25D.Characters
{
	public partial class Player : CharacterBody2D
	{
        #region Properties

        public float Speed { get; set; } = 300.0f;
		public float JumpVelocity { get; set; } = -750.0f;
		public float Gravity { get; set; }
		public int MaxHealth { get; set; } = 50;
		public int CurrentHealth { get; set; } = 50;
        public bool CanUpdateMovement { get; set; } = true;
        public bool ReloadPending { get; set; } = true;

        #endregion

        #region Systems

        public DashAction DashAction { get; private set; }
		public FireballAction FireballAction { get; private set; }
		public List<PlayerAction> UnlockedAbilities { get; private set; } = new List<PlayerAction>();
		public Inventory Inventory { get; private set; }
		public ItemRechargeableInstance EquippedInstance { get; private set; }
		public AimIndicator AimIndicator { get; private set; }
        private WorldManager NetworkManager { get; set; }
        public Vector2 TargetPosition { get; set; }
		public long PeerId { get; set; } = 1;
        public List<EffectDefinition> Effects { get; private set; } = new();
        public List<BaseProperty> Buffs { get; private set; } = new();

		#endregion

		#region Inputs

		public bool IsOwner { get; private set; }
		public float InputX { get; private set; }
		public float InputY { get; private set; }
		public bool InputJump { get; private set; }
		public bool InputDash { get; private set; }
		public bool InputAttack { get; private set; }
		public bool InputReload { get; private set; }
		public bool InputAbility { get; private set; }
		public bool InputScrollNext { get; private set; }
		public bool InputScrollPrev { get; private set; }
		public Vector2 MousePosition { get; private set; }

		#endregion

		#region Player effects

		public Line2D Sprite { get; private set; }
		public float DamageEffectTimer { get; set; } = 0f;
		public float DamageColorDuration { get; set; } = 0.3f;

		#endregion

		#region CharacterBody2D

		public override void _Ready()
		{
			AddToGroup("players");

			Gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
            Sprite = GetNodeOrNull<Line2D>("Sprite/Border");
            NetworkManager = GetTree().Root.GetNode<WorldManager>(WorldManager.DEFAULT_NODE_PATH);

            if (Sprite == null)
			{
                GD.Print($"[Player._Ready] GetNodeOrNull: Sprite not founded");
            }

            DashAction = new DashAction(this);
			FireballAction = new FireballAction(this);

			UnlockedAbilities.Add(DashAction);
			UnlockedAbilities.Add(FireballAction);

            IsOwner = GetMultiplayerAuthority() == Multiplayer.GetUniqueId();
			Inventory = GetNodeOrNull<Inventory>("Inventory");;

			if (Inventory == null)
			{
                GD.Print($"[Player._Ready] GetNodeOrNull: Inventory not founded");

                Inventory = new Inventory();

				AddChild(Inventory);

				Inventory.Name = "Inventory";
			}

			Inventory.ItemEquipped += OnItemEquipped;

			AimIndicator = new AimIndicator(this);
		}

		public override void _ExitTree()
		{
			if (Inventory != null)
			{
				Inventory.ItemEquipped -= OnItemEquipped;
			}

			EquippedInstance = null;

			AimIndicator?.Cleanup();

			base._ExitTree();
		}

		public override void _PhysicsProcess(double delta)
		{
            if (Multiplayer.IsServer())
            {
                Rpc(nameof(SyncPosition), GlobalPosition);
            }
			else
			{
                GlobalPosition = TargetPosition;
            }

			foreach (var effect in Effects.Where(e => e.ApplyToOwner))
			{
                effect.Tick(this, (float)delta);

                if (effect.Expired)
                {
                    Effects.Remove(effect);
                }
            }

            HandleInput();

            DashAction.Update((float)delta);
			FireballAction.Update((float)delta);
			EquippedInstance?.Update((float)delta);

			HandleHotbarScroll();
			HandleMovement((float)delta);
			HandleAttack((float)delta);
			HandleReload((float)delta);
			
			AimIndicator.Update(MousePosition, GlobalPosition);

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
			InputX       = x;
			InputY       = y;
			InputJump    = jump;
			InputDash    = dash;
			InputAttack  = attack;
			InputReload  = reload;
			InputAbility = inputAbility;
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
		public void SetServerMousePosition(Vector2 pos)
		{
			MousePosition = pos;
		}

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
        public void SyncPosition(Vector2 pos)
        {
            TargetPosition = pos;
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
				NetworkManager.ResetPlayerClientRequest();

            }
		}

		public void ReceiveDamage(DamageInfo damage)
		{
			var resistanceFactor = 0f;

			foreach (var buff in Buffs)
			{
				if (buff is DamageResistenceProperty r && r.DamageType == damage.Type)
				{
					resistanceFactor = System.Math.Max(resistanceFactor, r.ResistanceFactor);
				}
			}

			var critMultiplier = 1f + (GD.Randf() <= damage.CritChance ? damage.CritDamage : 0f);
			var finalDamage = (int)(damage.Amount * critMultiplier * (1f - resistanceFactor));
			
			TakeDamage(finalDamage);
		}

		public void AddEffect(EffectDefinition definition)
		{
			if (definition == null)
			{
				return;
			}

			Effects.Add(definition.Clone());
		}

		#endregion

		#region Public local methods

		private void HandleInput()
		{
			if (!IsOwner)
			{
				return;
			}

			var im = InputManager.Instance;

			InputX       = im.MoveX;
			InputY       = im.MoveY;
			InputJump    = im.Jump;
			InputDash    = im.Dash;
			InputAttack  = im.Attack;
			InputReload  = im.Reload;
			InputAbility   = im.Ability;
			InputScrollNext = im.ScrollNext;
			InputScrollPrev = im.ScrollPrev;
			MousePosition  = GetGlobalMousePosition();

			Rpc(nameof(SetServerInput), InputX, InputY, InputJump, InputDash, InputAttack, InputReload, InputAbility);
			Rpc(nameof(SetServerMousePosition), MousePosition);
		}

		private void HandleAttack(float delta)
		{
			if (EquippedInstance == null || EquippedInstance.IsEmpty())
			{
				return;
			}

			if (!InputAttack)
			{
				return;
			}

			GD.Print($"[HandleAttack] cooldown={EquippedInstance.CooldownRemaining:F2} reloading={EquippedInstance.IsReloading} charges={EquippedInstance.CurrentCharges}");

			EquippedInstance.Definition.Use(this, EquippedInstance);
		}

		private void HandleHotbarScroll()
		{
			if (!IsOwner || Inventory == null) return;

			int dir = 0;
			if (InputScrollNext) dir = 1;
			else if (InputScrollPrev) dir = -1;
			if (dir == 0) return;

			const int HotbarSize = 8;
			int current = Inventory.GetEquippedSlotIndex();
			if (current < 0) current = 0;

			for (int i = 1; i <= HotbarSize; i++)
			{
				int next = ((current + dir * i) % HotbarSize + HotbarSize) % HotbarSize;
				var slot = Inventory.GetSlot(next);
				if (slot != null && !slot.IsEmpty())
				{
					Inventory.EquipItem(next);
					return;
				}
			}
		}

		private void HandleReload(float delta)
		{
			if (EquippedInstance == null || EquippedInstance.IsEmpty())
			{
				return;
			}

			var chargesProp = EquippedInstance.Properties.OfType<ChargesProperty>().FirstOrDefault();
			if (chargesProp == null || chargesProp.InfiniteCharges)
			{
				return;
			}

			if (!EquippedInstance.IsReloading &&
				EquippedInstance.CurrentCharges < chargesProp.MaxCharges &&
				ReloadPending)
			{
				ReloadPending = false;
				int needed = chargesProp.MaxCharges - EquippedInstance.CurrentCharges;
				int taken  = Inventory?.RemoveAmmoByChargeType(chargesProp.ChargeType, needed) ?? 0;
				EquippedInstance.FinishReload(taken);
			}

			if (InputReload && EquippedInstance.CanReload())
			{
				EquippedInstance.StartReload();
				ReloadPending = true;
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

            if (InputJump && IsOnFloor())
			{
				v.Y = JumpVelocity;

                GD.Print("[HandleMovement] Pulando");
            }

            if (InputX != 0)
			{
				v.X = InputX * Speed;
            }
            else
			{
				v.X = Mathf.MoveToward(v.X, 0, Speed);
            }

            Velocity = v;

			MoveAndSlide();
		}
		
		private void OnItemEquipped(int slotIndex)
		{
			if (EquippedInstance != null && EquippedInstance.Definition != null)
			{
				EquippedInstance.Definition.OnUnequip(this, EquippedInstance);
			}

			var slot = Inventory?.GetSlot(slotIndex);

			if (slot == null || slot.IsEmpty())
			{
				return;
			}

			EquippedInstance = slot as ItemRechargeableInstance;

			if (EquippedInstance == null)
			{
				return;
			}

			var chargesProp = slot.Properties.OfType<ChargesProperty>().FirstOrDefault();

			EquippedInstance.CurrentCharges = chargesProp != null ? chargesProp.MaxCharges : 0;
			ReloadPending = false;

			EquippedInstance.Definition.OnEquip(this, EquippedInstance);
		}

		#endregion
	}
}

