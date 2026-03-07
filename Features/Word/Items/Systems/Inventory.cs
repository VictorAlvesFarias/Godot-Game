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
		[Signal]
		public delegate void InventoryChangedEventHandler();

		[Signal]
		public delegate void ItemEquippedEventHandler(int slotIndex);

		public const int INVENTORY_SIZE = 16;
		public Player LocalPlayer { get; set; }

		public override void _Ready()
		{
			LocalPlayer = GetOwner<Player>();
		}

		public ItemInstance GetSlot(int index)
		{
			if (index < 0 || index >= INVENTORY_SIZE)
			{ 
				return null;
			}

			return LocalPlayer.Items[index];
		}

		public bool AddItem(ItemDefinition definition, int quantity = 1)
		{
			if (definition == null)
			{
				return false;
			}

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
								return true;
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
					
					return true;
				}
			}

			return false;
		}

		public bool RemoveItem(int slotIndex, int quantity = 1)
		{
			if (slotIndex < 0 || slotIndex >= INVENTORY_SIZE)
			{ 
				return false;
			}

			var slot = LocalPlayer.Items[slotIndex];

			if (slot.IsEmpty())
			{ 
				return false;
			}

			slot.Quantity -= quantity;

			if (slot.Quantity <= 0)
			{
				slot.Clear();
			}

			EmitSignal(SignalName.InventoryChanged);

			return true;
		}

		public bool SwapSlots(int fromIndex, int toIndex)
		{
			if (fromIndex < 0 || fromIndex >= INVENTORY_SIZE)
			{
				return false;
			}

			if (toIndex < 0 || toIndex >= INVENTORY_SIZE)
			{ 
				return false;
			}

			if (fromIndex == toIndex)
			{ 
				return false;
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
			return true;
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
		public bool EquipItem(int slotIndex)
		{
			if (slotIndex < 0 || slotIndex >= INVENTORY_SIZE)
			{
				return false;
			}

			var slot = LocalPlayer.Items[slotIndex];

			if (slot.IsEmpty() || slot.Definition == null || !slot.Definition.IsEquippable)
			{
				return false;
			}

			LocalPlayer.EquippedDefinition = slot.Definition;
			LocalPlayer.EquippedSlotIndex = slotIndex;

			EmitSignal(SignalName.ItemEquipped, slotIndex);

			return true;
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
	} 
}