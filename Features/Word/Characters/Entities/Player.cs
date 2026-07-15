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
using Jogo25D.Features.Word.Characters.Resources;
using Jogo25D.Features.Word.Items.Resources;

namespace Jogo25D.Characters
{
	public partial class Player : CharacterBody2D
	{
        #region Events 

        [Signal]
        public delegate void InventoryChangedEventHandler();

        [Signal]
        public delegate void ItemEquippedEventHandler(int slotIndex);

        #endregion

        #region Properties

        [Export]
        public long PeerId { get; set; } = 1;

        [Export]
        public float Gravity { get; set; }

		[Export]
		public PlayerData Data { get; set; } = new PlayerData();

		[Export]
		public bool Loaded { get; set;  }

        #endregion

        #region Systems

		public Inventory Inventory { get; set; } = new Inventory();

        #endregion

		#region Node references

		private WorldManager NetworkManager { get; set; }
		public AnimatedSprite2D Sprite { get; set; }
		public GroundIndicator GroundMarker { get; set; }
		public AimIndicator AimIndicator { get; set; }
		public PlayerInput Input { get; set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
		{
			GD.Print("[Player._Ready] Starting method");
			GD.Print("[Player._Ready] Adding players to group");

			AddToGroup("players");

			GD.Print("[Player._Ready] Initializating ItemDB");

			ItemDB.Initialize();

			GD.Print("[Player._Ready] Trying get Nodes");

			Gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
			NetworkManager = GetTree().Root.GetNode<WorldManager>(WorldManager.DEFAULT_NODE_PATH);
			Sprite = GetNodeOrNull<AnimatedSprite2D>("Sprite");
			Input = GetNodeOrNull<PlayerInput>("Systems/PlayerInput");
			AimIndicator = GetNodeOrNull<AimIndicator>("Systems/AimIndicator");
			GroundMarker = GetNodeOrNull<GroundIndicator>("Systems/GroundMarker");

			GD.Print("[Player._Ready] Setting default states");

			Sprite.Play("idle");

			GD.Print("[Player._Ready] Adding animation events");

			Sprite.AnimationFinished += () =>
			{
				if (Sprite.Animation == "dead")
				{
					Sprite.Stop();

					NetworkManager.ResetPlayerClientRequest();
				}
			};

			GD.Print("[Player._Ready] Seting starter slot");

			Data ??= new PlayerData();
			Data.Inventory ??= new InventoryData();

			Inventory.EnsureSize(Data.Inventory);

			var equipped = Data.EquippedSlotIndex;

			if (equipped >= 0 && equipped < Data.Inventory.Size && Data.Inventory.Items[equipped] != null)
			{
				GD.Print("[Player._Ready] Running equip item");

				EquipItemRequest(equipped);
			}
		}

		public override void _PhysicsProcess(double delta)
		{
			var dt = (float)delta;

			//TODO: Player process effects
			for (int i = Data.Effects.Count - 1; i >= 0; i--)
			{
				if (Data.Effects[i].ApplyToOwner)
				{
					Data.Effects[i].Tick(this, dt);

					if (Data.Effects[i].Expired)
					{
						Data.Effects.RemoveAt(i);
					}
				}
			}

			//Action process
			foreach (var action in Data.UnlockedAbilities)
			{
				if (action == null)
				{
					continue;
				}

				ActionDB.Get(action.Id)?.Update(dt, this, action);
			}

			foreach(var item in Data.Inventory.Items)
			{
				if (item == null)
				{
					continue;
				}

				ItemDB.Get(item.Id)?.Update(dt, item);
			}

			if (IsOwner())
			{
				HandleHotbarScroll();
			}

			TestPositionRequest(Position);
			HandleMovement(dt);
			HandleUseItem(dt);
			HandleReload(dt);
			UpdateAnimation();
		}

		#endregion

		#region Core - Damage system

		public void ReceiveDamage(DamageInfo damage)
		{
			var resistanceFactor = 0f;

			foreach (var buff in Data.Buffs)
			{
				if (buff is DamageResistencePropertyData r && r.DamageType == damage.Type)
				{
					resistanceFactor = System.Math.Max(resistanceFactor, r.ResistanceFactor);
				}
			}

			var critMultiplier = 1f + (GD.Randf() <= damage.CritChance ? damage.CritDamage : 0f);
			var finalDamage = (int)(damage.Amount * critMultiplier * (1f - resistanceFactor));

			if (Data.CurrentHealth <= 0 || finalDamage <= 0)
			{
				return;
			}

			Data.CurrentHealth = Mathf.Max(0, Data.CurrentHealth - finalDamage);

			if (Data.CurrentHealth <= 0)
			{
				Sprite.Play("dead");
			}
		}

		#endregion

		#region Core - Items system handlers

		public void HandleUseItem(float delta)
		{
			var data = GetSlot(Data.EquippedSlotIndex);

            if (data == null)
			{
				return;
			}

			if (!Input.Attack)
			{
				return;
			}

			var def = ItemDB.Get(data.Id);

			GD.Print($"[HandleAttack] cooldown={data.CooldownRemainingTimer:F2} reloading={def.IsReloading(data)} charges={data.CurrentCharges}");

			def.Use(this, data);
		}

		public void HandleReload(float delta)
		{
            var data = GetSlot(Data.EquippedSlotIndex);

            if (data == null)
            {
                return;
            }

            var def = ItemDB.Get(data.Id);
            var chargesProp = data.Properties.OfType<ChargesPropertyData>().FirstOrDefault();

			if (chargesProp == null || chargesProp.InfiniteCharges)
			{
				return;
			}

			if (!def.IsReloading(data) && data.CurrentCharges < chargesProp.MaxCharges && Data.ReloadPending)
			{
				Data.ReloadPending = false;

				var needed = chargesProp.MaxCharges - data.CurrentCharges;
				var taken = RemoveAmmoByChargeType(chargesProp.ChargeItemId, needed);

                def.FinishReload(taken, data);
			}

			if (Input.Reload && def.CanReload(data))
			{
                def.TriggerReloadTimer(data);

				Data.ReloadPending = true;
			}
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

			if (Data.EquippedSlotIndex < 0)
			{
				Data.EquippedSlotIndex = 0;
			}

			for (int i = 1; i <= hotbarSize; i++)
			{
				var next = ((Data.EquippedSlotIndex + dir * i) % hotbarSize + hotbarSize) % hotbarSize;
				var slot = Inventory.GetSlot(Data.Inventory, next);

				if (slot != null)
				{
					if (Multiplayer != null && Multiplayer.HasMultiplayerPeer())
					{
						EquipItemRequest(next);
					}
					else
					{
						EquipItemRequest(next);
					}

					return;
				}
			}
		}

		#endregion

		#region Core - Items system 

		public void RemoveItemRequest(int slotIndex, int quantity)
		{
			GD.Print("[Inventory.RemoveItemRequest] Starting method");

			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
			{
				RemoveItemReceive(slotIndex, quantity);

				return;
			}

			Rpc(nameof(RemoveItemReceive), slotIndex, quantity);
		}

		public int CountAmmoByChargeType(string chargeType)
		{
			if (string.IsNullOrEmpty(chargeType))
			{
				return 0;
			}

			int count = 0;

			for (int i = 0; i < Data.Inventory.Size; i++)
			{
				if (i == Data.EquippedSlotIndex)
				{
					continue;
				}

				if (Data.Inventory.Items[i] == null)
				{
					continue;
				}

				var chargesProp = Data.Inventory.Items[i].Properties.OfType<ChargesPropertyData>().FirstOrDefault();

				if (chargesProp != null && chargesProp.ChargeItemId == chargeType)
				{
					count += Data.Inventory.Items[i].Quantity;
				}
			}
			return count;
		}

		public int RemoveAmmoByChargeType(string chargeType, int quantity)
		{
			if (string.IsNullOrEmpty(chargeType) || quantity <= 0)
			{
				return 0;
			}

			int removed = 0;

			for (int i = 0; i < Data.Inventory.Size && removed < quantity; i++)
			{
				if (Data.Inventory.Items[i] == null)
				{
					continue;
				}

				var chargesProp = Data.Inventory.Items[i].Properties.OfType<ChargesPropertyData>().FirstOrDefault();

				if (chargesProp == null || chargesProp.ChargeItemId != chargeType)
				{
					continue;
				}

				int toRemove = Mathf.Min(quantity - removed, Data.Inventory.Items[i].Quantity);

				Data.Inventory.Items[i].Quantity -= toRemove;

				removed += toRemove;

				if (Data.Inventory.Items[i].Quantity <= 0)
				{
					Data.Inventory.Items[i] = null;
				}
			}

			if (removed > 0)
			{
				EmitSignal(SignalName.InventoryChanged);
			}

			return removed;
		}

		public void EquipItem(int slotIndex)
		{
			if (slotIndex < 0 || slotIndex >= Data.Inventory.Size)
			{
				return;
			}

			var slot = Data.Inventory.Items[slotIndex];

			if (slot == null)
			{
				return;
			}

			Data.EquippedSlotIndex = slotIndex;

			EmitSignal(SignalName.ItemEquipped, slotIndex);

			return;
		}

		#region Public API - Items query

		public ItemDefinitionData GetSlot(int index)
		{
			return Inventory.GetSlot(Data?.Inventory, index);
		}

		public ItemDefinitionData EquippedInstance => GetSlot(Data?.EquippedSlotIndex ?? -1);

		public void GiveItem(ItemDefinitionData item)
		{
			if (Data?.Inventory == null)
			{
				return;
			}

			if (Inventory.AddItem(Data.Inventory, item))
			{
				EmitSignal(SignalName.InventoryChanged);
			}
		}

		#endregion

		#endregion

		#region Animation 

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

		#endregion

		#region Core - Effects system

		public void AddEffect(EffectDefinition definition)
		{
			if (definition == null)
			{
				return;
			}

			Data.Effects.Add(definition.Clone());
		}

		#endregion

		#region Core - Movement handlers

		public void HandleMovement(float delta)
		{
			if (!Data.CanUpdateMovement)
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
				v.Y = Data.JumpVelocity;

				GD.Print("[HandleMovement] Pulando");
			}

			if (Input.MoveX != 0)
			{
				v.X = Input.MoveX * Data.Speed;
			}
			else
			{
				v.X = Mathf.MoveToward(v.X, 0, Data.Speed);
			}

			Velocity = v;

			MoveAndSlide();
		}

