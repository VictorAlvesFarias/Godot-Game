using Godot;
using Jogo25D.Characters;
using Jogo25D.Effects;
using Jogo25D.Features.World.Resolver.Singletons;
using Jogo25D.Hitboxes;
using Jogo25D.Items;
using Jogo25D.Properties;
using Jogo25D.Utils.Extensions;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Actions
{
    public class FireballDefinition : ActionDefinition
    {
        #region Core - Abstract

        public override bool OnStartActionValidation(Player player, ActionDefinitionData instance, float delta)
        {
            return player.Input.Ability && instance.CanUse;
        }

        public override void OnStartAction(Player player, ActionDefinitionData instance, float delta)
        {
            if (HitboxScene == null)
            {
                return;
            }

            var damageProps = Properties.OfType<DamagePropertyData>().ToList();

            if (damageProps.Count == 0 || HitboxScene == null)
            {
                GD.Print($"[Attack] Bloqueado - damageProps={damageProps.Count} HitboxScene={HitboxScene != null}");

                return;
            }

            var direction = (player.Input.MousePosition - player.GlobalPosition).Normalized();
            var hitbox = HitboxScene.Instantiate<ProjectileHitbox>();
            var weapon = Resolver.Resolve(Properties.OfType<AttackPropertyData>().ToList());
            var crit = Resolver.Resolve(Properties.OfType<CritPropertyData>().ToList());
            var resolvedDamages = Resolver.Resolve(damageProps);
            var damages = resolvedDamages.ConvertAll(d => new DamageInfo
            {
                Amount = (int)(d.DamageAmount * d.DamageMultiplier),
                Type = d.DamageType,
                SourcePeerId = (int)player.PeerId,
                CritChance = crit.CritChance,
                CritDamage = crit.CritDamage
            }).ToGodotArray();

            hitbox.Initialize(damages, CreateEffects(EffectTriggerType.OnHit), player, weapon.KnockbackForce);

            hitbox.Direction = direction;
            hitbox.Speed = weapon.ProjectileSpeed;
            hitbox.Lifetime = weapon.AttackRange / weapon.ProjectileSpeed;
            hitbox.GlobalPosition = player.GlobalPosition + direction * 60f;
            hitbox.Scale = Vector2.One * (weapon.AttackArea / 25f);

            player.GetParent().AddChild(hitbox);
        }

        #endregion
    }
}