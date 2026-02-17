using Godot;
using System;

namespace Jogo25D.Items
{
    public partial class Item : Resource
    {
        [Export] public string ItemName { get; set; } = "Item";
        [Export] public string Description { get; set; } = "";
        [Export] public Texture2D Icon { get; set; }
        [Export] public ItemType Type { get; set; } = ItemType.Collectible;
        [Export] public bool IsStackable { get; set; } = false;
        [Export] public int MaxStackSize { get; set; } = 1;
        [Export] public bool IsEquippable { get; set; } = false;
        [Export] public int Damage { get; set; } = 10;
        [Export] public float AttackCooldown { get; set; } = 0.5f;
        [Export] public float AttackRange { get; set; } = 1.5f;
        [Export] public float KnockbackForce { get; set; } = 200f;
        [Export] public float AttackArea { get; set; } = 25f;
        [Export] public PackedScene ProjectileScene { get; set; }
        [Export] public float ProjectileSpeed { get; set; } = 500f;

        public Node2D WeaponNode { get; set; }
        
        public float CooldownTimer { get; set; } = 0f;

        public Item() 
        {
            //no-actions
        }

        public Item(string name, ItemType type) 
        {
            Type = type;
            ItemName = name;
        }

        public void StartCooldown()
        {
            CooldownTimer = AttackCooldown;
        }

        public void UpdateCooldown(float delta)
        {
            if (CooldownTimer > 0f)
            {
                CooldownTimer -= delta;
            }
        }
    }
}
