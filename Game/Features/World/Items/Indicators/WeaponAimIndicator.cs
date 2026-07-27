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

        public void Update(Player player, ItemDefinition definition, ItemDefinitionData data, float delta)
        {
            var line = player.GetOrCreateIndicator<Line2D>(nameof(WeaponAimIndicator), l =>
            {
                l.Width = Width;
                l.DefaultColor = LineColor;
                l.ZIndex = 10;
            });

            var dir = player.Input.MousePosition - player.GlobalPosition;

            if (dir.LengthSquared() < 0.01f)
            {
                line.Visible = false;

                return;
            }

            var d = dir.Normalized();

            line.ClearPoints();
            line.AddPoint(d * Offset);
            line.AddPoint(d * (Offset + Length));
            line.Visible = true;
        }

        public void Hide(Player player)
        {
            player.GetOrCreateIndicator<Line2D>(nameof(WeaponAimIndicator)).Visible = false;
        }
    }
}
