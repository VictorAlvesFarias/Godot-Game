using Godot;
using System;
using System.Collections.Generic;
using Jogo25D.Items;

namespace Jogo25D.Systems
{
    /// <summary>
    /// Sistema de gerenciamento de inventário com 16 slots
    /// </summary>
    public partial class InventorySystem : Node
    {
        [Signal]
        public delegate void InventoryChangedEventHandler();
        
        [Signal]
        public delegate void ItemEquippedEventHandler(InventoryItem item, int slotIndex);

        private const int INVENTORY_SIZE = 16;
        private ItemSlot[] slots = new ItemSlot[INVENTORY_SIZE];

        public override void _Ready()
        {
            // Inicializar slots vazios
            for (int i = 0; i < INVENTORY_SIZE; i++)
            {
                slots[i] = new ItemSlot();
            }
        }

        /// <summary>
        /// Adiciona um item ao inventário
        /// </summary>
        public bool AddItem(InventoryItem item, int quantity = 1)
        {
            if (item == null) return false;

            // Se o item é empilhável, tenta adicionar em slots existentes
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

            // Procura por slot vazio
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

        /// <summary>
        /// Remove um item do inventário
        /// </summary>
        public bool RemoveItem(int slotIndex, int quantity = 1)
        {
            if (slotIndex < 0 || slotIndex >= INVENTORY_SIZE) return false;
            if (slots[slotIndex].IsEmpty) return false;

            slots[slotIndex].Quantity -= quantity;
            
            if (slots[slotIndex].Quantity <= 0)
            {
                slots[slotIndex].Clear();
            }

            EmitSignal(SignalName.InventoryChanged);
            return true;
        }

        /// <summary>
        /// Obtém o item em um slot específico
        /// </summary>
        public ItemSlot GetSlot(int index)
        {
            if (index < 0 || index >= INVENTORY_SIZE) return null;
            return slots[index];
        }

        /// <summary>
        /// Move um item de um slot para outro
        /// </summary>
        public void MoveItem(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= INVENTORY_SIZE) return;
            if (toIndex < 0 || toIndex >= INVENTORY_SIZE) return;
            if (fromIndex == toIndex) return;

            var temp = slots[fromIndex];
            slots[fromIndex] = slots[toIndex];
            slots[toIndex] = temp;

            EmitSignal(SignalName.InventoryChanged);
        }

        /// <summary>
        /// Equipa um item (apenas weapons por enquanto)
        /// </summary>
        public void EquipItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= INVENTORY_SIZE)
            {
                GD.PrintErr($"[InventorySystem] Índice inválido: {slotIndex}");
                return;
            }
            
            var slot = slots[slotIndex];
            
            if (slot.IsEmpty)
            {
                GD.PrintErr($"[InventorySystem] Slot {slotIndex} está vazio!");
                return;
            }
            
            if (!slot.Item.IsEquippable)
            {
                GD.PrintErr($"[InventorySystem] Item {slot.Item.ItemName} não é equipável!");
                return;
            }

            EmitSignal(SignalName.ItemEquipped, slot.Item, slotIndex);
        }

        /// <summary>
        /// Retorna todos os slots do inventário
        /// </summary>
        public ItemSlot[] GetAllSlots()
        {
            return slots;
        }

        /// <summary>
        /// Verifica se o inventário está cheio
        /// </summary>
        public bool IsFull()
        {
            for (int i = 0; i < INVENTORY_SIZE; i++)
            {
                if (slots[i].IsEmpty) return false;
            }
            return true;
        }

        /// <summary>
        /// Conta quantos itens de um tipo específico existem no inventário
        /// </summary>
        public int CountItem(string itemName)
        {
            int count = 0;
            for (int i = 0; i < INVENTORY_SIZE; i++)
            {
                if (!slots[i].IsEmpty && slots[i].Item?.ItemName == itemName)
                {
                    count += slots[i].Quantity;
                }
            }
            return count;
        }
    }
}
