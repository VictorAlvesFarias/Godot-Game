using Godot;
using System;

namespace Jogo25D.Items
{
    public partial class Item : Resource
    {
        public string ItemName { get; set; } = "Item";
        public string Description { get; set; } = "";
        public Texture2D Icon { get; set; }
        public ItemType Type { get; set; } = ItemType.Collectible;
        public bool IsStackable { get; set; } = false;
        public int MaxStackSize { get; set; } = 1;
        public bool IsEquippable { get; set; } = false;
        public int Damage { get; set; } = 10;
        public float AttackCooldown { get; set; } = 0.5f;
        public float AttackRange { get; set; } = 1.5f;
        public float KnockbackForce { get; set; } = 200f;
        public float AttackArea { get; set; } = 25f;
        public PackedScene ProjectileScene { get; set; }
        public float ProjectileSpeed { get; set; } = 500f;
        public int MaxCharges { get; set; } = 1;
        public string ChargeType { get; set; } = "";
        public bool InfiniteCharges { get; set; } = true;
        public float ReloadCooldown { get; set; } = 1.0f;

        public float CooldownTimer { get; set; } = 0f;

        public Item(string name, ItemType type) 
        {
            Type = type;
            ItemName = name;
        }

        public static Item IronSword = new Item("Iron Sword", ItemType.WeaponMelee)
        {
            Damage = 15,
            AttackCooldown = 0.7f,
            AttackRange = 1.5f,
            KnockbackForce = 250f,
            AttackArea = 30f,
            IsEquippable = true
        };
    }
}
