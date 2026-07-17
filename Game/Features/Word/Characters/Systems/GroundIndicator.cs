using Godot;
using Jogo25D.Characters;
using Jogo25D.Effects;
using Jogo25D.Hitboxes;
using Jogo25D.Items;
using Jogo25D.Properties;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Systems
{
    public partial class GroundIndicator : Node2D
    {
        public bool IsGroundFound { get; set; }
        public Vector2 GroundPosition { get; set; }
        public bool IsActive { get; set; } = false;

        public float AreaRadiusX { get; set; } = 60f;
        public float AreaRadiusY { get; set; } = 18f;
        public float HorizontalRange { get; set; } = 300f;
        public float MaxVerticalDrop { get; set; } = 350f;
        public uint RayMask { get; set; } = 1;

        public bool UseRectangle { get; set; } = false;

        public Polygon2D _indicator;

        public override void _Ready()
        {
            TopLevel = true;

            _indicator = new Polygon2D();
            _indicator.Color = new Color(0.4f, 0.8f, 1f, 0.55f);
            _indicator.ZIndex = 5;
            _indicator.Polygon = BuildRectangle(AreaRadiusX, AreaRadiusY) ;
            _indicator.Visible = false;
            AddChild(_indicator);

            var crossH = new Line2D();
            crossH.Width = 2f;
            crossH.DefaultColor = new Color(0.9f, 1f, 1f, 0.9f);
            crossH.ZIndex = 6;
            crossH.AddPoint(new Vector2(-10f, 0f));
            crossH.AddPoint(new Vector2( 10f, 0f));
            _indicator.AddChild(crossH);

            var crossV = new Line2D();
            crossV.Width = 2f;
            crossV.DefaultColor = new Color(0.9f, 1f, 1f, 0.9f);
            crossV.ZIndex = 6;
            crossV.AddPoint(new Vector2(0f, -10f));
            crossV.AddPoint(new Vector2(0f,  10f));
            _indicator.AddChild(crossV);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_indicator == null) return;

            var player = GetParent()?.GetParent() as Player;
            if (player == null || !IsActive)
            {
                SetFound(false, Vector2.Zero);
                return;
            }

            var mouse = player.Input.MousePosition;
            float targetX = mouse.X;

            if (HorizontalRange > 0f)
            {
                float offset = mouse.X - player.GlobalPosition.X;
                targetX = player.GlobalPosition.X + Mathf.Clamp(offset, -HorizontalRange, HorizontalRange);
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

                if (MaxVerticalDrop > 0f && drop > MaxVerticalDrop)
                {
                    SetFound(false, Vector2.Zero);
                }
                else
                {
                    SetFound(true, pos);
                }
            }
            else
            {
                SetFound(false, Vector2.Zero);
            }
        }

        public new void Hide()
        {
            IsActive = false;
            SetFound(false, Vector2.Zero);
        }

        public void SetFound(bool found, Vector2 pos)
        {
            IsGroundFound = found;
            GroundPosition = pos;
            _indicator.Visible = found;

            if (found)
            {
                _indicator.GlobalPosition = pos - new Vector2(0, AreaRadiusY);
            }
        }

        public Vector2 GetVisualPosition()
        {
            return GroundPosition - new Vector2(0, AreaRadiusY);
        }

        public void UpdateIndicatorShape()
        {
            if (_indicator == null) return;

            _indicator.Polygon = BuildRectangle(AreaRadiusX, AreaRadiusY);
        }

        public static Vector2[] BuildRectangle(float width, float height)
        {
            return new Vector2[]
            {
                new Vector2(-width, -height),
                new Vector2(width, -height),
                new Vector2(width, height),
                new Vector2(-width, height)
            };
        }
    }
}