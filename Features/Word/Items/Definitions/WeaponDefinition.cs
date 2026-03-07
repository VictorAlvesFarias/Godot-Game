using Godot;
using System.Collections.Generic;
using System.Linq;
using Jogo25D.Properties;
using Jogo25D.Hitboxes;
using Jogo25D.Characters;
using Jogo25D.Systems;

namespace Jogo25D.Items
{
    public class WeaponDefinition : ItemDefinition
    {
        public override void OnEquip(Player player, ItemInstance instance)
        {
            if (player.AimIndicator != null)
            {
                player.AimIndicator.IsActive = true;
            }
        }

        public override void OnUnequip(Player player, ItemInstance instance)
        {
            player.AimIndicator?.Hide();
        }
        public override void Use(Player player, ItemInstance instance)
        {
            if (!instance.CanAttack())
            {
                GD.Print($"[Attack] Bloqueado - cooldown={instance.CooldownRemaining:F2} reloading={instance.IsReloading} charges={instance.CurrentCharges}");
                
                return;
            }

            var damageProps = instance.Properties.OfType<DamageProperty>().ToList();

            if (damageProps.Count == 0 || HitboxScene == null)
            {
                GD.Print($"[Attack] Bloqueado - damageProps={damageProps.Count} HitboxScene={HitboxScene != null}");
                
                return;
            }

            var weapon = instance.Properties.OfType<AttackProperty>().DefaultIfEmpty(new AttackProperty()).First();
            var charges = instance.Properties.OfType<ChargesProperty>().DefaultIfEmpty(new ChargesProperty()).First();
            var crit = instance.Properties.OfType<CritProperty>().DefaultIfEmpty(new CritProperty()).First();
            var damages = damageProps.ConvertAll(d => new DamageInfo
            {
                Amount = d.DamageAmount,
                Type = d.DamageType,
                SourcePeerId = (int)player.PeerId,
                CritChance = crit.CritChance,
                CritDamage = crit.CritDamage
            });

            if (HitboxScene.Instantiate<Area2D>() is not BaseHitbox hitbox)
            {
                GD.Print($"[Attack] Falha ao instanciar hitbox da cena '{HitboxScene.ResourcePath}'");
                
                return;
            }

            hitbox.Initialize(damages, instance.OnHitEffects, player);

            var rawDir = player.Input.MousePosition - player.GlobalPosition;
            var dir = rawDir.LengthSquared() > 0.001f ? rawDir.Normalized() : Vector2.Right;
            var angle = dir.Angle();

            if (hitbox is ProjectileHitbox proj)
            {
                proj.Direction = dir;
                proj.Speed = weapon.ProjectileSpeed;
                proj.Lifetime = weapon.AttackRange / Mathf.Max(weapon.ProjectileSpeed, 1f);
                hitbox.GlobalPosition = player.GlobalPosition + dir * 60f;
                hitbox.Scale = Vector2.One * (weapon.AttackArea / 25f);
                hitbox.Rotation = angle;
            }
            else if (hitbox is MeleeHitbox melee)
            {
                melee.Offset = dir * weapon.AttackRange * 0.7f;
                hitbox.GlobalPosition = player.GlobalPosition + melee.Offset;
                hitbox.Rotation = angle;
            }
            else
            {
                hitbox.GlobalPosition = player.GlobalPosition;
                hitbox.Rotation = angle;
            }

            player.GetParent().AddChild(hitbox);

            GD.Print($"[Attack] Hitbox '{hitbox.GetType().Name}' criado - danos={damages.Count} dir={dir}");

            instance.TriggerCooldown();

            if (!charges.InfiniteCharges)
            {
                instance.ConsumeCharge();
            }
        }
    }
}