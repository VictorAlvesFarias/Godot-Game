using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Jogo25D.Items;
using Jogo25D.Properties;

namespace Jogo25D.Systems
{
	public partial class Inventory : Node
	{
		[Signal]
		public delegate void InventoryChangedEventHandler();

		[Signal]
		public delegate void ItemEquippedEventHandler(int slotIndex);

		private const int INVENTORY_SIZE = 16;
		private ItemInstance[] slots = new ItemInstance[INVENTORY_SIZE];
		private ItemDefinition equippedDefinition;
		private int equippedSlotIndex = -1;

		public override void _Ready()
		{
			for (int i = 0; i < INVENTORY_SIZE; i++)
			{
				slots[i] = new ItemInstance();
			}
		}

		public bool AddItem(ItemDefinition definition, int quantity = 1)
		{
			if (definition == null) return false;

			if (definition.Stackable)
			{
				for (int i = 0; i < INVENTORY_SIZE; i++)
				{
					if (!slots[i].IsEmpty() && slots[i].Definition?.Name == definition.Name)
					{
						if (slots[i].CanAddMore())
						{
							int spaceLeft = definition.MaxStackSize - slots[i].Quantity;
							int toAdd = Mathf.Min(quantity, spaceLeft);
							slots[i].Quantity += toAdd;
							quantity -= toAdd;

							EmitSignal(SignalName.InventoryChanged);

							if (quantity <= 0) return true;
						}
					}
				}
			}

			for (int i = 0; i < INVENTORY_SIZE; i++)
			{
				if (slots[i].IsEmpty())
				{
					slots[i] = new ItemRechargeableInstance();
					slots[i].Definition = definition;
					slots[i].Quantity = quantity;
					// Populate per-instance properties from the definition defaults
					slots[i].Properties = new List<Jogo25D.Properties.BaseProperty>(definition.Properties);
					slots[i].OnHitEffects = new List<Jogo25D.Effects.EffectDefinition>(definition.OnHitEffects);
					slots[i].OnUseEffects = new List<Jogo25D.Effects.EffectDefinition>(definition.OnUseEffects);
					EmitSignal(SignalName.InventoryChanged);
					return true;
				}
			}

			return false;
		}

		public bool RemoveItem(int slotIndex, int quantity = 1)
		{
			if (slotIndex < 0 || slotIndex >= INVENTORY_SIZE) return false;

			var slot = slots[slotIndex];
			if (slot.IsEmpty()) return false;

			slot.Quantity -= quantity;

			if (slot.Quantity <= 0)
			{
				slot.Clear();
			}

			EmitSignal(SignalName.InventoryChanged);
			return true;
		}

		public ItemInstance GetSlot(int index)
		{
			if (index < 0 || index >= INVENTORY_SIZE) return null;
			return slots[index];
		}

		public ItemInstance[] GetAllSlots()
		{
			return slots;
		}

		public void Clear()
		{
			for (int i = 0; i < INVENTORY_SIZE; i++)
			{
				slots[i].Clear();
			}

			equippedDefinition = null;
			equippedSlotIndex = -1;

			EmitSignal(SignalName.InventoryChanged);
		}

		public bool SwapSlots(int fromIndex, int toIndex)
		{
			if (fromIndex < 0 || fromIndex >= INVENTORY_SIZE) return false;
			if (toIndex < 0 || toIndex >= INVENTORY_SIZE) return false;
			if (fromIndex == toIndex) return false;

			GD.Print($"Trocando slot {fromIndex} ({slots[fromIndex].Definition?.Name ?? "vazio"}) com slot {toIndex} ({slots[toIndex].Definition?.Name ?? "vazio"})");

			ItemDefinition tempDef      = slots[fromIndex].Definition;
			int            tempQuantity = slots[fromIndex].Quantity;
			var tempProps               = slots[fromIndex].Properties;
			var tempEffects             = slots[fromIndex].OnHitEffects;

			slots[fromIndex].Definition   = slots[toIndex].Definition;
			slots[fromIndex].Quantity     = slots[toIndex].Quantity;
			slots[fromIndex].Properties   = slots[toIndex].Properties;
			slots[fromIndex].OnHitEffects = slots[toIndex].OnHitEffects;

			slots[toIndex].Definition   = tempDef;
			slots[toIndex].Quantity     = tempQuantity;
			slots[toIndex].Properties   = tempProps;
			slots[toIndex].OnHitEffects = tempEffects;

			GD.Print($"Após troca - slot {fromIndex}: {slots[fromIndex].Definition?.Name ?? "vazio"}, slot {toIndex}: {slots[toIndex].Definition?.Name ?? "vazio"}");

			EmitSignal(SignalName.InventoryChanged);
			return true;
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
		public bool EquipItem(int slotIndex)
		{
			if (slotIndex < 0 || slotIndex >= INVENTORY_SIZE)
				return false;

			var slot = slots[slotIndex];

			if (slot.IsEmpty() || slot.Definition == null || !slot.Definition.IsEquippable)
				return false;

			equippedDefinition = slot.Definition;
			equippedSlotIndex  = slotIndex;

			EmitSignal(SignalName.ItemEquipped, slotIndex);
			return true;
		}

		public void UnequipItem()
		{
			equippedDefinition = null;
			equippedSlotIndex  = -1;
		}

		public ItemDefinition GetEquippedItem()
		{
			return equippedDefinition;
		}

		public int GetEquippedSlotIndex()
		{
			return equippedSlotIndex;
		}

		public bool HasEquippedItem()
		{
			return equippedDefinition != null;
		}

		public int CountItem(string itemName)
		{
			int count = 0;
			for (int i = 0; i < INVENTORY_SIZE; i++)
			{
				if (!slots[i].IsEmpty() && slots[i].Definition?.Name == itemName)
				{
					count += slots[i].Quantity;
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
				if (i == equippedSlotIndex)
				{
					continue;
				}

				if (slots[i].IsEmpty())
				{
					continue;
				}

				var chargesProp = slots[i].Properties.OfType<ChargesProperty>().FirstOrDefault();
				if (chargesProp != null && chargesProp.ChargeType == chargeType)
				{
					count += slots[i].Quantity;
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
				if (i == equippedSlotIndex)
				{
					continue;
				}

				if (slots[i].IsEmpty())
				{
					continue;
				}

				var chargesProp = slots[i].Properties.OfType<ChargesProperty>().FirstOrDefault();
				if (chargesProp == null || chargesProp.ChargeType != chargeType)
				{
					continue;
				}

				int toRemove = Mathf.Min(quantity - removed, slots[i].Quantity);
				slots[i].Quantity -= toRemove;
				removed += toRemove;

				if (slots[i].Quantity <= 0)
				{
					slots[i].Clear();
				}
			}

			if (removed > 0)
			{
				EmitSignal(SignalName.InventoryChanged);
			}

			return removed;
		}

		public bool IsFull()
		{
			for (int i = 0; i < INVENTORY_SIZE; i++)
			{
				if (slots[i].IsEmpty()) return false;
			}
			return true;
		}

		public int GetEmptySlotCount()
		{
			int count = 0;
			for (int i = 0; i < INVENTORY_SIZE; i++)
			{
				if (slots[i].IsEmpty()) count++;
			}
			return count;
		}

		public void NotifyInventoryChanged()
		{
			EmitSignal(SignalName.InventoryChanged);
		}
	} 
}
