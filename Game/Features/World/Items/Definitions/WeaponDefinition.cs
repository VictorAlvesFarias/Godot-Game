using Godot;
using Jogo25D.Characters;
using Jogo25D.Effects;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Features.World.Resolver.Singletons;
using Jogo25D.Hitboxes;
using Jogo25D.Properties;
using Jogo25D.Systems;
using Jogo25D.Utils.Extensions;
using System.Linq;

namespace Jogo25D.Items
{
    public class WeaponDefinition : ItemDefinition
    {
        #region Node children references

        public Line2D AimLine { get; set; }

        #endregion

        #region Core - Virtuals

        public override void Use(Player player, ItemData instance)
        {
            if (!CanUse(instance))
            {
                GD.Print($"[WeaponDefinition.Use] Bloqueado - cooldown={instance.CooldownRemainingTimer:F2} reloading={IsReloading(instance)} charges={instance.CurrentCharges}");

                return;
            }

            var resolvedDamages = Resolver.Resolve(
                Properties.OfType<DamagePropertyData>().ToList(),
                instance.Properties.OfType<DamagePropertyData>().ToList(),
                player.ActiveProperties.OfType<DamagePropertyData>().ToList(),
                player.ActiveProperties.OfType<DamagePropertyData>().ToList()
            );
            var weapon = Resolver.Resolve(
                Properties.OfType<AttackPropertyData>().ToList(),
                instance.Properties.OfType<AttackPropertyData>().ToList(),
                player.ActiveProperties.OfType<AttackPropertyData>().ToList(),
                player.ActiveProperties.OfType<AttackPropertyData>().ToList()
            );
            var charges = Resolver.Resolve(
                Properties.OfType<ChargesPropertyData>().ToList(),
                instance.Properties.OfType<ChargesPropertyData>().ToList()
            ).FirstOrDefault();
            var crit = Resolver.Resolve(
                Properties.OfType<CritPropertyData>().ToList(),
                instance.Properties.OfType<CritPropertyData>().ToList(),
                player.ActiveProperties.OfType<CritPropertyData>().ToList(),
                player.ActiveProperties.OfType<CritPropertyData>().ToList()
            );
            var damages = resolvedDamages.ConvertAll(d => new DamageInfo
            {
                Amount = (int)(d.DamageAmount * d.DamageMultiplier),
                Type = d.DamageType,
                SourcePeerId = (int)player.PeerId,
                CritChance = crit.CritChance,
                CritDamage = crit.CritDamage
            }).ToGodotArray();

            if (HitboxScene.Instantiate<Area2D>() is not BaseHitbox hitbox)
            {
                GD.Print($"[WeaponDefinition.Use] Falha ao instanciar hitbox da cena '{HitboxScene.ResourcePath}'");

                return;
            }

            var hitEffects = new Godot.Collections.Array<EffectDefinitionData>(instance.Effects.Where(e => e.Type == EffectTriggerType.OnHit));

            hitbox.Initialize(damages, hitEffects, player, weapon.KnockbackForce);

            var rawDir = player.Input.MousePosition - player.GlobalPosition;
            var dir = rawDir.LengthSquared() > 0.001f ? rawDir.Normalized() : Vector2.Right;
            var angle = dir.Angle();

            player.SetFacing(!(angle >= -1.5f && angle <= 1.5f));

            player.Sprite.Play("melee");

            if (hitbox is ProjectileHitbox proj)
            {
                proj.Direction = dir;
                proj.Speed = weapon.ProjectileSpeed;
                proj.Lifetime = weapon.AttackRange / Mathf.Max(weapon.ProjectileSpeed, 1f);
                hitbox.GlobalPosition = player.GlobalPosition + dir * 60f;
                hitbox.Scale = Vector2.One * (weapon.AttackArea / 25f);
                hitbox.DirectionAngle = angle;
                hitbox.StopDamageOnMaxPerfuracao = true;
            }
            else if (hitbox is MeleeHitbox melee)
            {
                melee.Offset = dir * weapon.AttackRange * 0.7f;
                hitbox.GlobalPosition = player.GlobalPosition + melee.Offset;
                hitbox.DirectionAngle = angle;
                hitbox.DestroyInAllBodies = false;
            }
            else
            {
                hitbox.GlobalPosition = player.GlobalPosition;
                hitbox.DirectionAngle = angle;
                hitbox.DestroyInAllBodies = false;
            }

            player.GetParent().AddChild(hitbox);

            GD.Print($"[WeaponDefinition.Use] Hitbox '{hitbox.GetType().Name}' criado - danos={damages.Count} dir={dir}");

            TriggerCooldownTimer(instance);

            if (!charges.InfiniteCharges && player.IsOwner())
            {
                player.ConsumeChargeRequest(instance.InstanceId);
            }
        }

        #endregion

        #region Core - Indicator

        public override void UpdateIndicator(Player player, ItemData data, float delta)
        {
            EnsureAimLine(player);

            var dir = player.Input.MousePosition - player.GlobalPosition;

            if (dir.LengthSquared() < 0.01f)
            {
                AimLine.Visible = false;

                return;
            }

            var d = dir.Normalized();

            AimLine.ClearPoints();
            AimLine.AddPoint(d * 40.0f);
            AimLine.AddPoint(d * (40.0f + 25.0f));
            AimLine.Visible = true;
        }

        public override void HideIndicator(Player player)
        {
            if (AimLine != null && GodotObject.IsInstanceValid(AimLine))
            {
                AimLine.Visible = false;
            }
        }

        public override void DestroyIndicator()
        {
            if (AimLine != null && GodotObject.IsInstanceValid(AimLine))
            {
                AimLine.QueueFree();
            }

            AimLine = null;
        }

        private void EnsureAimLine(Player player)
        {
            if (AimLine != null && GodotObject.IsInstanceValid(AimLine))
            {
                return;
            }

            AimLine = new Line2D
            {
                Width = 3.0f,
                DefaultColor = new Color(1f, 1f, 1f, 0.7f),
                ZIndex = 10,
            };

            player.AddChild(AimLine);
        }

        #endregion
    }
}