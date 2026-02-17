using Godot;
using System;

namespace Jogo25D.Weapons
{
    public abstract partial class Weapon : Node2D
    {
        [Export] public string WeaponName { get; set; } = "Weapon";
        [Export] public int Damage { get; set; } = 1;
        [Export] public float AttackCooldown { get; set; } = 0.5f;
        [Export] public Texture2D Icon { get; set; }
        [Export] public float WeaponOffset { get; set; } = 25.0f;
        
        protected float cooldownTimer = 0f;
        protected Node2D owner;
        protected Node2D weaponHolder;
        protected Vector2 lastAttackDirection = Vector2.Right;

        public bool CanAttack => cooldownTimer <= 0f;

        public override void _Ready()
        {
            owner = GetParent<Node2D>();
            
            weaponHolder = new Node2D();
            weaponHolder.Name = "WeaponHolder";
            AddChild(weaponHolder);
        }

        public override void _Process(double delta)
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= (float)delta;
            }
            
            UpdateWeaponPosition();
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
            return;
        }

        protected void StartCooldown()
        {
            cooldownTimer = AttackCooldown;
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
