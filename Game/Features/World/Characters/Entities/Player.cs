using Godot;
using Jogo25D.Actions;
using Jogo25D.Characters;
using Jogo25D.Chunks;
using Jogo25D.Constants;
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
using Jogo25D.TileEntities;
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
		public ulong LastDimensionTradeMsec { get; set; }
        public bool Loaded { get; set;  }
		public string DisplayName { get; set; } = "";
		public float KnockbackTimer { get; set; }  = 0f;
        public float KnockbackDuration { get; set; } = 0.2f;
        public float DamagePopupDuration { get; set; } = 0.8f;
		public float DamagePopupRiseDistance { get; set; } = 26f;
        public PlayerData Data { get; set; } = new PlayerData();
        public Godot.Collections.Array<BasePropertyData> Properties { get; set; } = new();
        public Godot.Collections.Array<EffectDefinitionData> CurrentEffects { get; set; } = new();
        public Godot.Collections.Array<ActionDefinitionData> UnlockedAbilities { get; set; } = new Godot.Collections.Array<ActionDefinitionData>();
		public Dictionary<string, ActionDefinition> ActionDefinitions { get; } = new();
		public Dictionary<long, ItemDefinition> ItemDefinitions { get; } = new();

        #endregion

        #region Systems

        public Inventory Inventory { get; set; } = new Inventory();

        #endregion

        #region Node references

        public WorldManager NetworkManager { get; set; }

		#endregion

        #region Node children references

		public PlayerInput Input { get; set; }
		public Node2D Visuals { get; set; }
		public AnimatedSprite2D Sprite { get; set; }
		public CollisionShape2D Shape { get; set; }
		public Node2D Labels { get; set; }
		public Label NameLabel { get; set; }
		public Label HealthLabel { get; set; }
		public Label InteractPromptLabel { get; set; }

		#endregion

		#region Core - Facing

		public bool FacingLeft ()
		{
			return Visuals != null && Visuals.Scale.X < 0f;
		} 

		public void SetFacing(bool faceLeft)
		{
			if (Visuals != null)
			{
				Visuals.Scale = new Vector2(faceLeft ? -1f : 1f, 1f);
			}

			if (Shape != null)
			{
				var position = Shape.Position;

				Shape.Position = position;
			}
		}

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			GD.Print("[Player._Ready] Starting method");
			GD.Print("[Player._Ready] Adding players to group");

			AddToGroup("players");

			GD.Print("[Player._Ready] Initializating ItemFactory");

			ItemFactory.Initialize();

			GD.Print("[Player._Ready] Trying get Nodes");

			Gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
			NetworkManager = GetTree().Root.GetNode<WorldManager>(WorldManager.DEFAULT_NODE_PATH);
			Visuals = GetNodeOrNull<Node2D>("Visuals");
			Sprite = GetNodeOrNull<AnimatedSprite2D>("Visuals/Sprite");
			Shape = GetNodeOrNull<CollisionShape2D>("Shape");
			Input = GetNodeOrNull<PlayerInput>("Systems/PlayerInput");
			Labels = GetNodeOrNull<Node2D>("Labels");
			NameLabel = GetNodeOrNull<Label>("Labels/NameLabel");
			HealthLabel = GetNodeOrNull<Label>("Labels/HealthLabel");
			InteractPromptLabel = GetNodeOrNull<Label>("Labels/InteractPromptLabel");

			GD.Print("[Player._Ready] Setting default states");

			Sprite.Play("idle");

			GD.Print("[Player._Ready] Adding animation events");

			GD.Print("[Player._Ready] Seting starter slot");

			if (IsAuthoritative())
			{
				Data ??= new PlayerData();
				Data.Inventory ??= new InventoryData();

                GiveItem(ItemFactory.CreateInstance("fire_slash_sword"));
                GiveItem(ItemFactory.CreateInstance("blue_fire_sword"));
                GiveItem(ItemFactory.CreateInstance("blue_fire_sword_alt"));
                GiveItem(ItemFactory.CreateInstance("dark_slash_sword"));
                GiveItem(ItemFactory.CreateInstance("whip"));
                GiveItem(ItemFactory.CreateInstance("green_blow_sword"));
                GiveItem(ItemFactory.CreateInstance("real_sword"));
				GiveItem(ItemFactory.CreateInstance("bow_starting2"));

				var startingMeleeWeapon = ItemFactory.CreateInstance("sword_starting");

				GiveItem(startingMeleeWeapon);

				GiveItem(ItemFactory.CreateInstance("pickaxe_starting"));

				var startingGrassBlocks = ItemFactory.CreateInstance("block_grass");

				startingGrassBlocks.Quantity = 20;

				GiveItem(startingGrassBlocks);

				var startingPoisonFlask = ItemFactory.CreateInstance("poison_flask");

				startingPoisonFlask.Quantity = 20;

				GiveItem(startingPoisonFlask);

				var startingFireDamagePotion = ItemFactory.CreateInstance("fire_damage_potion");

				startingFireDamagePotion.Quantity = 20;

				GiveItem(startingFireDamagePotion);

				var startingHealthRegenPotion = ItemFactory.CreateInstance("health_regen_potion");

				startingHealthRegenPotion.Quantity = 20;

				GiveItem(startingHealthRegenPotion);

				var startingSpeedPotion = ItemFactory.CreateInstance("speed_potion");

				startingSpeedPotion.Quantity = 20;

				GiveItem(startingSpeedPotion);

				var startingInstantHealPotion = ItemFactory.CreateInstance("instant_heal_potion");

				startingInstantHealPotion.Quantity = 20;

				GiveItem(startingInstantHealPotion);

				foreach (var actionId in ActionFactory.GetAllIds())
				{
					GiveAbility(actionId);
				}
			}

			ApplySkillTree();

			Inventory.EnsureSize(Data.Inventory);

			// Reconstroi ItemDefinitions/ActionDefinitions a partir do que
			// ja esta em Data - necessario mesmo pra quem nao e autoritativo,
			// ja que pra esses clientes Data chega pronto via replicacao
			// (SpawnPlayerReceive desserializa o Data inteiro direto, sem
			// passar por GiveItem/GiveAbility) e os dois dicionarios sao
			// cache local, nunca sincronizado pela rede.
			foreach (var item in Data.Inventory.Items)
			{
				if (item != null)
				{
					EnsureItemDefinition(item);
				}
			}

			foreach (var action in Data.UnlockedAbilities)
			{
				if (action != null)
				{
					EnsureActionDefinition(action.Id, action);
				}
			}

			if (Data.EquippedItemId > 0 && Inventory.FindItem(Data.Inventory, Data.EquippedItemId) != null)
			{
				GD.Print("[Player._Ready] Running equip item");

				EquipItemRequest(Data.EquippedItemId);
			}
		}

		public override void _PhysicsProcess(double delta)
		{
			var dt = (float)delta;

			UpdateEffects(dt);
            UpdateKnockback(dt);
			UpdateAbilities(dt);
			UpdateItems(dt);

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
			UpdatePvpCollisionExceptions();
			UpdateIndicators(dt);
        }

		public override void _Process(double delta)
		{
			UpdateNameplate();
			UpdateInteractPrompt();
		}

        #endregion

        #region Updates

        protected void UpdatePvpCollisionExceptions()
        {
            foreach (var other in GetTree().GetNodesInGroup("players").OfType<Player>())
            {
                if (other == this)
                {
                    continue;
                }

                if (Data.PvpEnabled && other.Data.PvpEnabled)
                {
                    RemoveCollisionExceptionWith(other);
                }
                else
                {
                    AddCollisionExceptionWith(other);
                }
            }
        }

        protected void UpdateNameplate()
        {
            if (NameLabel == null || HealthLabel == null)
            {
                return;
            }

            if (IsOwner())
            {
                NameLabel.Visible = false;
                HealthLabel.Visible = false;

                return;
            }

            var hasName = !string.IsNullOrEmpty(DisplayName);

            NameLabel.Visible = hasName;
            NameLabel.Text = DisplayName;

            HealthLabel.Visible = true;
            HealthLabel.Text = $"{Data.CurrentHealth}/{GetMaxHealth()}";
        }

        protected void UpdateInteractPrompt()
        {
            if (InteractPromptLabel == null)
            {
                return;
            }

            if (!IsOwner())
            {
                InteractPromptLabel.Visible = false;

                return;
            }

            var manager = GetParent()?.GetNodeOrNull<TileEntityManager>("TileEntityManager");

            if (manager == null || !manager.TryGetPromptFor(this, out var prompt))
            {
                InteractPromptLabel.Visible = false;

                return;
            }

            InteractPromptLabel.Text = prompt;
            InteractPromptLabel.Visible = true;
        }

        protected void UpdateItems(float dt)
		{
            foreach (var item in Data.Inventory.Items)
            {
                if (item == null)
                {
                    continue;
                }

                ItemDefinitions.GetValueOrDefault(item.InstanceId)?.Update(dt, item);
            }
        }

        protected void UpdateAbilities(float dt)
        {
			var abilities = Resolver.Resolve(Data.UnlockedAbilities, UnlockedAbilities);

            foreach (var action in abilities)
            {
                if (action == null)
                {
                    continue;
                }

                var def = ActionDefinitions.GetValueOrDefault(action.Id);

                def?.Update(dt, this, action);
                def?.UpdateIndicator(this, action, dt);
            }
        }

		protected void UpdateEffects(float dt)
		{
            for (int i = Data.CurrentEffects.Count - 1; i >= 0; i--)
            {
                var effect = Data.CurrentEffects[i];

                if (effect == null)
                {
                    Data.CurrentEffects.RemoveAt(i);

                    continue;
                }

                EffectDB.Get(effect.Id)?.Tick(this, effect, dt);

                if (effect.Expired)
                {
                    Data.CurrentEffects.RemoveAt(i);
                }
            }

            for (int i = CurrentEffects.Count - 1; i >= 0; i--)
            {
                var effect = CurrentEffects[i];

                if (effect == null)
                {
                    CurrentEffects.RemoveAt(i);

                    continue;
                }

                EffectDB.Get(effect.Id)?.Tick(this, effect, dt);

                if (effect.Expired)
                {
                    CurrentEffects.RemoveAt(i);
                }
            }
        }

        protected void UpdateKnockback(float dt)
        {
            if (KnockbackTimer <= 0f)
            {
                return;
            }

            KnockbackTimer -= dt;

            if (KnockbackTimer <= 0f)
            {
                Data.CanUpdateMovement = true;
                Velocity = Vector2.Zero;
            }
        }

		protected void UpdateIndicators(float dt)
		{
            var equipped = EquippedInstance();

            if (equipped != null && ItemDefinitions.TryGetValue(equipped.InstanceId, out var def))
            {
                def.UpdateIndicator(this, equipped, dt);
            }
        }

        #endregion

        #region Animation 

        public void UpdateAnimation()
        {
            if (Velocity.X != 0)
            {
                SetFacing(Velocity.X < 0);
            }

            if (Sprite.Animation == "dead")
            {
                return;
            }

            if (Sprite.Animation == "melee" && Sprite.IsPlaying())
            {
                return;
            }

            if (Sprite.Animation == "mining" && Input.Attack)
            {
                return;
            }

            if (Sprite.Animation == "taking_damage" && Sprite.IsPlaying())
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

        #region Core - Damage system

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
		}

		public int GetMaxHealth()
		{
			var equippedProperties = EquippedInstance()?.Properties.OfType<HealthPropertyData>().ToList() ?? new List<HealthPropertyData>();

			return Resolver.Resolve(Data.Properties.OfType<HealthPropertyData>().ToList(), Properties.OfType<HealthPropertyData>().ToList(), equippedProperties).MaxHealth;
		}

        #endregion

        #region Core - Knockback

        public void ApplyKnockback(Vector2 direction, float force)
        {
            if (force <= 0f || direction == Vector2.Zero || !IsAuthoritative())
            {
                return;
            }

            ApplyKnockbackRequest(direction.Normalized() * force);
        }

        #endregion

        #region Core - Damage popup

        public void ShowDamagePopup(int amount)
		{
			if (amount <= 0 || Labels == null)
			{
				return;
			}

			var label = new Label();

			label.Text = $"-{amount}";
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.Size = new Vector2(60, 20);
			label.AddThemeFontSizeOverride("font_size", 16);
			label.AddThemeColorOverride("font_color", new Color(1f, 0.25f, 0.25f, 1f));
			label.AddThemeColorOverride("font_outline_color", Colors.Black);
			label.AddThemeConstantOverride("outline_size", 3);

			var offset = new Vector2(-30f + (float)GD.RandRange(-8, 8), -70f);

			label.Position = offset;

			Labels.AddChild(label);

			var tween = CreateTween();

			tween.TweenProperty(label, "position", offset + Vector2.Up * DamagePopupRiseDistance, DamagePopupDuration);
			tween.Parallel().TweenProperty(label, "modulate:a", 0f, DamagePopupDuration);
			tween.TweenCallback(Callable.From(label.QueueFree));
		}

		#endregion

		#region Core - Items system handlers

		public void HandleUseItem(float delta)
		{
			var data = Inventory.FindItem(Data.Inventory, Data.EquippedItemId);
			var def = data == null ? null : ItemDefinitions.GetValueOrDefault(data.InstanceId);

			if (data == null || def == null)
			{
				if (def is ToolDefinition idleTool)
				{
					idleTool.ResetMining();
				}

				return;
			}

			if (!Input.Attack)
			{
				if (def is ToolDefinition tool)
				{
					tool.ResetMining();
				}

				return;
			}

			def.Use(this, data);
		}

		public void HandleReload(float delta)
		{
			var data = Inventory.FindItem(Data.Inventory, Data.EquippedItemId);

			if (data == null)
			{
				return;
			}

			var def = ItemDefinitions.GetValueOrDefault(data.InstanceId);
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

		#region Core - Blocks system

		public string GetActiveDimensionId()
		{
			return GetParent() == NetworkManager?.OverworldParent ? ChunkStreamingManager.OverworldId : ChunkStreamingManager.UpsidedownId;
		}

		public TileMapLayer GetActiveTileLayer()
		{
			var parent = GetParent();
			var handAuthoredName = GetActiveDimensionId() == ChunkStreamingManager.OverworldId ? "Overworld-Tiles" : "Upsidedown-Tiles";

			return parent?.GetNodeOrNull<TileMapLayer>("ProceduralTiles")
				?? parent?.GetNodeOrNull<TileMapLayer>(handAuthoredName);
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

				var slotDef = ItemDefinitions.GetValueOrDefault(slot.InstanceId);
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

				var slotDef = ItemDefinitions.GetValueOrDefault(slot.InstanceId);
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
				var previousDef = ItemDefinitions.GetValueOrDefault(previousItem.InstanceId);

				previousDef?.OnUnequip(this, previousItem);
				previousDef?.HideIndicator(this);
			}

			Data.EquippedItemId = instanceId;

			ItemDefinitions.GetValueOrDefault(item.InstanceId)?.OnEquip(this, item);

			EmitSignal(SignalName.ItemEquipped, instanceId);
		}

		public void GiveItem(ItemData item)
        {
            if (Data?.Inventory == null)
            {
                return;
            }

            if (Inventory.AddItem(Data.Inventory, item))
            {
                EnsureItemDefinition(item);

                EmitSignal(SignalName.InventoryChanged);
            }
        }

		public ItemData GetSlot(int index)
		{
			return Inventory.GetSlot(Data?.Inventory, index);
		}
		
		public ItemData EquippedInstance()
		{
			return Inventory.FindItem(Data?.Inventory, Data?.EquippedItemId ?? 0);
		}
        
		private void EnsureItemDefinition(ItemData item)
		{
			if (item == null || ItemDefinitions.ContainsKey(item.InstanceId))
			{
				return;
			}

			if (Inventory.FindItem(Data.Inventory, item.InstanceId) == null)
			{
				return;
			}

			ItemDefinitions[item.InstanceId] = ItemFactory.Create(item.Id);
		}

        private void RemoveItemDefinitionIfGone(long instanceId)
		{
			if (Inventory.FindItem(Data.Inventory, instanceId) != null)
			{
				return;
			}

			if (ItemDefinitions.TryGetValue(instanceId, out var def))
			{
				def.DestroyIndicator();
				ItemDefinitions.Remove(instanceId);
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

			var data = ActionFactory.CreateInstance(actionId);

			Data.UnlockedAbilities.Add(data);

			EnsureActionDefinition(actionId, data);

			EmitSignal(SignalName.AbilitiesChanged);
		}

        public void EnsureActionDefinition(string actionId, ActionDefinitionData data)
		{
			if (string.IsNullOrEmpty(actionId) || ActionDefinitions.ContainsKey(actionId))
			{
				return;
			}

			var def = ActionFactory.Create(actionId);

			if (def == null)
			{
				return;
			}

			ActionDefinitions[actionId] = def;

			def.OnCreate(this, data);
		}

        public void RemoveActionDefinition(string actionId)
		{
			if (string.IsNullOrEmpty(actionId) || !ActionDefinitions.TryGetValue(actionId, out var def))
			{
				return;
			}

			def.DestroyIndicator();

			ActionDefinitions.Remove(actionId);
		}

        public bool IsAbilityStillGranted(string actionId)
		{
			return (Data?.UnlockedAbilities?.Any(e => e != null && e.Id == actionId) ?? false)
				|| UnlockedAbilities.Any(e => e != null && e.Id == actionId);
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
						Properties.Add(property);
					}
				}

				foreach (var abilityId in node.UnlockedAbilities)
				{
					grantedAbilityIds.Add(abilityId);

					if (!UnlockedAbilities.Any(e => e.Id == abilityId))
					{
						var abilityData = ActionFactory.CreateInstance(abilityId);

						UnlockedAbilities.Add(abilityData);

						EnsureActionDefinition(abilityId, abilityData);
					}
				}

				foreach (var effectId in node.Effects)
				{
					grantedEffectIds.Add(effectId);

					if (!CurrentEffects.Any(e=> e.Id == effectId))
					{
						CurrentEffects.Add(EffectDB.CreateInstance(effectId));
					}
				}
			}

			for (int i = UnlockedAbilities.Count - 1; i >= 0; i--)
			{
				if (UnlockedAbilities[i] == null || !grantedAbilityIds.Contains(UnlockedAbilities[i].Id))
				{
					var removedId = UnlockedAbilities[i]?.Id;

					UnlockedAbilities.RemoveAt(i);

					if (!string.IsNullOrEmpty(removedId) && !IsAbilityStillGranted(removedId))
					{
						RemoveActionDefinition(removedId);
					}
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

		public bool LevelUpSkillNode(string nodeId)
		{
			if (string.IsNullOrEmpty(nodeId) || !SkillTreeDB.CanLevelUp(Data.SkillTree, nodeId))
			{
				return false;
			}

			var progress = Data.SkillTree.FirstOrDefault(e => e.NodeId == nodeId);

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

        #region Core - Rpc - PlaceBlock

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void PlaceBlockReceive(Vector2I cell, long instanceId)
        {
            if (!IsAuthoritative())
            {
                return;
            }

            var item = Inventory.FindItem(Data.Inventory, instanceId);

            if (item == null || item.Quantity <= 0)
            {
                return;
            }

            if (ItemDefinitions.GetValueOrDefault(item.InstanceId) is not BlockItemDefinition blockItemDef)
            {
                return;
            }

            if (NetworkManager == null || !NetworkManager.PlaceBlockAuthoritative(cell, blockItemDef.BlockId, GetActiveDimensionId()))
            {
                return;
            }

            RemoveItemRequest(instanceId, 1);
        }

        public void PlaceBlockRequest(Vector2I cell, long instanceId)
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
            {
                PlaceBlockReceive(cell, instanceId);

                return;
            }

            RpcId(1, nameof(PlaceBlockReceive), cell, instanceId);
        }

        #endregion

        #region Core - Rpc - Knockback

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ApplyKnockbackReceive(Vector2 velocity)
        {
            Velocity = velocity;
            KnockbackTimer = KnockbackDuration;
            Data.CanUpdateMovement = false;
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

            SyncSkillTreeToRequest();
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

            SyncSkillTreeToRequest();
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
        
		private void SyncSkillTreeToRequest()
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

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void SetPvpEnabledReceive(bool enabled)
		{
			Data.PvpEnabled = enabled;
		}

		public void SetPvpEnabledRequest(bool enabled)
		{
			if (!IsOwner())
			{
				return;
			}

			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
			{
				SetPvpEnabledReceive(enabled);

				return;
			}

			Rpc(nameof(SetPvpEnabledReceive), enabled);
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

				if (health > 0 && Sprite.Animation != "dead")
				{
					Sprite.Play("taking_damage");
				}
			}

			if (health <= 0 && previousHealth > 0)
			{
				Sprite.Play("dead");
				Input?.AddBlocker("dead");
			}

			if (health > 0 && previousHealth <= 0)
			{
				Sprite.Play("idle");
				Input?.RemoveBlocker("dead");
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
			var item = GodotDictionaryParser.ToResource<ItemData>(data);

			if (Inventory.AddItem(Data.Inventory, item))
			{
				EnsureItemDefinition(item);

				EmitSignal(SignalName.InventoryChanged);
			}
		}

		public void AddItemRequest(ItemData item)
		{
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
			if (Inventory.MoveItem(Data.Inventory, instanceId, toIndex))
			{
				EmitSignal(SignalName.InventoryChanged);
			}
		}

		public void MoveItemRequest(long instanceId, int toIndex)
		{
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
			if (Inventory.RemoveItem(Data.Inventory, instanceId, quantity))
			{
				RemoveItemDefinitionIfGone(instanceId);

				EmitSignal(SignalName.InventoryChanged);
			}
		}

		public void RemoveItemRequest(long instanceId, int quantity)
		{
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
			var dropData = (ItemData)item.Duplicate(true);

			dropData.InstanceId = ItemFactory.NextInstanceId();
			dropData.Quantity = dropQuantity;

			RemoveItemRequest(instanceId, dropQuantity);

			var dropOffset = new Vector2(FacingLeft() ? -40f : 40f, 0f);

			NetworkManager.SpawnWorldItemRequest(dropData, GlobalPosition + dropOffset, GetActiveDimensionId());
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
				EnsureItemDefinition(worldItem.Data);

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

			ItemDefinitions.GetValueOrDefault(data.InstanceId)?.ConsumeCharge(data);
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

			ItemDefinitions.GetValueOrDefault(data.InstanceId)?.FinishReload(taken, data);
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

			RpcId(1, nameof(TestPositionReceive), pos);
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void SyncPositionReceive(Vector2 pos, bool sendToOwner)
		{
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
			Rpc(nameof(SyncPositionReceive), pos, sendToOwner);
		}

		#endregion
	}
}