using Godot;
using Jogo25D.Characters;
using Jogo25D.Effects;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Hitboxes;
using Jogo25D.Properties;
using Jogo25D.Systems;
using Jogo25D.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;

namespace Jogo25D.Items
{
    public class WeaponDefinition : ItemDefinition
    {
        #region Core - Virtuals

        public override void OnEquip(Player player, ItemDefinitionData data)
        {
            if (player.AimIndicator != null)
            {
                player.AimIndicator.IsActive = true;
            }
        }

        public override void OnUnequip(Player player, ItemDefinitionData data)
        {
            player.AimIndicator?.Hide();
        }

        public override void Use(Player player, ItemDefinitionData instance)
        {
            if (!CanUse(instance))
            {
                GD.Print($"[Attack] Bloqueado - cooldown={instance.CooldownRemainingTimer:F2} reloading={IsReloading(instance)} charges={instance.CurrentCharges}");
                
                return;
            }

            var damageProps = instance.Properties.OfType<DamagePropertyData>().ToList();

            if (damageProps.Count == 0 || HitboxScene == null)
            {
                GD.Print($"[Attack] Bloqueado - damageProps={damageProps.Count} HitboxScene={HitboxScene != null}");
                
                return;
            }

            var weapon = instance.Properties.OfType<AttackPropertyData>().DefaultIfEmpty(new AttackPropertyData()).First();
            var charges = instance.Properties.OfType<ChargesPropertyData>().DefaultIfEmpty(new ChargesPropertyData()).First();
            var crit = instance.Properties.OfType<CritPropertyData>().DefaultIfEmpty(new CritPropertyData()).First();
            var damages = damageProps.ConvertAll(d => new DamageInfo
            {
                Amount = d.DamageAmount,
                Type = d.DamageType,
                SourcePeerId = (int)player.PeerId,
                CritChance = crit.CritChance,
                CritDamage = crit.CritDamage
            }).ToGodotArray();

            if (HitboxScene.Instantiate<Area2D>() is not BaseHitbox hitbox)
            {
                GD.Print($"[Attack] Falha ao instanciar hitbox da cena '{HitboxScene.ResourcePath}'");
                
                return;
            }

            hitbox.Initialize(damages, instance.OnHitEffects, player);

            var rawDir = player.Input.MousePosition - player.GlobalPosition;
            var dir = rawDir.LengthSquared() > 0.001f ? rawDir.Normalized() : Vector2.Right;
            var angle = dir.Angle();

            player.Sprite.FlipH = !(angle >= -1.5f && angle <= 1.5f);

            player.Sprite.Play("melee");

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
                hitbox.DestroyInAllBodies = false;
            }
            else
            {
                hitbox.GlobalPosition = player.GlobalPosition;
                hitbox.Rotation = angle;
            }

            player.GetParent().AddChild(hitbox);

            GD.Print($"[Attack] Hitbox '{hitbox.GetType().Name}' criado - danos={damages.Count} dir={dir}");

            TriggerCooldownTimer(instance);

            if (!charges.InfiniteCharges && player.IsOwner())
            {
                player.ConsumeChargeRequest(instance.InstanceId);
            }
        }

        #endregion
    }
}