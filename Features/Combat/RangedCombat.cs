using Godot;
using Jogo25D.Characters;
using System;

namespace Jogo25D.Weapons
{
    public partial class RangedCombat : Combat
    {
        [Export] public PackedScene BulletScene { get; set; }
        [Export] public float BulletSpeed { get; set; } = 800.0f;

        public RangedCombat(Player player, PackedScene bulletScene) : base(player)
        {
            BulletScene = bulletScene;
        }

        public override void _Ready()
        {
            base._Ready();
        }

        public override void Attack(Vector2 direction)
        {
            GD.Print("=== Attack chamado ===");
            GD.Print("Direction: ", direction);

            if (!CanAttack() || BulletScene == null)
            {
                GD.PushWarning("Ataque bloqueado");

                GD.Print("CanAttack: ", CanAttack());
                GD.Print("BulletScene null: ", BulletScene == null);

                return;
            }

            if (owner == null)
            {
                GD.PushError("Owner is null when attacking");
                return;
            }

            base.Attack(direction);

            GD.Print("Instanciando projetil");

            var bullet = BulletScene.Instantiate<Projectile>();

            bullet.Speed = BulletSpeed;
            bullet.Direction = direction.Normalized();
            bullet.Damage = Damage;
            bullet.Lifetime = Range / BulletSpeed;
            bullet.Shooter = owner;
            bullet.Scale = Vector2.One * (Area / 25.0f);
            bullet.GlobalPosition = owner.GlobalPosition + (direction.Normalized() * 60.0f);

            GD.Print("Bullet configurada");
            GD.Print("Speed: ", bullet.Speed);
            GD.Print("Damage: ", bullet.Damage);
            GD.Print("Lifetime: ", bullet.Lifetime);
            GD.Print("Position: ", bullet.GlobalPosition);

            GetTree().Root.AddChild(bullet);

            GD.Print("Bullet adicionada na scene");

            StartCooldown();

            GD.Print("Cooldown iniciado");
        }

        public override void OnEquip()
        {
            base.OnEquip();
        }
    }
}
