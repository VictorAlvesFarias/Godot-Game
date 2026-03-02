using Godot;
using System.Collections.Generic;
using System.Linq;
using Jogo25D.Properties;
using Jogo25D.Hitboxes;
using Jogo25D.Characters;

namespace Jogo25D.Items
{
    public class WeaponDefinition : ItemDefinition
    {
        public override void Use(Player player, ItemInstance instance)
        {
            if (instance is not ItemRechargeableInstance rechargeable)
            {
                GD.Print("[Attack] Bloqueado - instância não é recarregável");
                
                return;
            }

            if (!rechargeable.CanAttack())
            {
                GD.Print($"[Attack] Bloqueado - cooldown={rechargeable.CooldownRemaining:F2} reloading={rechargeable.IsReloading} charges={rechargeable.CurrentCharges}");
                
                return;
            }

            var damageProps = instance.Properties.OfType<DamageProperty>().ToList();

            if (damageProps.Count == 0 || HitboxScene == null)
            {
                GD.Print($"[Attack] Bloqueado - damageProps={damageProps.Count} HitboxScene={HitboxScene != null}");
                
                return;
            }

            var weapon  = instance.Properties.OfType<WeaponProperty>().DefaultIfEmpty(new WeaponProperty()).First();
            var charges = instance.Properties.OfType<ChargesProperty>().DefaultIfEmpty(new ChargesProperty()).First();
            var crit    = instance.Properties.OfType<CritProperty>().DefaultIfEmpty(new CritProperty()).First();

            var damages = damageProps.ConvertAll(d => new DamageInfo
            {
                Amount       = d.DamageAmount,
                Type         = d.DamageType,
                SourcePeerId = (int)player.PeerId,
                CritChance   = crit.CritChance,
                CritDamage   = crit.CritDamage
            });

            if (HitboxScene.Instantiate<Area2D>() is not BaseHitbox hitbox)
            {
                GD.Print($"[Attack] Falha ao instanciar hitbox da cena '{HitboxScene.ResourcePath}'");
                
                return;
            }

            hitbox.Initialize(damages, instance.OnHitEffects, player);

            var rawDir = player.MousePosition - player.GlobalPosition;
            var dir = rawDir.LengthSquared() > 0.001f ? rawDir.Normalized() : Vector2.Right;
            var angle = dir.Angle();

            if (hitbox is ProjectileHitbox proj)
            {
                proj.Direction = dir;
                proj.Speed     = weapon.ProjectileSpeed;
                proj.Lifetime  = weapon.AttackRange / Mathf.Max(weapon.ProjectileSpeed, 1f);
                hitbox.GlobalPosition = player.GlobalPosition + dir * 60f;
                hitbox.Scale          = Vector2.One * (weapon.AttackArea / 25f);
                hitbox.Rotation       = angle;
            }
            else if (hitbox is MeleeHitbox melee)
            {
                melee.Offset          = dir * weapon.AttackRange * 0.7f;
                hitbox.GlobalPosition = player.GlobalPosition + melee.Offset;
                hitbox.Rotation       = angle;
            }
            else
            {
                hitbox.GlobalPosition = player.GlobalPosition;
                hitbox.Rotation       = angle;
            }

            player.GetParent().AddChild(hitbox);

            GD.Print($"[Attack] Hitbox '{hitbox.GetType().Name}' criado - danos={damages.Count} dir={dir}");

            rechargeable.TriggerCooldown();

            if (!charges.InfiniteCharges)
            {
                rechargeable.ConsumeCharge();
            }
        }
    }
}
