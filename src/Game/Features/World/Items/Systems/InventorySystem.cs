using Godot;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Items;
using System.Collections.Generic;

namespace Jogo25D.Systems
{
    public static class InventorySystem
    {
        #region Core - Actions

        public static bool AddItem(InventoryData inv, ItemData item)
        {
            if (inv == null || item == null)
            {
                return false;
            }

            EnsureSize(inv);

            var definition = ItemFactory.Create(item.Id);

            if (definition == null)
            {
                GD.PushWarning($"[Inventory.AddItem] Item desconhecido '{item.Id}'");

                return false;
            }

            if (definition.Stackable)
            {
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
                    inv.Items[i] = item;

                    return true;
                }

                var defSlot = ItemFactory.Create(itemSlot.Id);

                if (defSlot != null && defSlot.IsEmpty(itemSlot))
                {
                    inv.Items[i] = item;

                    return true;
                }
            }

            return false;
        }

        // Quantidade total de um item (somando todos os slots com o mesmo id).
        public static int CountItem(InventoryData inv, string itemId)
        {
            var total = 0;

            foreach (var slot in inv?.Items ?? new Godot.Collections.Array<ItemData>())
            {
                if (slot != null && slot.Id == itemId)
                {
                    total += slot.Quantity;
                }
            }

            return total;
        }

        // Se o item caberia no inventario (empilhando ou num slot vazio), sem alterar nada. removals: quantidades que vao
        // sair de cada slot antes (por instanceId); um slot que fica zerado conta como vazio.
        public static bool CanAdd(InventoryData inv, ItemData item, IReadOnlyDictionary<long, int> removals = null)
        {
            if (inv == null || item == null)
            {
                return false;
            }

            EnsureSize(inv);

            var definition = ItemFactory.Create(item.Id);

            if (definition == null)
            {
                return false;
            }

            var remaining = item.Quantity;

            for (int i = 0; i < inv.Size; i++)
            {
                var slot = inv.Items[i];
                var quantity = slot == null ? 0 : slot.Quantity - (removals?.GetValueOrDefault(slot.InstanceId) ?? 0);

                if (slot == null || quantity <= 0 || ItemFactory.Create(slot.Id)?.IsEmpty(slot) == true)
                {
                    return true;
                }

                if (definition.Stackable && slot.Id == definition.Id)
                {
                    remaining -= Mathf.Max(0, definition.MaxStackSize - quantity);

                    if (remaining <= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Slots (instanceId, quantidade) de onde sairiam quantity unidades do item, na ordem do inventario. Nao altera nada;
        // vazio quando nao ha o bastante.
        public static List<(long InstanceId, int Quantity)> TakeItems(InventoryData inv, string itemId, int quantity)
        {
            var taken = new List<(long, int)>();
            var missing = quantity;

            for (int i = 0; i < (inv?.Size ?? 0) && missing > 0; i++)
            {
                var slot = inv.Items[i];

                if (slot == null || slot.Id != itemId || slot.Quantity <= 0)
                {
                    continue;
                }

                var take = Mathf.Min(missing, slot.Quantity);

                taken.Add((slot.InstanceId, take));
                missing -= take;
            }

            return missing > 0 ? new List<(long, int)>() : taken;
        }

        public static bool RemoveItem(InventoryData inv, long instanceId, int quantity = 1)
        {
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

        public static bool MoveItem(InventoryData inv, long instanceId, int toIndex)
        {
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

        public static ItemData GetSlot(InventoryData inv, int index)
        {
            if (inv == null || index < 0 || index >= inv.Size || index >= inv.Items.Count)
            {
                return null;
            }

            return inv.Items[index];
        }

        public static int FindSlotIndex(InventoryData inv, long instanceId)
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

        public static ItemData FindItem(InventoryData inv, long instanceId)
        {
            var index = FindSlotIndex(inv, instanceId);

            return index < 0 ? null : inv.Items[index];
        }

        public static void EnsureSize(InventoryData inv)
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
