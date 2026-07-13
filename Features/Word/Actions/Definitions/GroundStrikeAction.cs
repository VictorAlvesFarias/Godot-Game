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
        private const uint RayMask = 1;

        private float _halfHeight;
        private float _halfWidth;

        public override void OnCreate(Player player, ActionInstance instance)
        {
            if (HitboxScene == null)
                return;

            var weapon = Properties.OfType<AttackProperty>().DefaultIfEmpty(new AttackProperty()).First();

            var preview = HitboxScene.Instantiate<GroundHitbox>();

            float scale = weapon.AttackArea / 25f;
            preview.Scale = Vector2.One * scale;

            var sprite = preview.GetNode<AnimatedSprite2D>("Sprite");
            var frames = sprite.SpriteFrames;
            var texture = frames.GetFrameTexture(sprite.Animation, sprite.Frame);

            _halfHeight = texture.GetHeight() * scale * 0.5f;
            _halfWidth = texture.GetWidth() * scale * 0.5f;

            if (player.GroundMarker != null)
            {
                player.GroundMarker.AreaRadiusX = _halfWidth;
                player.GroundMarker.AreaRadiusY = _halfHeight;
                player.GroundMarker.HorizontalRange = weapon.AttackRange;
                player.GroundMarker.MaxVerticalDrop = weapon.AttackRange;
                player.GroundMarker.UpdateIndicatorShape();
                player.GroundMarker.Hide();
            }

            preview.QueueFree();
        }

        public override void OnPassiveUpdate(Player player, ActionInstance instance, float delta)
        {
            if (player.GroundMarker != null)
                player.GroundMarker.IsActive = player.Input.Ability2Held && instance.CanUse;
        }

        public override bool OnStartActionValidation(Player player, ActionInstance instance, float delta)
        {
            return player.Input.Ability2JustReleased && instance.CanUse;
        }

        public override void OnStartAction(Player player, ActionInstance instance, float delta)
        {
            if (HitboxScene == null)
                return;

            var weapon = Properties.OfType<AttackProperty>().DefaultIfEmpty(new AttackProperty()).First();

            float horizontalRange = weapon.AttackRange;
            float maxVerticalDrop = weapon.AttackRange;

            Vector2? ground = CalculateGroundPosition(player, horizontalRange, maxVerticalDrop);

            if (ground == null)
                return;

            var damageProps = Properties.OfType<DamageProperty>().ToList();
            if (damageProps.Count == 0)
                return;

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

            float scale = weapon.AttackArea / 25f;
            hitbox.Scale = Vector2.One * scale;

            player.GetParent().AddChild(hitbox);

            hitbox.GlobalPosition = ground.Value - new Vector2(0, _halfHeight);
        }

        private Vector2? CalculateGroundPosition(Player player, float horizontalRange, float maxVerticalDrop)
        {
            var mouse = player.Input.MousePosition;
            float targetX = mouse.X;

            if (horizontalRange > 0f)
            {
                float offset = mouse.X - player.GlobalPosition.X;
                targetX = player.GlobalPosition.X + Mathf.Clamp(offset, -horizontalRange, horizontalRange);
            }

            var from = new Vector2(targetX, player.GlobalPosition.Y - 50f);
            var to = new Vector2(targetX, player.GlobalPosition.Y + 2000f);

            var query = PhysicsRayQueryParameters2D.Create(from, to, RayMask);
            query.Exclude = new Godot.Collections.Array<Rid> { player.GetRid() };

            var hit = player.GetWorld2D().DirectSpaceState.IntersectRay(query);

            if (hit != null && hit.Count > 0)
            {
                var pos = hit["position"].AsVector2();
                float drop = pos.Y - player.GlobalPosition.Y;

                if (maxVerticalDrop > 0f && drop > maxVerticalDrop)
                    return null;

                return pos;
            }

            return null;
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