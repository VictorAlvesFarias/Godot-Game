using Godot;
using System;
using System.Collections.Generic;
using Jogo25D.Items;

namespace Jogo25D.Systems
{
    /// <summary>
    /// Sistema de inventário simplificado com 16 slots
    /// </summary>
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
            // Inicializar slots vazios
            for (int i = 0; i < INVENTORY_SIZE; i++)
            {
                slots[i] = new ItemSlot();
            }
        }

        public override void _Process(double delta)
        {
            // Atualizar cooldown do item equipado
            if (equippedItem != null)
            {
                equippedItem.UpdateCooldown((float)delta);
            }
        }

        #region Gerenciamento de Itens

        /// <summary>
        /// Adiciona um item ao inventário
        /// </summary>
        public bool AddItem(Item item, int quantity = 1)
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

        /// <summary>
        /// Obtém um item pelo índice
        /// </summary>
        public ItemSlot GetSlot(int index)
        {
            if (index < 0 || index >= INVENTORY_SIZE) return null;
            return slots[index];
        }

        /// <summary>
        /// Obtém todos os slots
        /// </summary>
        public ItemSlot[] GetAllSlots()
        {
            return slots;
        }

        /// <summary>
        /// Limpa todo o inventário
        /// </summary>
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

        #endregion

        #region Sistema de Equipar

        /// <summary>
        /// Equipa um item do inventário
        /// </summary>
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

        /// <summary>
        /// Desequipa o item atual
        /// </summary>
        public void UnequipItem()
        {
            equippedItem = null;
            equippedSlotIndex = -1;
        }

        /// <summary>
        /// Obtém o item atualmente equipado
        /// </summary>
        public Item GetEquippedItem()
        {
            return equippedItem;
        }

        /// <summary>
        /// Obtém o índice do slot equipado
        /// </summary>
        public int GetEquippedSlotIndex()
        {
            return equippedSlotIndex;
        }

        /// <summary>
        /// Verifica se há um item equipado
        /// </summary>
        public bool HasEquippedItem()
        {
            return equippedItem != null;
        }

        #endregion

        #region Utilitários

        /// <summary>
        /// Conta quantos itens de um tipo específico existem no inventário
        /// </summary>
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
        /// Retorna o número de slots vazios
        /// </summary>
        public int GetEmptySlotCount()
        {
            int count = 0;
            for (int i = 0; i < INVENTORY_SIZE; i++)
            {
                if (slots[i].IsEmpty) count++;
            }
            return count;
        }

        #endregion
    }
}
