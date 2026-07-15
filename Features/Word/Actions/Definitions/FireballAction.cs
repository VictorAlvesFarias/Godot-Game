using Godot;
using Jogo25D.Characters;
using Jogo25D.Effects;
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
            var weapon = Properties.OfType<AttackPropertyData>().DefaultIfEmpty(new AttackPropertyData()).First();
            var charges = Properties.OfType<ChargesPropertyData>().DefaultIfEmpty(new ChargesPropertyData()).First();
            var crit = Properties.OfType<CritPropertyData>().DefaultIfEmpty(new CritPropertyData()).First();
            var damages = damageProps.ConvertAll(d => new DamageInfo
            {
                Amount = d.DamageAmount,
                Type = d.DamageType,
                SourcePeerId = (int)player.PeerId,
                CritChance = crit.CritChance,
                CritDamage = crit.CritDamage
            }).ToGodotArray();

            hitbox.Initialize(damages, OnHitEffects, player);

            hitbox.Direction = direction;
            hitbox.Speed = weapon.ProjectileSpeed;
            hitbox.Lifetime = weapon.AttackRange / weapon.ProjectileSpeed;
            hitbox.GlobalPosition = player.GlobalPosition + direction * 60f;
            hitbox.Scale = Vector2.One * (weapon.AttackArea / 25f);

            player.GetParent().AddChild(hitbox);
        }
    }
}