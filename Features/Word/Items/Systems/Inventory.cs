using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Jogo25D.Characters;
using Jogo25D.Items;
using Jogo25D.Properties;
using Jogo25D.Effects;
using Jogo25D.Features.Word.Items.Resources;

namespace Jogo25D.Systems
{
    public partial class Inventory : Resource
    {
        #region Core - Actions

        public bool AddItem(InventoryData inv, ItemDefinitionData item)
        {
            GD.Print("[Inventory.AddItem] Starting method");

            if (inv == null || item == null)
            {
                return false;
            }

            EnsureSize(inv);

            var definition = ItemDB.Get(item.Id);

            if (definition == null)
            {
                GD.Print($"[Inventory.AddItem] Unknown item id '{item.Id}'");

                return false;
            }

            GD.Print($"[Inventory.AddItem] Item received: {definition.Name} x{item.Quantity}");

            //TODO: Verificar se os itens são exatamente iguais, funcionaliadde de munição encantada, ou talvez separa
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

                var defSlot = ItemDB.Get(itemSlot.Id);

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

        public bool RemoveItem(InventoryData inv, int slotIndex, int quantity = 1)
        {
            GD.Print($"[Inventory.RemoveItem] Starting method, slotIndex={slotIndex} quantity={quantity}");

            if (inv == null || slotIndex < 0 || slotIndex >= inv.Size)
            {
                return false;
            }

            var slot = inv.Items[slotIndex];

            if (slot == null)
            {
                return false;
            }

            slot.Quantity -= quantity;

            if (slot.Quantity <= 0)
            {
                inv.Items[slotIndex] = null;
            }

            return true;
        }

        public bool SwapSlots(InventoryData inv, int fromIndex, int toIndex)
        {
            GD.Print($"[Inventory.SwapSlots] Starting method, fromIndex={fromIndex} toIndex={toIndex}");

            if (inv == null || fromIndex < 0 || fromIndex >= inv.Size)
            {
                return false;
            }

            if (toIndex < 0 || toIndex >= inv.Size)
            {
                return false;
            }

            if (fromIndex == toIndex)
            {
                return false;
            }

            (inv.Items[fromIndex], inv.Items[toIndex]) = (inv.Items[toIndex], inv.Items[fromIndex]);

            return true;
        }

        #endregion

        #region Core - Information

        public ItemDefinitionData GetSlot(InventoryData inv, int index)
        {
            if (inv == null || index < 0 || index >= inv.Size || index >= inv.Items.Count)
            {
                return null;
            }

            return inv.Items[index];
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
