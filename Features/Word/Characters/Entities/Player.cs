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
using Jogo25D.UI;
using Jogo25D.Utils.GodotDictionaryParser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Characters
{
	public partial class Player : CharacterBody2D
	{
		#region Events 

		[Signal]
		public delegate void InventoryChangedEventHandler();

		[Signal]
		public delegate void ItemEquippedEventHandler(long instanceId);

		[Signal]
		public delegate void EffectsChangedEventHandler();

		[Signal]
		public delegate void BuffsChangedEventHandler();

		[Signal]
		public delegate void AbilitiesChangedEventHandler();

		#endregion

		#region Properties

		public long PeerId { get; set; } = 1;
		public float Gravity { get; set; }
		public PlayerData Data { get; set; } = new PlayerData();
		public bool Loaded { get; set;  }
		public string DisplayName { get; set; } = "";

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

			if (IsAuthoritative())
			{
				Data ??= new PlayerData();
				Data.Inventory ??= new InventoryData();

				var startingWeapon = ItemDB.CreateInstance("bow_starting2");

				GiveItem(startingWeapon);

				var startingMeleeWeapon = ItemDB.CreateInstance("sword_starting");

				GiveItem(startingMeleeWeapon);

				var startingPoisonFlask = ItemDB.CreateInstance("poison_flask");

				startingPoisonFlask.Quantity = 20;

				GiveItem(startingPoisonFlask);

				Data.EquippedItemId = startingWeapon.InstanceId;

				foreach (var actionId in ActionDB.GetAllIds())
				{
					GiveAbility(actionId);
				}
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

			for (int i = Data.Effects.Count - 1; i >= 0; i--)
			{
				var effect = Data.Effects[i];

				if (effect == null)
				{
					Data.Effects.RemoveAt(i);

					continue;
				}

				if (effect.ApplyToOwner)
				{
					EffectDB.Get(effect.Id)?.Tick(this, effect, dt);

					if (effect.Expired)
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

		public virtual void ReceiveDamage(DamageInfo damage)
		{
			var resistanceFactor = 0f;

			if (IsAuthoritative())
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

		#region Core - Damage popup

		public void ShowDamagePopup(int amount)
		{
			if (amount <= 0)
			{
				return;
			}

			DamagePopupOverlayUI.Instance?.ShowDamagePopup(this, amount);
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

				if (IsOwner())
				{
					var needed = chargesProp.MaxCharges - data.CurrentCharges;

					FinishReloadRequest(data.InstanceId, chargesProp.ChargeItemId, needed);
				}
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

		public void GiveEffect(string effectId)
		{
			if (string.IsNullOrEmpty(effectId) || Data?.Effects == null)
			{
				return;
			}

			Data.Effects.Add(EffectDB.CreateInstance(effectId));

			EmitSignal(SignalName.EffectsChanged);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void AddEffectReceive(string effectId)
		{
			GiveEffect(effectId);
		}

		public void AddEffectRequest(string effectId)
		{
			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
			{
				AddEffectReceive(effectId);

				return;
			}

			RpcId(1, nameof(AddEffectReceive), effectId);
		}

		#endregion

		#region Core - Buffs system

		public void GiveBuff(BasePropertyData buff)
		{
			if (buff == null || Data?.Buffs == null)
			{
				return;
			}

			if (buff.InstanceId <= 0)
			{
				buff.InstanceId = BasePropertyData.NextInstanceId();
			}

			Data.Buffs.Add(buff);

			EmitSignal(SignalName.BuffsChanged);
		}

		public void RemoveBuff(long instanceId)
		{
			if (Data?.Buffs == null || instanceId <= 0)
			{
				return;
			}

			for (int i = 0; i < Data.Buffs.Count; i++)
			{
				if (Data.Buffs[i]?.InstanceId == instanceId)
				{
					Data.Buffs.RemoveAt(i);

					EmitSignal(SignalName.BuffsChanged);

					return;
				}
			}
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void AddBuffReceive(Godot.Collections.Dictionary data)
		{
			GiveBuff(GodotDictionaryParser.ToResource<BasePropertyData>(data));
		}

		public void AddBuffRequest(BasePropertyData buff)
		{
			var data = GodotDictionaryParser.ToDictionary(buff);

			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
			{
				AddBuffReceive(data);

				return;
			}

			Rpc(nameof(AddBuffReceive), data);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void RemoveBuffReceive(long instanceId)
		{
			RemoveBuff(instanceId);
		}

		public void RemoveBuffRequest(long instanceId)
		{
			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
			{
				RemoveBuffReceive(instanceId);

				return;
			}

			Rpc(nameof(RemoveBuffReceive), instanceId);
		}

		#endregion

		#region Core - Abilities system

		public void GiveAbility(string actionId)
		{
			if (string.IsNullOrEmpty(actionId) || Data?.UnlockedAbilities == null)
			{
				return;
			}

			Data.UnlockedAbilities.Add(ActionDB.CreateInstance(actionId, this));

			EmitSignal(SignalName.AbilitiesChanged);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void UnlockAbilityReceive(string actionId)
		{
			GiveAbility(actionId);
		}

		public void UnlockAbilityRequest(string actionId)
		{
			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
			{
				UnlockAbilityReceive(actionId);

				return;
			}

			RpcId(1, nameof(UnlockAbilityReceive), actionId);
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

		public bool IsAuthoritative()
		{
			return Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();
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

			var previousHealth = Data.CurrentHealth;

			Data.CurrentHealth = health;

			if (health < previousHealth)
			{
				ShowDamagePopup(previousHealth - health);
			}
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

		#region Core - Rpc - Item drop and pickup

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void DropItemReceive(long instanceId, int quantity)
		{
			if (!IsAuthoritative())
			{
				return;
			}

			var item = Inventory.FindItem(Data.Inventory, instanceId);

			if (item == null || quantity <= 0)
			{
				return;
			}

			var dropQuantity = Mathf.Min(quantity, item.Quantity);
			var dropData = (ItemDefinitionData)item.Duplicate(true);

			dropData.InstanceId = ItemDB.NextInstanceId();
			dropData.Quantity = dropQuantity;

			RemoveItemRequest(instanceId, dropQuantity);

			var dropOffset = new Vector2(Sprite.FlipH ? -40f : 40f, 0f);

			NetworkManager.SpawnWorldItemRequest(dropData, GlobalPosition + dropOffset);
		}

		public void DropItemRequest(long instanceId, int quantity)
		{
			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
			{
				DropItemReceive(instanceId, quantity);

				return;
			}

			RpcId(1, nameof(DropItemReceive), instanceId, quantity);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void PickupItemReceive(long worldItemId)
		{
			if (!IsAuthoritative())
			{
				return;
			}

			var worldItem = NetworkManager.FindWorldItem(worldItemId);

			if (worldItem == null)
			{
				return;
			}

			if (Inventory.AddItem(Data.Inventory, worldItem.Data))
			{
				EmitSignal(SignalName.InventoryChanged);
			}

			NetworkManager.RemoveWorldItemRequest(worldItemId);
		}

		public void PickupItemRequest(long worldItemId)
		{
			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
			{
				PickupItemReceive(worldItemId);

				return;
			}

			RpcId(1, nameof(PickupItemReceive), worldItemId);
		}

		#endregion

		#region Core - Rpc - Item charges

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void ConsumeChargeReceive(long instanceId)
		{
			var data = Inventory.FindItem(Data.Inventory, instanceId);

			if (data == null)
			{
				return;
			}

			ItemDB.Get(data.Id)?.ConsumeCharge(data);
		}

		public void ConsumeChargeRequest(long instanceId)
		{
			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
			{
				ConsumeChargeReceive(instanceId);

				return;
			}

			RpcId(1, nameof(ConsumeChargeReceive), instanceId);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void FinishReloadReceive(long instanceId, string chargeType, int needed)
		{
			var data = Inventory.FindItem(Data.Inventory, instanceId);

			if (data == null)
			{
				return;
			}

			var taken = RemoveAmmoByChargeType(chargeType, needed);

			ItemDB.Get(data.Id)?.FinishReload(taken, data);
		}

		public void FinishReloadRequest(long instanceId, string chargeType, int needed)
		{
			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
			{
				FinishReloadReceive(instanceId, chargeType, needed);

				return;
			}

			RpcId(1, nameof(FinishReloadReceive), instanceId, chargeType, needed);
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
