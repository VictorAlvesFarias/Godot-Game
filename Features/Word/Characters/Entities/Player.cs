using Godot;
using Jogo25D.Characters;
using Jogo25D.Systems;
using Jogo25D.Items;
using Jogo25D.Properties;
using Jogo25D.Effects;
using Jogo25D.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;

namespace Jogo25D.Characters
{
	public partial class Player : CharacterBody2D
	{
		public long PeerId { get; set; } = 1;
		public float Speed { get; set; } = 300.0f;
		public float JumpVelocity { get; set; } = -750.0f;
		public float Gravity { get; set; }
		public int MaxHealth { get; set; } = 50;
		public int CurrentHealth { get; set; } = 50;
		public bool CanUpdateMovement { get; set; } = true;
		public bool ReloadPending { get; set; } = true;
		public int EquippedSlotIndex { get; set; } = -1;
		public List<EffectDefinition> Effects { get; set; } = new();
		public List<BaseProperty> Buffs { get; set; } = new();
		public List<ActionInstance> UnlockedAbilities { get; set; } = new List<ActionInstance>();
		public ItemInstance EquippedInstance { get; set; }
		public ItemInstance[] Items { get; set; } = Array.Empty<ItemInstance>();
		public ItemDefinition EquippedDefinition { get; set; }
		public ControlledInputs Input { get; set; } = new();

		public WorldManager NetworkManager { get; set; }
		public InputManager InputManager { get; set; }

		public Inventory Inventory { get; set; }
		public AimIndicator AimIndicator { get; set; }
		public GroundIndicator GroundMarker { get; set; }

		public AnimatedSprite2D Sprite { get; set; }

		public override void _Ready()
		{
			AddToGroup("players");

			Gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
			
			NetworkManager = GetTree().Root.GetNode<WorldManager>(WorldManager.DEFAULT_NODE_PATH);
			InputManager = GetTree().Root.GetNode<InputManager>(InputManager.DEFAULT_NODE_PATH);

			Sprite = GetNodeOrNull<AnimatedSprite2D>("Visual/Sprite");
			AimIndicator = GetNodeOrNull<AimIndicator>("Systems/AimIndicator");
			GroundMarker = GetNodeOrNull<GroundIndicator>("Systems/GroundMarker");
			Inventory = GetNodeOrNull<Inventory>("Systems/Inventory");

			Items = new ItemInstance[Inventory.INVENTORY_SIZE];

			for (int i = 0; i < Inventory.INVENTORY_SIZE; i++)
			{
				Items[i] = new ItemInstance();
			}

			ActionDB.Initialize();
			
			UnlockedAbilities.Add(ActionDB.CreateInstance("dash", this));
			UnlockedAbilities.Add(ActionDB.CreateInstance("fireball", this));
			UnlockedAbilities.Add(ActionDB.CreateInstance("ground_strike", this));

			Inventory.ItemEquipped += OnItemEquipped;

			Sprite.Play("idle");

			Sprite.AnimationFinished += () =>
			{
				if (Sprite.Animation == "dead")
				{
					Sprite.Stop();

					NetworkManager.ResetPlayerClientRequest();
				}
			};
		}

		public override void _ExitTree()
		{
			if (Inventory != null)
			{
				Inventory.ItemEquipped -= OnItemEquipped;
			}

			EquippedInstance = null;

			base._ExitTree();
		}

		public override void _PhysicsProcess(double delta)
		{
			if (Multiplayer.IsServer())
			{
				Rpc(nameof(SyncPosition), GlobalPosition);
			}

			foreach (var effect in Effects.Where(e => e.ApplyToOwner))
			{
				effect.Tick(this, (float)delta);

				if (effect.Expired)
				{
					Effects.Remove(effect);
				}
			}

			foreach (var action in UnlockedAbilities)
			{
				action.Update((float)delta);
			}

			EquippedInstance?.Update((float)delta);

			UpdateAnimation();

			HandleInput();
			HandleHotbarScroll();
			HandleMovement((float)delta);
			HandleAttack((float)delta);
			HandleReload((float)delta);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
		public void SetServerInput(ControlledInputs input)
		{
			Input = input;
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
		public void SyncPosition(Vector2 pos)
		{
			GlobalPosition = pos;
		}

		public void TakeDamage(int damage)
		{
			if (CurrentHealth <= 0)
			{
				return;
			}

			Sprite.Play("dead");
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

		public void UpdateAnimation()
		{
			if (!IsOnFloor())
			{
				if (Velocity.Y < 0)
				{
					if (Sprite.Animation != "jump")
						Sprite.Play("jump");
				}
				else
				{
					if (Sprite.Animation != "falling")
						Sprite.Play("falling");
				}

				return;
			}

			if (Velocity.X != 0)
			{
				if (Sprite.Animation != "run")
					Sprite.Play("run");
			}
			else
			{
				if (Sprite.Animation != "idle")
					Sprite.Play("idle");
			}

			if (Velocity.X != 0)
				Sprite.FlipH = Velocity.X < 0;
		}

		public void HandleInput()
		{
			if (!IsOwner())
			{
				return;
			}

			Input = InputManager.Current;
			Input.MousePosition = GetGlobalMousePosition();

			Rpc(nameof(SetServerInput), Input);
		}

		public void HandleAttack(float delta)
		{
			if (EquippedInstance == null || EquippedInstance.IsEmpty())
			{
				return;
			}

			if (!Input.Attack)
			{
				return;
			}

			GD.Print($"[HandleAttack] cooldown={EquippedInstance.CooldownRemaining:F2} reloading={EquippedInstance.IsReloading} charges={EquippedInstance.CurrentCharges}");

			EquippedInstance.Definition.Use(this, EquippedInstance);
		}

		public void HandleHotbarScroll()
		{
			if (!IsOwner() || Inventory == null)
			{
				return;
			}

			int dir = 0;
			if (Input.ScrollNext)
			{
				dir = 1;
			}
			else if (Input.ScrollPrev)
			{
				dir = -1;
			}
			if (dir == 0)
			{
				return;
			}

			const int HotbarSize = 8;
			int current = Inventory.GetEquippedSlotIndex();
			if (current < 0)
			{
				current = 0;
			}

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

		public void HandleReload(float delta)
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
				int taken = Inventory?.RemoveAmmoByChargeType(chargesProp.ChargeItemId, needed) ?? 0;
				EquippedInstance.FinishReload(taken);
			}

			if (Input.Reload && EquippedInstance.CanReload())
			{
				EquippedInstance.StartReload();
				ReloadPending = true;
			}
		}

		public void HandleMovement(float delta)
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

			if (Input.Jump && IsOnFloor())
			{
				v.Y = JumpVelocity;

				GD.Print("[HandleMovement] Pulando");
			}

			if (Input.MoveX != 0)
			{
				v.X = Input.MoveX * Speed;
			}
			else
			{
				v.X = Mathf.MoveToward(v.X, 0, Speed);
			}

			Velocity = v;

			MoveAndSlide();
		}
		
		public void OnItemEquipped(int slotIndex)
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

			EquippedInstance = slot;

			var chargesProp = EquippedInstance.Properties.OfType<ChargesProperty>().FirstOrDefault();
			EquippedInstance.CurrentCharges = chargesProp != null ? chargesProp.MaxCharges : 0;
			ReloadPending = false;

			EquippedInstance.Definition.OnEquip(this, EquippedInstance);
		}

		public bool IsOwner()
		{
			return GetMultiplayerAuthority() == Multiplayer.GetUniqueId();
		}
	}
}
