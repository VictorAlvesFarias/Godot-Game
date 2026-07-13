using Godot;
using Jogo25D.Characters;
using Jogo25D.Systems;
using Jogo25D.Items;
using Jogo25D.Properties;
using Jogo25D.Effects;
using Jogo25D.Actions;
using Jogo25D.Hitboxes;
using System;
using System.Collections.Generic;
using System.Linq;

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
		private Vector2 TargetPosition { get; set; }
		public List<EffectDefinition> Effects { get; set; } = new();
		public List<BaseProperty> Buffs { get; set; } = new();
		public List<ActionInstance> UnlockedAbilities { get; set; } = new List<ActionInstance>();
		public ItemInstance EquippedInstance { get; set; }
		public ItemInstance[] Items { get; set; } = Array.Empty<ItemInstance>();
		public ItemDefinition EquippedDefinition { get; set; }
		public WorldManager NetworkManager { get; set; }
		public Inventory Inventory { get; set; }
		public PlayerInput Input { get; set; }
		public AimIndicator AimIndicator { get; set; }
		public GroundIndicator GroundMarker { get; set; }
		public AnimatedSprite2D Sprite { get; set; }

		public override void _Ready()
		{
			AddToGroup("players");

			ItemDB.Initialize();

			Gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
			
			NetworkManager = GetTree().Root.GetNode<WorldManager>(WorldManager.DEFAULT_NODE_PATH);

			Sprite = GetNodeOrNull<AnimatedSprite2D>("Sprite");
			Input = GetNodeOrNull<PlayerInput>("Systems/PlayerInput");
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

			TargetPosition = GlobalPosition;

			Sprite.AnimationFinished += () =>
			{
				if (Sprite.Animation == "dead")
				{
					Sprite.Stop();

					NetworkManager.ResetPlayerClientRequest();
				}
			};

			Inventory.AddItem(ItemDB.Get("bow_starting2"));
			Inventory.AddItem(ItemDB.Get("sword_starting"));

			if (EquippedSlotIndex < 0)
			{
				for (int i = 0; i < Items.Length; i++)
				{
					var slot = Items[i];

					if (slot == null || slot.IsEmpty() || slot.Definition == null || !slot.Definition.IsEquippable)
					{
						continue;
					}

					EquippedSlotIndex = i;
					break;
				}
			}

			if (EquippedSlotIndex >= 0)
			{
				Inventory.EquipItem(EquippedSlotIndex);
			}
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
			var dt = (float)delta;

			//TODO: Player process effects
			for (int i = Effects.Count - 1; i >= 0; i--)
			{
				if (Effects[i].ApplyToOwner)
				{
					Effects[i].Tick(this, dt);
			
					if (Effects[i].Expired)
					{
						Effects.RemoveAt(i);
					}
				}
			}

			//Action process
			foreach (var action in UnlockedAbilities)
			{
				action.Update(dt);
			}

			EquippedInstance?.Update(dt);

			if (IsOwner())
			{
				HandleHotbarScroll();
			}

			if (Multiplayer.IsServer())
			{
				// Toda lógica de jogo roda apenas no servidor
				HandleMovement(dt);
				HandleAttack(dt);
				HandleReload(dt);
				UpdateAnimation();

				Rpc(nameof(SyncPosition), GlobalPosition, Velocity);
				Rpc(nameof(SyncAnimation), (string)Sprite.Animation, Sprite.FlipH);
			}
			else
			{
                HandleReload(dt);
                HandleAttack(dt);

				// Outros clientes apenas interpolam para a posição do servidor
				var dist = GlobalPosition.DistanceTo(TargetPosition);

				if (dist > 300f)
				{
					GlobalPosition = TargetPosition;
				}
				else
				{
                    if (GlobalPosition.DistanceTo(TargetPosition) < 1f)
                        GlobalPosition = TargetPosition;
                    else
                        GlobalPosition = GlobalPosition.Lerp(TargetPosition, 15f * dt);
                }
			}
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
		public void SyncPosition(Vector2 pos, Vector2 vel)
		{
			TargetPosition = pos;
			Velocity = vel;
		}

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void SyncAnimation(string animName, bool flipH)
		{
			Sprite.FlipH = flipH;

			if (Sprite.Animation != animName)
			{
				Sprite.Play(animName);
			}
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void SyncHealth(int currentHealth)
		{
			CurrentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
		}

		public void TakeDamage(int damage)
		{
         if (CurrentHealth <= 0 || damage <= 0)
			{
				return;
			}

			CurrentHealth = Mathf.Max(0, CurrentHealth - damage);

			if (Multiplayer != null && Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
			{
				Rpc(nameof(SyncHealth), CurrentHealth);
			}

            if (CurrentHealth <= 0)
			{
				Sprite.Play("dead");
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

		public void UpdateAnimation()
		{
			if (Velocity.X != 0)
			{
				Sprite.FlipH = Velocity.X < 0;
			}

			if (Sprite.Animation == "melee" && Sprite.IsPlaying())
			{
				return;
			}

			if (!IsOnFloor())
			{
				if (Velocity.Y < 0)
				{
					if (Sprite.Animation != "jump")
					{
						GD.Print($"[Player.UpdateAnimation] {Sprite.Animation} -> jump");
						Sprite.Play("jump");
					}
				}
				else
				{
					if (Sprite.Animation != "falling" && Sprite.Animation != "dash")
					{
						GD.Print($"[Player.UpdateAnimation] {Sprite.Animation} -> falling");
						Sprite.Play("falling");
					}
				}
				return;
			}

			if (Sprite.Animation == "dash" && Sprite.IsPlaying())
				return;

			if (Velocity.X != 0)
			{
				if (Sprite.Animation != "run")
				{
					GD.Print($"[Player.UpdateAnimation] {Sprite.Animation} -> run");
					Sprite.Play("run");
				}
			}
			else
			{
				if (Sprite.Animation != "idle")
				{
					GD.Print($"[Player.UpdateAnimation] {Sprite.Animation} -> idle");

					Sprite.Play("idle");
				}
			}
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

			var dir = Input.ScrollDirection;
			
			if (dir == 0)
			{
				return;
			}

			var hotbarSize = 8;
			var current = Inventory.GetEquippedSlotIndex();

			if (current < 0)
			{
				current = 0;
			}

			for (int i = 1; i <= hotbarSize; i++)
			{
				var next = ((current + dir * i) % hotbarSize + hotbarSize) % hotbarSize;
				var slot = Inventory.GetSlot(next);

				if (slot != null && !slot.IsEmpty())
				{
					if (Multiplayer != null && Multiplayer.HasMultiplayerPeer())
					{
						Inventory.Rpc(nameof(Inventory.EquipItem), next);
					}
					else
					{
						Inventory.EquipItem(next);
					}

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

			if (!EquippedInstance.IsReloading && EquippedInstance.CurrentCharges < chargesProp.MaxCharges && ReloadPending)
			{
				ReloadPending = false;

				var needed = chargesProp.MaxCharges - EquippedInstance.CurrentCharges;
				var taken = Inventory?.RemoveAmmoByChargeType(chargesProp.ChargeItemId, needed) ?? 0;
				
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
			if (!Multiplayer.IsServer())
			{
				return;
			}

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
			return PeerId == Multiplayer.GetUniqueId();
		}
	}
}
