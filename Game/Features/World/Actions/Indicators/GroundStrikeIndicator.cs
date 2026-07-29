using Godot;
using Jogo25D.Characters;
using Jogo25D.Features.World.Resolver.Singletons;
using Jogo25D.Hitboxes;
using Jogo25D.Properties;
using System.Linq;

namespace Jogo25D.Actions.Indicators
{
    public class GroundStrikeIndicator : IActionIndicator
    {
        private const uint RayMask = 1;

        private float? _halfWidthPx;
        private float? _halfHeightPx;
        private Polygon2D _indicator;

        public void Update(Player player, ActionDefinition definition, ActionDefinitionData instance, float delta)
        {
            EnsureIndicator(player);

            if (!player.Input.Ability2Held || !instance.CanUse)
            {
                _indicator.Visible = false;

                return;
            }

            EnsureTextureSize(definition);

            var weapon = Resolver.Resolve(
                definition.Properties.OfType<AttackPropertyData>().ToList(),
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

        public void Destroy()
        {
            if (_indicator != null && GodotObject.IsInstanceValid(_indicator))
            {
                _indicator.QueueFree();
            }

            _indicator = null;
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

        private void EnsureTextureSize(ActionDefinition definition)
        {
            if (_halfWidthPx.HasValue || definition.HitboxScene == null)
            {
                return;
            }

            var preview = definition.HitboxScene.Instantiate<GroundHitbox>();
            var sprite = preview.GetNode<AnimatedSprite2D>("Sprite");
            var frames = sprite.SpriteFrames;
            var texture = frames.GetFrameTexture(sprite.Animation, sprite.Frame);

            _halfWidthPx = texture.GetWidth() * 0.5f;
            _halfHeightPx = texture.GetHeight() * 0.5f;

            preview.QueueFree();
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

        private static Vector2? CalculateGroundPosition(Player player, float horizontalRange, float maxVerticalDrop)
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

            if (hit == null || hit.Count == 0)
            {
                return null;
            }

            var pos = hit["position"].AsVector2();
            var drop = pos.Y - player.GlobalPosition.Y;

            if (maxVerticalDrop > 0f && drop > maxVerticalDrop)
            {
                return null;
            }

            return pos;
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
    }
}