		#endregion

		#region Utils

		public bool IsOwner()
		{
			return PeerId == Multiplayer.GetUniqueId();
		}

		public bool IsServer()
		{
			return PeerId == 1;
		}

		#endregion

		#region Core - Rpc - Items system

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void EquipItemReceive(int slotIndex)
		{
			this.EquipItem(slotIndex);
		}

		public void EquipItemRequest(int slotIndex)
		{
			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
			{
				EquipItemReceive(slotIndex);

				return;
			}

			RpcId(1, nameof(EquipItemReceive), slotIndex);
		}

		#endregion

		#region Core - Rpc - Movemet system

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
		public void TestPositionReceive(Vector2 pos)
		{
			var maxTolerance = 50.0f;
			var distance = GlobalPosition.DistanceTo(pos);
			var sendToOwner = false;

			if (distance > maxTolerance && !IsServer())
			{
				GD.Print($"[Sync] Diferença muito grande detectada ({distance:F2}). Sincronizando cliente.");

				sendToOwner = true;
			}
			else if (!IsServer())
			{
				// Opcional: Se a distância for aceitável, o servidor pode assumir a posição do cliente para ficar mais fluído
				GlobalPosition = pos;
			}

			SyncPositionRequest(GlobalPosition, sendToOwner);
		}

