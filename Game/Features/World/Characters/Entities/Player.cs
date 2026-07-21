using Godot;
using Jogo25D.Actions;
using Jogo25D.Characters;
using Jogo25D.Effects;
using Jogo25D.Features.World.Characters.Resources;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Features.World.Properties.Resources;
using Jogo25D.Features.World.Resolver.Singletons;
using Jogo25D.Hitboxes;
using Jogo25D.Items;
using Jogo25D.Properties;
using Jogo25D.SkillTree;
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
		public delegate void AbilitiesChangedEventHandler();

		#endregion

		#region Dinamic properties

		public long PeerId { get; set; } = 1;
		public float Gravity { get; set; }
        public bool Loaded { get; set;  }
		public string DisplayName { get; set; } = "";
		public PlayerData Data { get; set; } = new PlayerData();
        public Godot.Collections.Array<BasePropertyData> Properties { get; set; } = new();
        public Godot.Collections.Array<EffectDefinitionData> CurrentEffects { get; set; } = new();
        public Godot.Collections.Array<ActionDefinitionData> UnlockedAbilities { get; set; } = new Godot.Collections.Array<ActionDefinitionData>();

		#endregion

		#region Knockback

		protected const float KnockbackDuration = 0.2f;
		protected float _knockbackTimer = 0f;

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
		public CpuParticles2D DashParticles { get; set; }

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

			ApplySkillTree();

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

			TickEffects(Data.CurrentEffects, dt);
			TickEffects(CurrentEffects, dt);
			TickKnockback(dt);
			UpdateAbilities(Data.UnlockedAbilities, dt);
			UpdateAbilities(UnlockedAbilities, dt);

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
				HandleDropItem();
			}

			TestPositionRequest(Position);
			HandleMovement(dt);
			HandleUseItem(dt);
			HandleReload(dt);
			UpdateAnimation();
		}

		private void TickEffects(Godot.Collections.Array<EffectDefinitionData> effects, float dt)
		{
			for (int i = effects.Count - 1; i >= 0; i--)
			{
				var effect = effects[i];

				if (effect == null)
				{
					effects.RemoveAt(i);

					continue;
				}

				EffectDB.Get(effect.Id)?.Tick(this, effect, dt);

				if (effect.Expired)
				{
					effects.RemoveAt(i);
				}
			}
		}

		private void UpdateAbilities(Godot.Collections.Array<ActionDefinitionData> abilities, float dt)
		{
			foreach (var action in abilities)
			{
				if (action == null)
				{
					continue;
				}

				ActionDB.Get(action.Id)?.Update(dt, this, action);
			}
		}

		#endregion

		#region Core - Damage system

		public int GetMaxHealth()
		{
			var equippedProperties = EquippedInstance()?.Properties.OfType<HealthPropertyData>().ToList() ?? new List<HealthPropertyData>();

			return Resolver.Resolve(Data.Properties.OfType<HealthPropertyData>().ToList(), Properties.OfType<HealthPropertyData>().ToList(), equippedProperties).MaxHealth;
		}

		public Godot.Collections.Array<ActionDefinitionData> GetAllUnlockedAbilities()
		{
			var result = new Godot.Collections.Array<ActionDefinitionData>();

			foreach (var action in Data.UnlockedAbilities)
			{
				result.Add(action);
			}

			foreach (var action in UnlockedAbilities)
			{
				result.Add(action);
			}

			return result;
		}

		public Godot.Collections.Array<EffectDefinitionData> GetAllCurrentEffects()
		{
			var result = new Godot.Collections.Array<EffectDefinitionData>();

			foreach (var effect in Data.CurrentEffects)
			{
				result.Add(effect);
			}

			foreach (var effect in CurrentEffects)
			{
				result.Add(effect);
			}

			return result;
		}

		public virtual void ReceiveDamage(DamageInfo damage)
		{
			var resistanceFactor = 0f;

			if (IsAuthoritative())
			{
				var equippedInstance = EquippedInstance();
				var equippedResistances = equippedInstance?.Properties.OfType<DamageResistencePropertyData>().ToList() ?? new List<DamageResistencePropertyData>();
				var equippedResistanceMultipliers = equippedInstance?.Properties.OfType<DamageResistenceMultiplierPropertyData>().ToList() ?? new List<DamageResistenceMultiplierPropertyData>();
				var resolvedResistances = Resolver.Resolve(Data.Properties.OfType<DamageResistencePropertyData>().ToList(), Properties.OfType<DamageResistencePropertyData>().ToList(), equippedResistances);
				var resolvedResistanceMultipliers = Resolver.Resolve(Data.Properties.OfType<DamageResistenceMultiplierPropertyData>().ToList(), Properties.OfType<DamageResistenceMultiplierPropertyData>().ToList(), equippedResistanceMultipliers);

				resistanceFactor = resolvedResistances.FirstOrDefault(r => r.DamageType == damage.Type)?.ResistanceFactor ?? 0f;

				var resistanceMultiplier = resolvedResistanceMultipliers.FirstOrDefault(m => m.DamageType == damage.Type)?.Multiplier ?? 1f;
				var critMultiplier = 1f + (GD.Randf() <= damage.CritChance ? damage.CritDamage : 0f);
				var finalDamage = (int)(damage.Amount * critMultiplier * (1f - resistanceFactor) * resistanceMultiplier);

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

		public void ApplyKnockback(Vector2 direction, float force)
		{
			if (force <= 0f || direction == Vector2.Zero || !IsAuthoritative())
			{
				return;
			}

			ApplyKnockbackRequest(direction.Normalized() * force);
		}

		public void ApplyKnockbackRequest(Vector2 velocity)
		{
			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
			{
				ApplyKnockbackReceive(velocity);

				return;
			}

			Rpc(nameof(ApplyKnockbackReceive), velocity);
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void ApplyKnockbackReceive(Vector2 velocity)
		{
			Velocity = velocity;
			_knockbackTimer = KnockbackDuration;
			Data.CanUpdateMovement = false;
		}

		protected void TickKnockback(float dt)
		{
			if (_knockbackTimer <= 0f)
			{
				return;
			}

			_knockbackTimer -= dt;

			if (_knockbackTimer <= 0f)
			{
				Data.CanUpdateMovement = true;
				Velocity = Vector2.Zero;
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
			var chargesProp = Resolver.Resolve(def.Properties.OfType<ChargesPropertyData>().ToList(), data.Properties.OfType<ChargesPropertyData>().ToList()).FirstOrDefault();

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

		public void HandleDropItem()
		{
			if (!Input.DropItem || Data.EquippedItemId <= 0)
			{
				return;
			}

			DropItemRequest(Data.EquippedItemId, 1);
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

				var slotDef = ItemDB.Get(slot.Id);
				var chargesProp = Resolver.Resolve(slotDef?.Properties.OfType<ChargesPropertyData>().ToList() ?? new List<ChargesPropertyData>(), slot.Properties.OfType<ChargesPropertyData>().ToList()).FirstOrDefault();

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

				var slotDef = ItemDB.Get(slot.Id);
				var chargesProp = Resolver.Resolve(slotDef?.Properties.OfType<ChargesPropertyData>().ToList() ?? new List<ChargesPropertyData>(), slot.Properties.OfType<ChargesPropertyData>().ToList()).FirstOrDefault();

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

			var previousItem = EquippedInstance();

			if (previousItem != null && previousItem.InstanceId != instanceId)
			{
				ItemDB.Get(previousItem.Id)?.OnUnequip(this, previousItem);
			}

			Data.EquippedItemId = instanceId;

			ItemDB.Get(item.Id)?.OnEquip(this, item);

			EmitSignal(SignalName.ItemEquipped, instanceId);
		}

		#region Public API - Items query

		public ItemDefinitionData GetSlot(int index)
		{
			return Inventory.GetSlot(Data?.Inventory, index);
		}

		public ItemDefinitionData EquippedInstance()
		{
			return Inventory.FindItem(Data?.Inventory, Data?.EquippedItemId ?? 0);
		}

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
			if (string.IsNullOrEmpty(effectId) || Data?.CurrentEffects == null)
			{
				return;
			}

			Data.CurrentEffects.Add(EffectDB.CreateInstance(effectId));

			EmitSignal(SignalName.EffectsChanged);
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

		#endregion

		#region Core - Skill tree system

		public void ApplySkillTree()
		{
			Properties.Clear();

			var grantedAbilityIds = new HashSet<string>();
			var grantedEffectIds = new HashSet<string>();

			foreach (var progress in Data.SkillTree)
			{
				if (progress == null || progress.CurrentLevel <= 0)
				{
					continue;
				}

				var node = SkillTreeDB.Get(progress.NodeId);

				if (node == null)
				{
					continue;
				}

				for (int level = 0; level < progress.CurrentLevel; level++)
				{
					foreach (var property in node.Properties)
					{
						Properties.Add(Resolver.CloneProperty(property));
					}
				}

				foreach (var abilityId in node.UnlockedAbilities)
				{
					grantedAbilityIds.Add(abilityId);

					if (!HasUnlockedAbility(abilityId))
					{
						UnlockedAbilities.Add(ActionDB.CreateInstance(abilityId, this));
					}
				}

				foreach (var effectId in node.Effects)
				{
					grantedEffectIds.Add(effectId);

					if (!HasCurrentEffect(effectId))
					{
						CurrentEffects.Add(EffectDB.CreateInstance(effectId));
					}
				}
			}

			for (int i = UnlockedAbilities.Count - 1; i >= 0; i--)
			{
				if (UnlockedAbilities[i] == null || !grantedAbilityIds.Contains(UnlockedAbilities[i].Id))
				{
					UnlockedAbilities.RemoveAt(i);
				}
			}

			for (int i = CurrentEffects.Count - 1; i >= 0; i--)
			{
				if (CurrentEffects[i] == null || !grantedEffectIds.Contains(CurrentEffects[i].Id))
				{
					CurrentEffects.RemoveAt(i);
				}
			}
		}

		private bool HasUnlockedAbility(string actionId)
		{
			foreach (var action in UnlockedAbilities)
			{
				if (action != null && action.Id == actionId)
				{
					return true;
				}
			}

			return false;
		}

		private bool HasCurrentEffect(string effectId)
		{
			foreach (var effect in CurrentEffects)
			{
				if (effect != null && effect.Id == effectId)
				{
					return true;
				}
			}

			return false;
		}

		private SkillTreeNodeData FindSkillNodeProgress(string nodeId)
		{
			foreach (var entry in Data.SkillTree)
			{
				if (entry != null && entry.NodeId == nodeId)
				{
					return entry;
				}
			}

			return null;
		}

		public bool LevelUpSkillNode(string nodeId)
		{
			if (string.IsNullOrEmpty(nodeId) || !SkillTreeDB.CanLevelUp(Data.SkillTree, nodeId))
			{
				return false;
			}

			var progress = FindSkillNodeProgress(nodeId);

			if (progress == null)
			{
				progress = new SkillTreeNodeData { NodeId = nodeId };

				Data.SkillTree.Add(progress);
			}

			progress.CurrentLevel++;

			ApplySkillTree();

			return true;
		}

		public void ResetSkillTree()
		{
			Data.SkillTree.Clear();

			ApplySkillTree();
		}

		#endregion

		#region Core - Movement handlers

		public void HandleMovement(float delta)
		{
			var equippedProperties = EquippedInstance()?.Properties.OfType<MovementPropertyData>().ToList() ?? new List<MovementPropertyData>();
			var movementProperties = Resolver.Resolve(
				Data.Properties.OfType<MovementPropertyData>().ToList(),
				Properties.OfType<MovementPropertyData>().ToList(),
				equippedProperties
			);

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
				v.Y = movementProperties.JumpVelocity;

				GD.Print("[HandleMovement] Pulando");
			}

			if (Input.MoveX != 0)
			{
				v.X = Input.MoveX * movementProperties.Speed;
			}
			else
			{
				v.X = Mathf.MoveToward(v.X, 0, movementProperties.Speed);
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

        #region Core - Rpc - Effects

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

        #region Core - Rpc - Abilioties 

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

        #region Core - Rpc - Skill tree

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void LevelUpSkillNodeReceive(string nodeId)
        {
            LevelUpSkillNode(nodeId);

            SyncSkillTreeToOwner();
        }

        public void LevelUpSkillNodeRequest(string nodeId)
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
            {
                LevelUpSkillNodeReceive(nodeId);

                return;
            }

            RpcId(1, nameof(LevelUpSkillNodeReceive), nodeId);
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ResetSkillTreeReceive()
        {
            ResetSkillTree();

            SyncSkillTreeToOwner();
        }

        public void ResetSkillTreeRequest()
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
            {
                ResetSkillTreeReceive();

                return;
            }

            RpcId(1, nameof(ResetSkillTreeReceive));
        }

        // RpcId(1, ...) so executa localmente pra quem chama quando o alvo e
        // o proprio peer - como o cliente manda pro servidor (peer 1, que nao
        // e ele mesmo), o CallLocal nao dispara do lado do cliente e o
        // Data.SkillTree dele nunca fica sabendo do novo nivel. Por isso o
        // servidor, apos aplicar, reenvia o SkillTree atualizado de volta so
        // pro peer dono do player (nao precisa fazer nada se quem processou
        // for o proprio dono, ex: servidor jogando localmente).
        private void SyncSkillTreeToOwner()
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || !Multiplayer.IsServer() || PeerId == 1)
            {
                return;
            }

            var skillTree = new Godot.Collections.Array();

            foreach (var entry in Data.SkillTree)
            {
                skillTree.Add(GodotDictionaryParser.ToDictionary(entry));
            }

            RpcId(PeerId, nameof(SyncSkillTreeReceive), skillTree);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SyncSkillTreeReceive(Godot.Collections.Array skillTree)
        {
            Data.SkillTree = new Godot.Collections.Array<SkillTreeNodeData>();

            foreach (var entry in skillTree)
            {
                var node = GodotDictionaryParser.ToResource<SkillTreeNodeData>(entry.AsGodotDictionary());

                if (node != null)
                {
                    Data.SkillTree.Add(node);
                }
            }

            ApplySkillTree();
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
			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
			{
				EquipItemReceive(instanceId);

				return;
			}

			Rpc(nameof(EquipItemReceive), instanceId);
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
			// Precisa cobrir a distância percorrida em uma rajada rápida (ex: dash a
			// 800u/s) mais a latência de replicação do Input até o servidor começar
			// a simular a mesma ação, senão o servidor corrige/cancela o movimento.
			var maxTolerance = 250.0f;
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
			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
			{
				return;
			}

			if (Multiplayer.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected)
			{
				return;
			}

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
