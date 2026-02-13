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
        Material,
        Quest
    }

    /// <summary>
    /// Tipo de arma (para itens do tipo Weapon)
    /// </summary>
    public enum WeaponType
    {
        Melee,
        Ranged
    }

    /// <summary>
    /// Classe genérica para todos os itens do jogo
    /// </summary>
    public partial class Item : Resource
    {
        #region Propriedades Gerais
        [Export] public string ItemName { get; set; } = "Item";
        [Export] public string Description { get; set; } = "";
        [Export] public Texture2D Icon { get; set; }
        [Export] public ItemType Type { get; set; } = ItemType.Collectible;
        [Export] public bool IsStackable { get; set; } = false;
        [Export] public int MaxStackSize { get; set; } = 1;
        [Export] public bool IsEquippable { get; set; } = false;
        #endregion

        #region Propriedades de Armas
        [Export] public WeaponType WeaponType { get; set; } = WeaponType.Melee;
        [Export] public int Damage { get; set; } = 10;
        [Export] public float AttackCooldown { get; set; } = 0.5f;
        [Export] public float AttackRange { get; set; } = 1.5f;
        [Export] public float KnockbackForce { get; set; } = 200f;
        
        // Para armas ranged
        [Export] public PackedScene ProjectileScene { get; set; }
        [Export] public float ProjectileSpeed { get; set; } = 500f;
        #endregion

        // Referência para o node visual da arma (se existir)
        public Node2D WeaponNode { get; set; }
        
        // Timer de cooldown (gerenciado pelo sistema de ataque)
        public float CooldownTimer { get; set; } = 0f;
        
        public bool CanAttack => CooldownTimer <= 0f;

        public Item() { }
        
        public Item(string name, ItemType type)
        {
            ItemName = name;
            Type = type;
            IsEquippable = (type == ItemType.Weapon);
        }

        /// <summary>
        /// Inicia o cooldown do ataque
        /// </summary>
        public void StartCooldown()
        {
            CooldownTimer = AttackCooldown;
        }

        /// <summary>
        /// Atualiza o cooldown (deve ser chamado no _Process)
        /// </summary>
        public void UpdateCooldown(float delta)
        {
            if (CooldownTimer > 0f)
            {
                CooldownTimer -= delta;
            }
        }
    }

    /// <summary>
    /// Representa um slot no inventário com item e quantidade
    /// </summary>
    public class ItemSlot
    {
        public Item Item { get; set; }
        public int Quantity { get; set; } = 0;
        
        public bool IsEmpty => Item == null || Quantity <= 0;
        
        public ItemSlot() { }
        
        public ItemSlot(Item item, int quantity = 1)
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
