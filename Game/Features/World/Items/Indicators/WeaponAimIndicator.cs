using Godot;
using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items.Indicators
{
    public class WeaponAimIndicator : IItemIndicator
    {
        private const float Length = 25.0f;
        private const float Width = 3.0f;
        private const float Offset = 40.0f;
        private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.7f);

        private Line2D _line;

        public void Update(Player player, ItemDefinitionData data, float delta)
        {
            EnsureLine(player);

            var dir = player.Input.MousePosition - player.GlobalPosition;

            if (dir.LengthSquared() < 0.01f)
            {
                _line.Visible = false;

                return;
            }

            var d = dir.Normalized();

            _line.ClearPoints();
            _line.AddPoint(d * Offset);
            _line.AddPoint(d * (Offset + Length));
            _line.Visible = true;
        }

        public void Hide(Player player)
        {
            if (_line != null && GodotObject.IsInstanceValid(_line))
            {
                _line.Visible = false;
            }
        }

        public void Destroy()
        {
            if (_line != null && GodotObject.IsInstanceValid(_line))
            {
                _line.QueueFree();
            }

            _line = null;
        }

        private void EnsureLine(Player player)
        {
            if (_line != null && GodotObject.IsInstanceValid(_line))
            {
                return;
            }

            _line = new Line2D
            {
                Width = Width,
                DefaultColor = LineColor,
                ZIndex = 10,
            };

            player.AddChild(_line);
        }
    }
}
