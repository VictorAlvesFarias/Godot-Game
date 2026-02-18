using Godot;
using System;
using System.Collections.Generic;
using Jogo25D.Items;

namespace Jogo25D.Systems
{
    public partial class Inventory : Node
    {
        [Signal]
        public delegate void InventoryChangedEventHandler();

        [Signal]
        public delegate void ItemEquippedEventHandler(Item item, int slotIndex);

        private const int INVENTORY_SIZE = 16;
        private ItemSlot[] slots = new ItemSlot[INVENTORY_SIZE];
        private Item equippedItem;
        private int equippedSlotIndex = -1;

        public override void _Ready()
        {
            for (int i = 0; i < INVENTORY_SIZE; i++)
            {
                slots[i] = new ItemSlot();
            }
        }

        public override void _Process(double delta)
        {
            if (equippedItem != null)
            {
                equippedItem.UpdateCooldown((float)delta);
            }
        }

        public bool AddItem(Item item, int quantity = 1)
        {
            if (item == null) return false;

            if (item.IsStackable)
            {
                for (int i = 0; i < INVENTORY_SIZE; i++)
                {
                    if (!slots[i].IsEmpty && slots[i].Item?.ItemName == item.ItemName)
                    {
                        if (slots[i].CanAddMore())
                        {
                            int spaceLeft = item.MaxStackSize - slots[i].Quantity;
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
                if (slots[i].IsEmpty)
                {
                    slots[i].Item = item;
                    slots[i].Quantity = quantity;
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
            if (slot.IsEmpty) return false;

            slot.Quantity -= quantity;

            if (slot.Quantity <= 0)
            {
                slot.Clear();
            }

            EmitSignal(SignalName.InventoryChanged);
            return true;
        }

        public ItemSlot GetSlot(int index)
        {
            if (index < 0 || index >= INVENTORY_SIZE) return null;
            return slots[index];
        }

        public ItemSlot[] GetAllSlots()
        {
            return slots;
        }

        public void Clear()
        {
            for (int i = 0; i < INVENTORY_SIZE; i++)
            {
                slots[i].Clear();
            }

            equippedItem = null;
            equippedSlotIndex = -1;

            EmitSignal(SignalName.InventoryChanged);
        }

        public bool SwapSlots(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= INVENTORY_SIZE) return false;
            if (toIndex < 0 || toIndex >= INVENTORY_SIZE) return false;
            if (fromIndex == toIndex) return false;

            GD.Print($"Trocando slot {fromIndex} ({slots[fromIndex].Item?.ItemName ?? "vazio"}) com slot {toIndex} ({slots[toIndex].Item?.ItemName ?? "vazio"})");

            Item tempItem = slots[fromIndex].Item;
            int tempQuantity = slots[fromIndex].Quantity;

            slots[fromIndex].Item = slots[toIndex].Item;
            slots[fromIndex].Quantity = slots[toIndex].Quantity;

            slots[toIndex].Item = tempItem;
            slots[toIndex].Quantity = tempQuantity;

            GD.Print($"Após troca - slot {fromIndex}: {slots[fromIndex].Item?.ItemName ?? "vazio"}, slot {toIndex}: {slots[toIndex].Item?.ItemName ?? "vazio"}"); ;

            EmitSignal(SignalName.InventoryChanged);
            return true;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
        public bool EquipItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= INVENTORY_SIZE)
                return false;

            var slot = slots[slotIndex];

            if (slot.IsEmpty || !slot.Item.IsEquippable)
                return false;

            equippedItem = slot.Item;
            equippedSlotIndex = slotIndex;

            EmitSignal(SignalName.ItemEquipped, equippedItem, slotIndex);
            return true;
        }

        public void UnequipItem()
        {
            equippedItem = null;
            equippedSlotIndex = -1;
        }

        public Item GetEquippedItem()
        {
            return equippedItem;
        }

        public int GetEquippedSlotIndex()
        {
            return equippedSlotIndex;
        }

        public bool HasEquippedItem()
        {
            return equippedItem != null;
        }

        public int CountItem(string itemName)
        {
            int count = 0;
            for (int i = 0; i < INVENTORY_SIZE; i++)
            {
                if (!slots[i].IsEmpty && slots[i].Item.ItemName == itemName)
                {
                    count += slots[i].Quantity;
                }
            }
            return count;
        }

        public int CountAmmoByChargeType(string chargeType)
        {
            if (string.IsNullOrEmpty(chargeType))
                return 0;

            int count = 0;
            for (int i = 0; i < INVENTORY_SIZE; i++)
            {
                // Não contar o slot equipado (a arma) como munição de si mesma
                if (i == equippedSlotIndex)
                    continue;

                if (!slots[i].IsEmpty && slots[i].Item.ChargeType == chargeType)
                {
                    count += slots[i].Quantity;
                }
            }
            return count;
        }

        /// <summary>Remove munição do inventário pelo tipo. Retorna a quantidade removida.</summary>
        public int RemoveAmmoByChargeType(string chargeType, int quantity)
        {
            if (string.IsNullOrEmpty(chargeType) || quantity <= 0)
                return 0;

            int removed = 0;
            for (int i = 0; i < INVENTORY_SIZE && removed < quantity; i++)
            {
                if (i == equippedSlotIndex)
                    continue;

                if (slots[i].IsEmpty || slots[i].Item.ChargeType != chargeType)
                    continue;

                int toRemove = Mathf.Min(quantity - removed, slots[i].Quantity);
                slots[i].Quantity -= toRemove;
                removed += toRemove;

                if (slots[i].Quantity <= 0)
                    slots[i].Clear();
            }

            if (removed > 0)
                EmitSignal(SignalName.InventoryChanged);

            return removed;
        }

        public bool IsFull()
        {
            for (int i = 0; i < INVENTORY_SIZE; i++)
            {
                if (slots[i].IsEmpty) return false;
            }
            return true;
        }

        public int GetEmptySlotCount()
        {
            int count = 0;
            for (int i = 0; i < INVENTORY_SIZE; i++)
            {
                if (slots[i].IsEmpty) count++;
            }
            return count;
        }

        public void NotifyInventoryChanged()
        {
            EmitSignal(SignalName.InventoryChanged);
        }
    } 
}