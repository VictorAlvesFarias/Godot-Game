using Godot;
using System;

namespace Jogo25D.Items
{
    /// <summary>
    /// Tipo de item no inventário
    /// </summary>
    public enum ItemType
    {
        Weapon,
        Consumable,
        Collectible,
        Material
    }

    /// <summary>
    /// Classe base para todos os itens que podem estar no inventário
    /// </summary>
    public partial class InventoryItem : Resource
    {
        [Export] public string ItemName { get; set; } = "Item";
        [Export] public string Description { get; set; } = "";
        [Export] public Texture2D Icon { get; set; }
        [Export] public ItemType Type { get; set; } = ItemType.Collectible;
        [Export] public bool IsStackable { get; set; } = false;
        [Export] public int MaxStackSize { get; set; } = 1;
        [Export] public bool IsEquippable { get; set; } = false;
        
        // Referência para o objeto real (ex: Weapon node)
        public Node ItemReference { get; set; }
        
        public InventoryItem() { }
        
        public InventoryItem(string name, string description, Texture2D icon, ItemType type)
        {
            ItemName = name;
            Description = description;
            Icon = icon;
            Type = type;
        }

        /// <summary>
        /// Cria um InventoryItem a partir de uma Weapon
        /// </summary>
        public static InventoryItem FromWeapon(Weapons.Weapon weapon)
        {
            var item = new InventoryItem
            {
                ItemName = weapon.WeaponName,
                Description = $"Dano: {weapon.Damage} | Cooldown: {weapon.AttackCooldown}s",
                Icon = weapon.Icon,
                Type = ItemType.Weapon,
                IsStackable = false,
                IsEquippable = true,
                ItemReference = weapon
            };
            return item;
        }
    }

    /// <summary>
    /// Representa um slot no inventário com item e quantidade
    /// </summary>
    public class ItemSlot
    {
        public InventoryItem Item { get; set; }
        public int Quantity { get; set; } = 0;
        
        public bool IsEmpty => Item == null || Quantity <= 0;
        
        public ItemSlot() { }
        
        public ItemSlot(InventoryItem item, int quantity = 1)
        {
            Item = item;
            Quantity = quantity;
        }

        public void Clear()
        {
            Item = null;
            Quantity = 0;
        }

        public bool CanAddMore()
        {
            if (IsEmpty || Item == null) return true;
            return Item.IsStackable && Quantity < Item.MaxStackSize;
        }
    }
}
