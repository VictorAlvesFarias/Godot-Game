using Godot;
using Jogo25D.Characters;
using Jogo25D.Effects;
using Jogo25D.Hitboxes;
using Jogo25D.Items;
using Jogo25D.Properties;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Actions
{
    public class GroundStrikeDefinition : ActionDefinition
    {
        public override void OnCreate(Player player, ActionInstance instance)
        {
            if (player.GroundMarker == null)
            {
                return;
            }

            var weapon = Properties.OfType<AttackProperty>().DefaultIfEmpty(new AttackProperty()).First();

            player.GroundMarker.AreaRadiusX = weapon.AttackArea;
            player.GroundMarker.AreaRadiusY = weapon.AttackArea * 0.3f;
            player.GroundMarker.HorizontalRange = weapon.AttackRange;
            player.GroundMarker.MaxVerticalDrop = weapon.AttackRange;
            player.GroundMarker.Hide();
        }

        public override void OnPassiveUpdate(Player player, ActionInstance instance, float delta)
        {
            if (player.GroundMarker == null)
            {
                return;
            }
            player.GroundMarker.IsActive = player.Input.Ability2Held && instance.CanUse;
        }

        public override bool OnStartActionValidation(Player player, ActionInstance instance, float delta)
        {
            return player.Input.Ability2JustReleased
                && instance.CanUse
                && player.GroundMarker != null
                && player.GroundMarker.IsGroundFound;
        }

        public override void OnStartAction(Player player, ActionInstance instance, float delta)
        {
            if (player.GroundMarker == null)
            {
                return;
            }

            var spawnPos = player.GroundMarker.GroundPosition;

            player.GroundMarker.Hide();

            if (HitboxScene == null)
            {
                return;
            }

            var damageProps = Properties.OfType<DamageProperty>().ToList();

            if (damageProps.Count == 0)
            {
                return;
            }

            var crit = Properties.OfType<CritProperty>().DefaultIfEmpty(new CritProperty()).First();
            var damages = damageProps.ConvertAll(d => new DamageInfo
            {
                Amount = d.DamageAmount,
                Type = d.DamageType,
                SourcePeerId = (int)player.PeerId,
                CritChance = crit.CritChance,
                CritDamage = crit.CritDamage
            });

            var hitbox = HitboxScene.Instantiate<GroundHitbox>();

            hitbox.Initialize(damages, OnHitEffects, player);
            hitbox.GlobalPosition = spawnPos;

            player.GetParent().AddChild(hitbox);
        }

        public override void OnUpdateWhileActive(Player player, ActionInstance instance, float delta)
        {
        }

        public override void OnFinishedAction(Player player, ActionInstance instance, float delta)
        {
        }

        public override void OnEnableAction(Player player, ActionInstance instance, float delta)
        {
        }
    }
}