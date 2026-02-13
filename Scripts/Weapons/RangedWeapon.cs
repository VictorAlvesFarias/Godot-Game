using Godot;
using System;

namespace Jogo25D.Weapons
{
    public partial class RangedWeapon : Weapon
    {
        [Export] public PackedScene BulletScene { get; set; }
        [Export] public float BulletSpeed { get; set; } = 800.0f;
        [Export] public float Range { get; set; } = 1000.0f; // Alcance máximo do projétil (distância)
        [Export] public float Area { get; set; } = 25.0f; // Tamanho/escala do projétil

        public override void _Ready()
        {
            base._Ready();
            
            if (BulletScene == null)
            {
                BulletScene = GD.Load<PackedScene>("res://Scenes/Entities/Projectile.tscn");
            }
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

            var bullet = BulletScene.Instantiate<Projectile>();
            
            bullet.Speed = BulletSpeed;
            bullet.Direction = direction.Normalized();
            bullet.Damage = Damage;
            bullet.Lifetime = Range / BulletSpeed; // Calculado automaticamente: distância / velocidade
            bullet.Shooter = owner;
            bullet.Scale = Vector2.One * (Area / 25.0f); // Tamanho do projétil baseado no AttackArea
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
