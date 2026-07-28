using Godot;
using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items.Indicators
{
    public class MiningIndicator : IItemIndicator
    {
        private const float AimingAlpha = 0.15f;
        private const float MiningAlpha = 0.45f;

        public void Update(Player player, ItemDefinition definition, ItemDefinitionData data, float delta)
        {
            if (definition is not ToolDefinition tool)
            {
                Hide(player);

                return;
            }

            var layer = player.GetActiveTileLayer();

            if (layer == null)
            {
                Hide(player);

                return;
            }

            var (found, cell) = player.ResolveMiningTargetCell(layer, tool.Reach);

            if (!found)
            {
                Hide(player);

                return;
            }

            var indicator = player.GetOrCreateIndicator<Polygon2D>(nameof(MiningIndicator), p =>
            {
                p.ZIndex = 10;
                p.Polygon = TileQuad.Build(layer);
            }, layer);

            indicator.Color = new Color(1f, 1f, 1f, player.IsMining ? MiningAlpha : AimingAlpha);
            indicator.Position = layer.MapToLocal(cell);
            indicator.Visible = true;
        }

        public void Hide(Player player)
        {
            var indicator = player.GetIndicatorOrNull<Polygon2D>(nameof(MiningIndicator));

            if (indicator != null)
            {
                indicator.Visible = false;
            }
        }
    }
}
