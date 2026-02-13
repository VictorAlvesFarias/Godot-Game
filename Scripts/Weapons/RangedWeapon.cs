using Godot;
using System;

namespace Jogo25D.Weapons
{
    public partial class RangedWeapon : Weapon
    {
        [Export] public PackedScene BulletScene { get; set; }
        [Export] public float BulletSpeed { get; set; } = 800.0f;
        [Export] public float BulletLifetime { get; set; } = 2.0f;
        [Export] public float FireRate { get; set; } = 0.3f;

        public override void _Ready()
        {
            base._Ready();
            
            if (BulletScene == null)
            {
                BulletScene = GD.Load<PackedScene>("res://Scenes/Entities/Bullet.tscn");
            }
            
            AttackCooldown = FireRate;
        }

        public override void Attack(Vector2 direction)
        {
            if (!CanAttack || BulletScene == null)
            {
                return;
            }

            if (owner == null)
            {
                return;
            }

            var bullet = BulletScene.Instantiate<Characters.PlayerAttack>();
            
            bullet.Speed = BulletSpeed;
            bullet.Direction = direction.Normalized();
            bullet.Damage = Damage;
            bullet.Lifetime = BulletLifetime;
            bullet.Shooter = owner;
            bullet.GlobalPosition = owner.GlobalPosition + (direction.Normalized() * 60.0f);

            GetTree().Root.AddChild(bullet);
            StartCooldown();
        }

        public override void OnEquip()
        {
            base.OnEquip();
        }
    }
}
