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
        
        protected float cooldownTimer = 0f;
        protected Node2D owner;

        public bool CanAttack => cooldownTimer <= 0f;

        public override void _Ready()
        {
            var parent = GetParent();

            if (parent != null)
            {
                owner = parent.GetParent<Node2D>();
            }
        }

        public override void _Process(double delta)
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= (float)delta;
            }
        }

        public virtual void Attack(Vector2 direction)
        {
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
