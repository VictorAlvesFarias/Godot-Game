using Godot;
using Jogo25D.Actions;
using Jogo25D.Characters;
using Jogo25D.Effects;
using Jogo25D.Features.Word.Characters.Resources;
using Jogo25D.Features.Word.Items.Resources;
using Jogo25D.Hitboxes;
using Jogo25D.Items;
using Jogo25D.Properties;
using Jogo25D.Systems;
using Jogo25D.Utils.GodotDictionaryParser;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Jogo25D.Characters
{
	public partial class Player : CharacterBody2D
	{
        #region Events 

        [Signal]
        public delegate void InventoryChangedEventHandler();

        [Signal]
        public delegate void ItemEquippedEventHandler(long instanceId);

        #endregion

        #region Properties

        public long PeerId { get; set; } = 1;
        public float Gravity { get; set; }
		public PlayerData Data { get; set; } = new PlayerData();
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

			if (Multiplayer.IsServer())
			{
				Data ??= new PlayerData();
				Data.Inventory ??= new InventoryData();

                var startingWeapon = ItemDB.CreateInstance("bow_starting2");

                GiveItem(startingWeapon);

                Data.EquippedItemId = startingWeapon.InstanceId;
            }

			Inventory.EnsureSize(Data.Inventory);

			if (Data.EquippedItemId > 0 && Inventory.FindItem(Data.Inventory, Data.EquippedItemId) != null)
			{
				GD.Print("[Player._Ready] Running equip item");

				EquipItemRequest(Data.EquippedItemId);
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

			if (Multiplayer.IsServer())
			{
				foreach (var buff in Data.Buffs)
				{
					if (buff is DamageResistencePropertyData r && r.DamageType == damage.Type)
					{
						resistanceFactor = System.Math.Max(resistanceFactor, r.ResistanceFactor);
					}
				}

				var critMultiplier = 1f + (GD.Randf() <= damage.CritChance ? damage.CritDamage : 0f);
				var finalDamage = (int)(damage.Amount * critMultiplier * (1f - resistanceFactor));

				if (Data.CurrentHealth > 0 || finalDamage >= 0)
				{
					SetHealthRequest(Mathf.Max(0, Data.CurrentHealth - finalDamage));
				}
			}

			if (Data.CurrentHealth <= 0)
			{
				Sprite.Play("dead");
			}
		}

		#endregion

		#region Core - Items system handlers

		public void HandleUseItem(float delta)
		{
			var data = Inventory.FindItem(Data.Inventory, Data.EquippedItemId);

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
            var data = Inventory.FindItem(Data.Inventory, Data.EquippedItemId);

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
			var currentSlot = Inventory.FindSlotIndex(Data.Inventory, Data.EquippedItemId);

			if (currentSlot < 0 || currentSlot >= hotbarSize)
			{
				currentSlot = 0;
			}

			for (int i = 1; i <= hotbarSize; i++)
			{
				var next = ((currentSlot + dir * i) % hotbarSize + hotbarSize) % hotbarSize;
				var slot = Inventory.GetSlot(Data.Inventory, next);

				if (slot != null)
				{
					EquipItemRequest(slot.InstanceId);

					return;
				}
			}
		}

		#endregion

		#region Core - Items system 

		public int CountAmmoByChargeType(string chargeType)
		{
			if (string.IsNullOrEmpty(chargeType))
			{
				return 0;
			}

			int count = 0;

			for (int i = 0; i < Data.Inventory.Size; i++)
			{
				var slot = Data.Inventory.Items[i];

				if (slot == null || slot.InstanceId == Data.EquippedItemId)
				{
					continue;
				}

				var chargesProp = slot.Properties.OfType<ChargesPropertyData>().FirstOrDefault();

				if (chargesProp != null && chargesProp.ChargeItemId == chargeType)
				{
					count += slot.Quantity;
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
				var slot = Data.Inventory.Items[i];

				if (slot == null || slot.InstanceId == Data.EquippedItemId)
				{
					continue;
				}

				var chargesProp = slot.Properties.OfType<ChargesPropertyData>().FirstOrDefault();

				if (chargesProp == null || chargesProp.ChargeItemId != chargeType)
				{
					continue;
				}

				int toRemove = Mathf.Min(quantity - removed, slot.Quantity);

				slot.Quantity -= toRemove;

				removed += toRemove;

				if (slot.Quantity <= 0)
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

		public void EquipItem(long instanceId)
		{
			var item = Inventory.FindItem(Data.Inventory, instanceId);

			if (item == null)
			{
				return;
			}

			Data.EquippedItemId = instanceId;

			EmitSignal(SignalName.ItemEquipped, instanceId);
		}

		#region Public API - Items query

		public ItemDefinitionData GetSlot(int index)
		{
			return Inventory.GetSlot(Data?.Inventory, index);
		}

		public ItemDefinitionData EquippedInstance => Inventory.FindItem(Data?.Inventory, Data?.EquippedItemId ?? 0);

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

        #region Core - Rpc - Stats


        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void EquipItemReceive(long instanceId)
        {
            this.EquipItem(instanceId);
        }

        public void EquipItemRequest(long instanceId)
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
            {
                EquipItemReceive(instanceId);

                return;
            }

            RpcId(1, nameof(EquipItemReceive), instanceId);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SetHealthReceive(int health)
        {
			GD.Print($"[Player.SetHealthReceive] - Tentando definir o valor da saude para o peer {PeerId} no {(Multiplayer.IsServer() ? "server" : "cliente")}");

			Data.CurrentHealth = health;
        }

        public void SetHealthRequest(int health)
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
            {
                SetHealthReceive(health);

                return;
            }

            Rpc(nameof(SetHealthReceive), health);
        }

        #endregion

        #region Core - Rpc - Iventory and items

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void AddItemReceive(Godot.Collections.Dictionary data)
        {
            GD.Print("[Inventory.AddItemReceive] Starting method");

			var item = GodotDictionaryParser.ToResource<ItemDefinitionData>(data);

            if (Inventory.AddItem(Data.Inventory, item))
            {
                EmitSignal(SignalName.InventoryChanged);
            }
        }

        public void AddItemRequest(ItemDefinitionData item)
        {
            GD.Print("[Inventory.AddItemRequest] Starting method");

			var data = GodotDictionaryParser.ToDictionary(item);

            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
            {
                AddItemReceive(data);

                return;
            }

            Rpc(nameof(AddItemReceive), data);
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
        public void MoveItemReceive(long instanceId, int toIndex)
        {
            GD.Print("[Inventory.MoveItemReceive] Starting method");

            if (Inventory.MoveItem(Data.Inventory, instanceId, toIndex))
            {
                EmitSignal(SignalName.InventoryChanged);
            }
        }

        public void MoveItemRequest(long instanceId, int toIndex)
        {
            GD.Print("[Inventory.MoveItemRequest] Starting method");

            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
            {
                MoveItemReceive(instanceId, toIndex);

                return;
            }

            Rpc(nameof(MoveItemReceive), instanceId, toIndex);
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
        public void RemoveItemReceive(long instanceId, int quantity)
        {
            GD.Print("[Inventory.RemoveItemReceive] Starting method");

            if (Inventory.RemoveItem(Data.Inventory, instanceId, quantity))
            {
                EmitSignal(SignalName.InventoryChanged);
            }
        }

        public void RemoveItemRequest(long instanceId, int quantity)
        {
            GD.Print("[Inventory.RemoveItemRequest] Starting method");

            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
            {
                RemoveItemReceive(instanceId, quantity);

                return;
            }

            Rpc(nameof(RemoveItemReceive), instanceId, quantity);
        }

        #endregion

        #region Core - Rpc - Validate and sync position

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

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
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

		#endregion
	}
}
