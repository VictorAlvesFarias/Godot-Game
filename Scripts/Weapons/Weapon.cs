using Godot;
using System;
using Jogo25D.Characters;
using Jogo25D.Systems;

namespace Jogo25D.Weapons
{
    public abstract partial class Weapon : Node2D
    {
        [Export] public string WeaponName { get; set; } = "Weapon";
        [Export] public int Damage { get; set; } = 1;
        [Export] public float AttackCooldown { get; set; } = 0.5f;
        [Export] public Texture2D Icon { get; set; }
        [Export] public float WeaponOffset { get; set; } = 25.0f;
        [Export] public int MaxCharges { get; set; } = 1;
        [Export] public int CurrentCharges { get; set; } = 1;
        [Export] public int InventoryCharges { get; set; } = 0;
        [Export] public string ChargeType { get; set; } = "";
        [Export] public bool InfiniteCharges { get; set; } = true;
        [Export] public float ReloadCooldown { get; set; } = 1.0f;
        [Export] public float Range { get; set; } = 1000.0f;
        [Export] public float Area { get; set; } = 25.0f;

        protected float cooldownTimer = 0f;
        protected float reloadTimer = 0f;
        protected Player owner;
        protected Node2D weaponHolder;
        protected Vector2 lastAttackDirection = Vector2.Right;

        public Weapon(Player player)
        {
            owner = player;
        }

        public bool CanAttack()
        {
            return cooldownTimer <= 0f && !IsReloading() && (InfiniteCharges || CurrentCharges > 0);
        }

        public bool CanReload()
        {
            return !InfiniteCharges && !IsReloading() && CurrentCharges < MaxCharges && InventoryCharges > 0;
        }

        public bool IsReloading()
        { 
            return reloadTimer > 0f;
        }

        public float GetReloadProgress() 
        { 
            return ReloadCooldown > 0f ? 1f - (reloadTimer / ReloadCooldown) : 1f;
        }

        public float GetRemainingReloadTime() 
        { 
           return reloadTimer;
        } 

        public override void _Ready()
        {
            weaponHolder = new Node2D();
            weaponHolder.Name = "WeaponHolder";

            AddChild(weaponHolder);
        }

        public override void _Process(double delta)
        {
            var dt = (float)delta;

            if (cooldownTimer > 0f)
            {
                cooldownTimer -= dt;
            }

            if (reloadTimer > 0f)
            {
                reloadTimer -= dt;
                if (reloadTimer <= 0f)
                {
                    FinishReload();
                }
            }

            RefreshInventoryCharges();

            UpdateWeaponPosition();
        }

        protected virtual void RefreshInventoryCharges()
        {
            if (string.IsNullOrEmpty(ChargeType) || owner == null)
                return;

            if (owner is Player player && player.Inventory != null)
            {
                InventoryCharges = player.Inventory.CountAmmoByChargeType(ChargeType);
            }
        }
        
        protected virtual void UpdateWeaponPosition()
        {
            if (weaponHolder == null || lastAttackDirection.LengthSquared() <= 0.01f)
                return;
            
            weaponHolder.Rotation = lastAttackDirection.Angle();
        
            if (lastAttackDirection.X < 0)
            {
                weaponHolder.Position = new Vector2(-WeaponOffset, 0);
                weaponHolder.Scale = new Vector2(1, -1);
            }
            else
            {
                weaponHolder.Position = new Vector2(WeaponOffset, 0);
                weaponHolder.Scale = new Vector2(1, 1);
            }
        }

        public virtual void Attack(Vector2 direction)
        {
            lastAttackDirection = direction.Normalized();

            if (!InfiniteCharges && CurrentCharges > 0)
            {
                CurrentCharges--;
            }
        }

        protected void StartCooldown()
        {
            cooldownTimer = AttackCooldown;
        }

        public virtual bool Reload()
        {
            if (!CanReload() || owner == null || ReloadCooldown <= 0f)
                return false;

            reloadTimer = ReloadCooldown;
            return true;
        }

        protected virtual void FinishReload()
        {
            reloadTimer = 0f;
            if (owner == null)
                return;

            int needed = MaxCharges - CurrentCharges;
            if (owner is Player player && player.Inventory != null && needed > 0)
            {
                int taken = player.Inventory.RemoveAmmoByChargeType(ChargeType, needed);
                CurrentCharges += taken;
            }
        }

        public virtual void OnEquip()
        {
            Visible = true;
            
            SetProcess(true);
        }

        public virtual void OnUnequip()
        {
            Visible = false;
        
            SetProcess(false);
        }
    }
}
