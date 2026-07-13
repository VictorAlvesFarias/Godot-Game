using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Jogo25D.Characters;
using Jogo25D.Items;
using Jogo25D.Properties;
using Jogo25D.Effects;

namespace Jogo25D.Systems
{
	public partial class Inventory : Node
	{
        #region Events

        [Signal]
		public delegate void InventoryChangedEventHandler();

		[Signal]
		public delegate void ItemEquippedEventHandler(int slotIndex);

        #endregion

        #region Constants

        public const int INVENTORY_SIZE = 16;

        #endregion

        #region Node references

        public Player LocalPlayer { get; set; }

        #endregion

        #region Properties

        public ItemInstance[] Items { get; set; } = Array.Empty<ItemInstance>();

        #endregion

        #region Godot implementation

        public override void _Ready()
		{
			LocalPlayer = GetOwner<Player>();
		}

        #endregion

        #region Core - Actions
		
		private void AddItem(string id, int quantity = 1)
		{
			if (id == null)
			{
				return;
			}

			var definition = ItemDB.Get(id);

			if (definition.Stackable)
			{
				for (int i = 0; i < INVENTORY_SIZE; i++)
				{
					if (!LocalPlayer.Items[i].IsEmpty() && LocalPlayer.Items[i].Definition?.Name == definition.Name)
					{
						if (LocalPlayer.Items[i].CanAddMore())
						{
							var spaceLeft = definition.MaxStackSize - LocalPlayer.Items[i].Quantity;
							var toAdd = Mathf.Min(quantity, spaceLeft);

							LocalPlayer.Items[i].Quantity += toAdd;
							
							quantity -= toAdd;

							EmitSignal(SignalName.InventoryChanged);

							if (quantity <= 0)
							{
								return;
							}
						}
					}
				}
			}

			for (int i = 0; i < INVENTORY_SIZE; i++)
			{
				if (LocalPlayer.Items[i].IsEmpty())
				{
					LocalPlayer.Items[i] = new ItemInstance();
					LocalPlayer.Items[i].Definition = definition;
					LocalPlayer.Items[i].Quantity = quantity;
					LocalPlayer.Items[i].Properties = new List<BaseProperty>(definition.Properties);
					LocalPlayer.Items[i].OnHitEffects = new List<EffectDefinition>(definition.OnHitEffects);
					LocalPlayer.Items[i].OnUseEffects = new List<EffectDefinition>(definition.OnUseEffects);
					
					EmitSignal(SignalName.InventoryChanged);
					
					return;
				}
			};
		}
		
		private void RemoveItem(int slotIndex, int quantity = 1)
		{
			if (slotIndex < 0 || slotIndex >= INVENTORY_SIZE)
			{ 
				return;
			}

			var slot = LocalPlayer.Items[slotIndex];

			if (slot.IsEmpty())
			{ 
				return;
			}

			slot.Quantity -= quantity;

			if (slot.Quantity <= 0)
			{
				slot.Clear();
			}

			EmitSignal(SignalName.InventoryChanged);

			return;
		}

		private void SwapSlots(int fromIndex, int toIndex)
		{
			if (fromIndex < 0 || fromIndex >= INVENTORY_SIZE)
			{
				return;
			}

			if (toIndex < 0 || toIndex >= INVENTORY_SIZE)
			{ 
				return;
			}

			if (fromIndex == toIndex)
			{ 
				return;
			}

			GD.Print($"Trocando slot {fromIndex} ({LocalPlayer.Items[fromIndex].Definition?.Name ?? "vazio"}) com slot {toIndex} ({LocalPlayer.Items[toIndex].Definition?.Name ?? "vazio"})");

			var tempDef = LocalPlayer.Items[fromIndex].Definition;
			var tempQuantity = LocalPlayer.Items[fromIndex].Quantity;
			var tempProps = LocalPlayer.Items[fromIndex].Properties;
			var tempEffects = LocalPlayer.Items[fromIndex].OnHitEffects;

			LocalPlayer.Items[fromIndex].Definition = LocalPlayer.Items[toIndex].Definition;
			LocalPlayer.Items[fromIndex].Quantity = LocalPlayer.Items[toIndex].Quantity;
			LocalPlayer.Items[fromIndex].Properties = LocalPlayer.Items[toIndex].Properties;
			LocalPlayer.Items[fromIndex].OnHitEffects = LocalPlayer.Items[toIndex].OnHitEffects;
			LocalPlayer.Items[toIndex].Definition = tempDef;
			LocalPlayer.Items[toIndex].Quantity = tempQuantity;
			LocalPlayer.Items[toIndex].Properties = tempProps;
			LocalPlayer.Items[toIndex].OnHitEffects = tempEffects;

			GD.Print($"ApÃ³s troca - slot {fromIndex}: {LocalPlayer.Items[fromIndex].Definition?.Name ?? "vazio"}, slot {toIndex}: {LocalPlayer.Items[toIndex].Definition?.Name ?? "vazio"}");

			EmitSignal(SignalName.InventoryChanged);
			
			return;
		}
        