		public void TestPositionRequest(Vector2 pos)
		{
			if (Multiplayer.IsServer())
			{
				return;
			}

			if (!IsOwner() && !IsServer())
			{
				return;
			}

			// O cliente envia sua posição atual diretamente para o servidor (Peer ID 1)
			RpcId(1, nameof(TestPositionReceive), pos);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void SyncPositionReceive(Vector2 pos, bool sendToOwner)
		{
			// O cliente recebe a posição imposta pelo servidor e corrige sua localização
			if (Multiplayer.IsServer())
			{
				return;
			}

			if (!sendToOwner && !IsServer())
			{
				return;
			}

			GlobalPosition = pos;
		}

		public void SyncPositionRequest(Vector2 pos, bool sendToOwner)
		{
			// Apenas o servidor executa isso: envia a posição oficial apenas para o dono deste Player
			//RpcId((int)PeerId, nameof(SyncPositionReceive), pos);

			// Nota: Se quiser que TODOS vejam a posição corrigida ao mesmo tempo, use:
			Rpc(nameof(SyncPositionReceive), pos, sendToOwner);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void AddItemReceive(ItemDefinitionData item)
		{
			GD.Print("[Inventory.AddItemReceive] Starting method");

			if(Inventory.AddItem(Data.Inventory, item))
			{
                EmitSignal(SignalName.InventoryChanged);
            }
        }

		public void AddItemRequest(ItemDefinitionData item)
		{
			GD.Print("[Inventory.AddItemRequest] Starting method");

			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
			{
				AddItemReceive(item);

				return;
			}

			Rpc(nameof(AddItemReceive), item);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
		public void SwapSlotsReceive(int fromIndex, int toIndex)
		{
			GD.Print("[Inventory.SwapSlotsReceive] Starting method");

			if(Inventory.SwapSlots(Data.Inventory, fromIndex, toIndex))
			{
                EmitSignal(SignalName.InventoryChanged);
            }
        }

		public void SwapSlotsRequest(int fromIndex, int toIndex)
		{
			GD.Print("[Inventory.SwapSlotsRequest] Starting method");

			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
			{
				SwapSlotsReceive(fromIndex, toIndex);

				return;
			}

			Rpc(nameof(SwapSlotsReceive), fromIndex, toIndex);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
		public void RemoveItemReceive(int slotIndex, int quantity)
		{
			GD.Print("[Inventory.RemoveItemReceive] Starting method");

			if(Inventory.RemoveItem(Data.Inventory, slotIndex, quantity))
			{
                EmitSignal(SignalName.InventoryChanged);
            }
        }

		#endregion
	}
}
