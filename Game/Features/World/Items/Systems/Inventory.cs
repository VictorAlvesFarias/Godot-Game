using Godot;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Items;

namespace Jogo25D.Systems
{
    public class Inventory
    {
        #region Core - Actions

        public bool AddItem(InventoryData inv, ItemData item)
        {
            GD.Print("[Inventory.AddItem] Starting method");

            if (inv == null || item == null)
            {
                return false;
            }

            EnsureSize(inv);

            var definition = ItemFactory.Create(item.Id);

            if (definition == null)
            {
                GD.Print($"[Inventory.AddItem] Unknown item id '{item.Id}'");

                return false;
            }

            GD.Print($"[Inventory.AddItem] Item received: {definition.Name} x{item.Quantity}");

            if (definition.Stackable)
            {
                GD.Print($"[Inventory.AddItem] Item is stackable");

                for (int i = 0; i < inv.Size; i++)
                {
                    var slot = inv.Items[i];

                    if (slot == null || slot.Id != definition.Id)
                    {
                        continue;
                    }

                    var spaceLeft = definition.MaxStackSize - slot.Quantity;

                    if (spaceLeft <= 0)
                    {
                        continue;
                    }

                    var toAdd = Mathf.Min(item.Quantity, spaceLeft);

                    GD.Print($"[Inventory.AddItem] Stacking {toAdd} onto existing slot {i}");

                    slot.Quantity += toAdd;
                    item.Quantity -= toAdd;

                    if (item.Quantity <= 0)
                    {
                        return true;
                    }
                }
            }

            for (int i = 0; i < inv.Size; i++)
            {
                var itemSlot = inv.Items[i];

                if (itemSlot == null)
                {
                    GD.Print($"[Inventory.AddItem] Placed {definition.Name} on slot {i}");

                    inv.Items[i] = item;

                    return true;
                }

                var defSlot = ItemFactory.Create(itemSlot.Id);

                if (defSlot != null && defSlot.IsEmpty(itemSlot))
                {
                    GD.Print($"[Inventory.AddItem] Placed {definition.Name} on slot {i}");

                    inv.Items[i] = item;

                    return true;
                }
            }

            GD.Print("[Inventory.AddItem] No empty slot found, item was NOT placed");

            return false;
        }

        public bool RemoveItem(InventoryData inv, long instanceId, int quantity = 1)
        {
            GD.Print($"[Inventory.RemoveItem] Starting method, instanceId={instanceId} quantity={quantity}");

            var slotIndex = FindSlotIndex(inv, instanceId);

            if (slotIndex < 0)
            {
                return false;
            }

            var slot = inv.Items[slotIndex];

            slot.Quantity -= quantity;

            if (slot.Quantity <= 0)
            {
                inv.Items[slotIndex] = null;
            }

            return true;
        }

        public bool MoveItem(InventoryData inv, long instanceId, int toIndex)
        {
            GD.Print($"[Inventory.MoveItem] Starting method, instanceId={instanceId} toIndex={toIndex}");

            if (inv == null || toIndex < 0 || toIndex >= inv.Size)
            {
                return false;
            }

            var fromIndex = FindSlotIndex(inv, instanceId);

            if (fromIndex < 0 || fromIndex == toIndex)
            {
                return false;
            }

            (inv.Items[fromIndex], inv.Items[toIndex]) = (inv.Items[toIndex], inv.Items[fromIndex]);

            return true;
        }

        #endregion

        #region Core - Information

        public ItemData GetSlot(InventoryData inv, int index)
        {
            if (inv == null || index < 0 || index >= inv.Size || index >= inv.Items.Count)
            {
                return null;
            }

            return inv.Items[index];
        }

        public int FindSlotIndex(InventoryData inv, long instanceId)
        {
            if (inv == null || instanceId <= 0)
            {
                return -1;
            }

            for (int i = 0; i < inv.Items.Count; i++)
            {
                if (inv.Items[i]?.InstanceId == instanceId)
                {
                    return i;
                }
            }

            return -1;
        }

        public ItemData FindItem(InventoryData inv, long instanceId)
        {
            var index = FindSlotIndex(inv, instanceId);

            return index < 0 ? null : inv.Items[index];
        }

        public void EnsureSize(InventoryData inv)
        {
            if (inv == null)
            {
                return;
            }

            while (inv.Items.Count < inv.Size)
            {
                inv.Items.Add(null);
            }
        }

        #endregion
    }
}
