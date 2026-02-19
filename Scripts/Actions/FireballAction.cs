using Godot;
using Jogo25D;
using Jogo25D.Characters;
using Jogo25D.Items;
using Jogo25D.Scripts.Weapons;
using Jogo25D.Weapons;
using System;
using System.Buffers;
using System.Reflection.PortableExecutable;

namespace Jogo25D.Scripts.Actions
{
    public class FireballAction : PlayerAction
    {
        [Export] public float DashSpeed { get; set; } = 800.0f;
        [Export] public Vector2 DashDirection { get; private set; } = Vector2.Zero;
        [Export] public float MovementInfluence { get; set; } = 0.4f;
        [Export] public Item Characteristics { get; set; } = new Item("Fireball", ItemType.WeaponRanged);

        private Weapon weapon;

        public FireballAction(Player player) : base(player)
        {
            Duration = 0.2f;
            Cooldown = 1f;
            MaxCharges = 2;
            CurrentCharges = MaxCharges;
            ActionName = "Fireball";
            Characteristics.Description = "Um arco para ataques à distância";
            Characteristics.IsEquippable = true;
            Characteristics.Damage = 1;
            Characteristics.InfiniteCharges = true;
            Characteristics.MaxCharges = 1;
            Characteristics.AttackCooldown = 0.8f;
            Characteristics.AttackRange = 1500f; // Alcance máximo: 1500 unidades
            Characteristics.AttackArea = 50f; // Tamanho do projétil
            Characteristics.ProjectileSpeed = 750f; // Velocidade: 750 u/s → Lifetime = 1500/750 = 2s
            Characteristics.ProjectileScene = GD.Load<PackedScene>("res://Scenes/Entities/Projectile.tscn");
            weapon = WeaponFactory.Use(Characteristics, player);

            NodePlayer.AddChild(weapon);

            weapon.OnEquip();
        }

        public override void OnStartAction(float delta)
        {
            Console.WriteLine("OnStartAction");

            var direction = (NodePlayer.Controls.MousePosition - NodePlayer.GlobalPosition).Normalized();

            weapon.Attack(direction);
        }

        public override void OnFinishedAction(float delta)
        {

        }

        public override void OnUpdateWhileActive(float delta)
        {

        }

        public override bool OnStartActionValidation(float delta)
        {
            return NodePlayer.Controls.InputAbility && CanUse;
        }

        public override void OnEnableAction(float delta)
        {
            
        }
    }
}