		private void EquipItem(int slotIndex)
		{
			if (slotIndex < 0 || slotIndex >= INVENTORY_SIZE)
			{
				return;
			}

			var slot = LocalPlayer.Items[slotIndex];

			if (slot.IsEmpty() || slot.Definition == null || !slot.Definition.IsEquippable)
			{
				return;
			}

			LocalPlayer.EquippedDefinition = slot.Definition;
			LocalPlayer.EquippedSlotIndex = slotIndex;

			EmitSignal(SignalName.ItemEquipped, slotIndex);

			return;
		}

        #endregion

        #region Core - Information

        public ItemInstance GetSlot(int index)
		{
			if (index < 0 || index >= INVENTORY_SIZE)
			{ 
				return null;
			}

			return LocalPlayer.Items[index];
		}

		public int GetEquippedSlotIndex()
		{
			return LocalPlayer.EquippedSlotIndex;
		}

		public int CountItem(string itemName)
		{
			int count = 0;
			for (int i = 0; i < INVENTORY_SIZE; i++)
			{
				if (!LocalPlayer.Items[i].IsEmpty() && LocalPlayer.Items[i].Definition?.Name == itemName)
				{
					count += LocalPlayer.Items[i].Quantity;
				}
			}
			return count;
		}

		public int CountAmmoByChargeType(string chargeType)
		{
			if (string.IsNullOrEmpty(chargeType))
			{
				return 0;
			}

			int count = 0;

			for (int i = 0; i < INVENTORY_SIZE; i++)
			{
				if (i == LocalPlayer.EquippedSlotIndex)
				{
					continue;
				}

				if (LocalPlayer.Items[i].IsEmpty())
				{
					continue;
				}

				var chargesProp = LocalPlayer.Items[i].Properties.OfType<ChargesProperty>().FirstOrDefault();

				if (chargesProp != null && chargesProp.ChargeItemId == chargeType)
				{
					count += LocalPlayer.Items[i].Quantity;
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

			for (int i = 0; i < INVENTORY_SIZE && removed < quantity; i++)
			{
				if (i == LocalPlayer.EquippedSlotIndex)
				{
					continue;
				}

				if (LocalPlayer.Items[i].IsEmpty())
				{
					continue;
				}

				var chargesProp = LocalPlayer.Items[i].Properties.OfType<ChargesProperty>().FirstOrDefault();

				if (chargesProp == null || chargesProp.ChargeItemId != chargeType)
				{
					continue;
				}

				int toRemove = Mathf.Min(quantity - removed, LocalPlayer.Items[i].Quantity);

				LocalPlayer.Items[i].Quantity -= toRemove;

				removed += toRemove;

				if (LocalPlayer.Items[i].Quantity <= 0)
				{
					LocalPlayer.Items[i].Clear();
				}
			}

			if (removed > 0)
			{
				EmitSignal(SignalName.InventoryChanged);
			}

			return removed;
		}

        #endregion

        #region Core - Rpc

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
        public void EquipItemReceive(int slotIndex)
		{
			this.EquipItem(slotIndex);
		}

		public void EquipItemRequest(int slotIndex)
		{
			Rpc(nameof (EquipItemReceive), slotIndex);
		}

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
        public void AddItemReceive(string id, int quantity)
        {
            this.AddItem(id, quantity);
        }

        public void AddItemRequest(string id, int quantity)
        {
            Rpc(nameof(AddItemReceive), id, quantity);
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
        public void SwapSlotsReceive(int fromIndex, int toIndex)
        {
            this.SwapSlots(fromIndex, toIndex);
        }

        public void SwapSlotsRequest(int fromIndex, int toIndex)
        {
            Rpc(nameof(SwapSlotsReceive), fromIndex, toIndex);
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
        public void RemoveItemReceive(int slotIndex, int quantity)
        {
            this.RemoveItemReceive(slotIndex, quantity);
        }

        public void RemoveItemRequest(int slotIndex, int quantity)
        {
            Rpc(nameof(RemoveItemReceive), slotIndex, quantity);
        }

        #endregion
    }
}