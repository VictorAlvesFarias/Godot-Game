using Godot;
using System.Collections.Generic;
using System.Linq;
using Jogo25D.Properties;
using Jogo25D.Effects;
using Jogo25D.Hitboxes;
using Jogo25D.Characters;

namespace Jogo25D.Items
{
    public class ItemDefinition
    {
        #region Identity

        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public Texture2D Icon { get; set; }
        public ItemType Type { get; init; }

        #endregion

        #region Stacking

        public bool Stackable { get; init; } = false;
        public int MaxStackSize { get; init; } = 99;

        #endregion

        #region Combate V2

        public float Cooldown { get; init; } = 0.5f;
        public List<BaseProperty> Properties { get; set; } = new();
        public List<EffectDefinition> OnHitEffects { get; set; } = new();
        public PackedScene HitboxScene { get; set; }

        #endregion

        #region Helpers

        public bool IsEquippable
        {
            get
            {
                return Type.IsWeapon();
            }
        }

        #endregion

        #region Métodos do item

        public virtual void Use(Player player, ItemInstance instance)
        {
        }

        public virtual void Attack(Player player, ItemRechargeableInstance instance, Vector2 direction)
        {
            if (!instance.CanAttack())
            {
                GD.Print($"[Attack] Bloqueado - cooldown={instance.CooldownRemaining:F2} reloading={instance.IsReloading} charges={instance.CurrentCharges}");
                return;
            }

            var damageProp = instance.Properties.OfType<DamageProperty>().FirstOrDefault();
            if (damageProp == null || HitboxScene == null)
            {
                GD.Print($"[Attack] Bloqueado - damageProp={damageProp != null} HitboxScene={HitboxScene != null}");
                return;
            }

            var weaponProp = instance.Properties.OfType<WeaponProperty>().FirstOrDefault();
            var chargesProp = instance.Properties.OfType<ChargesProperty>().FirstOrDefault();

            float attackRange = weaponProp != null ? weaponProp.AttackRange : 80f;
            float attackArea = weaponProp != null ? weaponProp.AttackArea : 25f;
            float projectileSpeed = weaponProp != null ? weaponProp.ProjectileSpeed : 500f;
            bool infiniteCharges = chargesProp != null ? chargesProp.InfiniteCharges : true;

            var critProp = instance.Properties.OfType<CritProperty>().FirstOrDefault();
            float multiplier = 1f;

            if (critProp != null && GD.Randf() <= critProp.CritChance)
            {
                multiplier += critProp.CritDamage;
            }

            int finalDamage = (int)(damageProp.DamageAmount * multiplier);

            if (HitboxScene.Instantiate<Area2D>() is not BaseHitbox hitbox)
            {
                GD.Print($"[Attack] Falha ao instanciar hitbox da cena '{HitboxScene.ResourcePath}'");
                return;
            }

            hitbox.Initialize(
                new DamageInfo
                {
                    Amount = finalDamage,
                    Type = damageProp.DamageType,
                    SourcePeerId = (int)player.PeerId
                },
                instance.OnHitEffects,
                player
            );

            Vector2 dir;
            if (direction.LengthSquared() > 0.001f)
            {
                dir = direction.Normalized();
            }
            else
            {
                dir = Vector2.Right;
            }

            float angle = dir.Angle();

            if (hitbox is ProjectileHitbox proj)
            {
                proj.Direction = dir;
                proj.Speed = projectileSpeed;
                proj.Lifetime = attackRange / Mathf.Max(projectileSpeed, 1f);

                hitbox.GlobalPosition = player.GlobalPosition + dir * 60f;
                hitbox.Scale = Vector2.One * (attackArea / 25f);
                hitbox.Rotation = angle;
            }
            else if (hitbox is MeleeHitbox melee)
            {
                melee.Offset = dir * attackRange * 0.7f;
                hitbox.GlobalPosition = player.GlobalPosition + melee.Offset;
                hitbox.Rotation = angle;
            }
            else
            {
                hitbox.GlobalPosition = player.GlobalPosition;
                hitbox.Rotation = angle;
            }

            player.GetParent().AddChild(hitbox);

            GD.Print($"[Attack] Hitbox '{hitbox.GetType().Name}' criado - dano={finalDamage} dir={dir}");

            instance.TriggerCooldown();

            if (!infiniteCharges)
            {
                instance.ConsumeCharge();
            }
        }

        public virtual void OnEquip(Player player, ItemInstance instance)
        {
        }

        public virtual void OnUnequip(Player player, ItemInstance instance)
        {
        }

        #endregion
    }
}