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
    public class GroundStrikeDefinition : ActionDefinition
    {
        #region Properties

        private const uint RayMask = 1;

        private float _halfHeight;
        private float _halfWidth;

        #endregion

        #region Core - Virtuals

        public override void OnCreate(Player player, ActionDefinitionData instance)
        {
            if (HitboxScene == null)
            {
                return;
            }

            var weapon = Resolver.Resolve(Properties.OfType<AttackPropertyData>().ToList(), player.Data.Properties.OfType<AttackPropertyData>().ToList(), player.Properties.OfType<AttackPropertyData>().ToList());
            var preview = HitboxScene.Instantiate<GroundHitbox>();
            var scale = weapon.AttackArea / 25f;

            preview.Scale = Vector2.One * scale;

            var sprite = preview.GetNode<AnimatedSprite2D>("Sprite");
            var frames = sprite.SpriteFrames;
            var texture = frames.GetFrameTexture(sprite.Animation, sprite.Frame);

            _halfHeight = texture.GetHeight() * scale * 0.5f;
            _halfWidth = texture.GetWidth() * scale * 0.5f;

            preview.QueueFree();
        }

        #endregion

        #region Core - Abstract

        public override bool OnStartActionValidation(Player player, ActionDefinitionData instance, float delta)
        {
            return player.Input.Ability2JustReleased && instance.CanUse;
        }

        public override void OnStartAction(Player player, ActionDefinitionData instance, float delta)
        {
            if (HitboxScene == null)
            {
                return;
            }

            var weapon = Resolver.Resolve(Properties.OfType<AttackPropertyData>().ToList(), player.Data.Properties.OfType<AttackPropertyData>().ToList(), player.Properties.OfType<AttackPropertyData>().ToList());
            var horizontalRange = weapon.AttackRange;
            var maxVerticalDrop = weapon.AttackRange;
            var ground = CalculateGroundPosition(player, horizontalRange, maxVerticalDrop);

            if (ground == null)
            {
                return;
            }

            var damageProps = Properties.OfType<DamagePropertyData>().ToList();

            if (damageProps.Count == 0)
            {
                return;
            }

            var crit = Resolver.Resolve(Properties.OfType<CritPropertyData>().ToList(), player.Data.Properties.OfType<CritPropertyData>().ToList(), player.Properties.OfType<CritPropertyData>().ToList());
            var resolvedDamages = Resolver.Resolve(damageProps, player.Data.Properties.OfType<DamagePropertyData>().ToList(), player.Properties.OfType<DamagePropertyData>().ToList());
            var damages = resolvedDamages.ConvertAll(d => new DamageInfo
            {
                Amount = (int)(d.DamageAmount * d.DamageMultiplier),
                Type = d.DamageType,
                SourcePeerId = (int)player.PeerId,
                CritChance = crit.CritChance,
                CritDamage = crit.CritDamage
            }).ToGodotArray();

            var hitbox = HitboxScene.Instantiate<GroundHitbox>();

            hitbox.Initialize(damages, CreateEffects(EffectTriggerType.OnHit), player, weapon.KnockbackForce);

            var scale = weapon.AttackArea / 25f;

            hitbox.Scale = Vector2.One * scale;

            player.GetParent().AddChild(hitbox);

            hitbox.GlobalPosition = ground.Value - new Vector2(0, _halfHeight);
        }

        #endregion

        #region Core - Utils

        private Vector2? CalculateGroundPosition(Player player, float horizontalRange, float maxVerticalDrop)
        {
            var mouse = player.Input.MousePosition;
            var targetX = mouse.X;

            if (horizontalRange > 0f)
            {
                var offset = mouse.X - player.GlobalPosition.X;

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
                var drop = pos.Y - player.GlobalPosition.Y;

                if (maxVerticalDrop > 0f && drop > maxVerticalDrop)
                {
                    return null;
                }

                return pos;
            }

            return null;
        }

        #endregion

        #region Core - Virtuals

        public override void OnUpdateWhileActive(Player player, ActionDefinitionData instance, float delta)
        {
        }

        public override void OnFinishedAction(Player player, ActionDefinitionData instance, float delta)
        {
        }

        public override void OnEnableAction(Player player, ActionDefinitionData instance, float delta)
        {
        }

        #endregion

        #region Core - Indicator

        private float? _halfWidthPx;
        private float? _halfHeightPx;
        private Polygon2D _indicator;

        public override void UpdateIndicator(Player player, ActionDefinitionData data, float delta)
        {
            EnsureIndicator(player);

            if (!player.Input.Ability2Held || !data.CanUse)
            {
                _indicator.Visible = false;

                return;
            }

            EnsureTextureSize();

            var weapon = Resolver.Resolve(
                Properties.OfType<AttackPropertyData>().ToList(),
                player.Data.Properties.OfType<AttackPropertyData>().ToList(),
                player.Properties.OfType<AttackPropertyData>().ToList());

            var scale = weapon.AttackArea / 25f;
            var halfWidth = (_halfWidthPx ?? 0f) * scale;
            var halfHeight = (_halfHeightPx ?? 0f) * scale;

            var ground = CalculateGroundPosition(player, weapon.AttackRange, weapon.AttackRange);

            if (ground == null)
            {
                _indicator.Visible = false;

                return;
            }

            _indicator.Polygon = BuildRectangle(halfWidth, halfHeight);
            _indicator.GlobalPosition = ground.Value - new Vector2(0, halfHeight);
            _indicator.Visible = true;
        }

        public override void DestroyIndicator()
        {
            if (_indicator != null && GodotObject.IsInstanceValid(_indicator))
            {
                _indicator.QueueFree();
            }

            _indicator = null;
        }

        private void EnsureTextureSize()
        {
            if (_halfWidthPx.HasValue || HitboxScene == null)
            {
                return;
            }

            var preview = HitboxScene.Instantiate<GroundHitbox>();
            var sprite = preview.GetNode<AnimatedSprite2D>("Sprite");
            var frames = sprite.SpriteFrames;
            var texture = frames.GetFrameTexture(sprite.Animation, sprite.Frame);

            _halfWidthPx = texture.GetWidth() * 0.5f;
            _halfHeightPx = texture.GetHeight() * 0.5f;

            preview.QueueFree();
        }

        private void EnsureIndicator(Player player)
        {
            if (_indicator != null && GodotObject.IsInstanceValid(_indicator))
            {
                return;
            }

            _indicator = new Polygon2D();

            BuildVisual(_indicator);

            player.AddChild(_indicator);
        }

        private static void BuildVisual(Polygon2D indicator)
        {
            indicator.Color = new Color(0.4f, 0.8f, 1f, 0.55f);
            indicator.ZIndex = 5;
            indicator.Visible = false;

            var crossH = new Line2D();

            crossH.Width = 2f;
            crossH.DefaultColor = new Color(0.9f, 1f, 1f, 0.9f);
            crossH.ZIndex = 6;
            crossH.AddPoint(new Vector2(-10f, 0f));
            crossH.AddPoint(new Vector2(10f, 0f));
            indicator.AddChild(crossH);

            var crossV = new Line2D();

            crossV.Width = 2f;
            crossV.DefaultColor = new Color(0.9f, 1f, 1f, 0.9f);
            crossV.ZIndex = 6;
            crossV.AddPoint(new Vector2(0f, -10f));
            crossV.AddPoint(new Vector2(0f, 10f));
            indicator.AddChild(crossV);
        }

        private static Vector2[] BuildRectangle(float width, float height)
        {
            return new Vector2[]
            {
                new Vector2(-width, -height),
                new Vector2(width, -height),
                new Vector2(width, height),
                new Vector2(-width, height),
            };
        }

        #endregion
    }
}
