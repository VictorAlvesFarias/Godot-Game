using Godot;
using Jogo25D.Characters;

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

        public Polygon2D _ellipse;

        public override void _Ready()
        {
            TopLevel = true;

            _ellipse = new Polygon2D();
            _ellipse.Color = new Color(0.4f, 0.8f, 1f, 0.55f);
            _ellipse.ZIndex = 5;
            _ellipse.Polygon = BuildEllipse(AreaRadiusX, AreaRadiusY, 32);
            _ellipse.Visible = false;
            AddChild(_ellipse);

            var crossH = new Line2D();
            crossH.Width = 2f;
            crossH.DefaultColor = new Color(0.9f, 1f, 1f, 0.9f);
            crossH.ZIndex = 6;
            crossH.AddPoint(new Vector2(-10f, 0f));
            crossH.AddPoint(new Vector2( 10f, 0f));
            _ellipse.AddChild(crossH);

            var crossV = new Line2D();
            crossV.Width = 2f;
            crossV.DefaultColor = new Color(0.9f, 1f, 1f, 0.9f);
            crossV.ZIndex = 6;
            crossV.AddPoint(new Vector2(0f, -10f));
            crossV.AddPoint(new Vector2(0f,  10f));
            _ellipse.AddChild(crossV);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_ellipse == null)
            {
                return;
            }

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

            var from = new Vector2(targetX, player.GlobalPosition.Y - 10f);
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
            _ellipse.Visible = found;

            if (found)
            {
                _ellipse.GlobalPosition = pos;
            }
        }

        public static Vector2[] BuildEllipse(float rx, float ry, int segments)
        {
            var pts = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.Tau * i / segments;
                pts[i] = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
            }
            return pts;
        }
    }
}