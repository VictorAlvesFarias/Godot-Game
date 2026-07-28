using Godot;
using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items.Indicators
{
    public class PlacementIndicator : IItemIndicator
    {
        private static readonly Color FillColor = new Color(0.3f, 1f, 0.4f, 0.3f);

        public void Update(Player player, ItemDefinition definition, ItemDefinitionData data, float delta)
        {
            if (definition is not BlockItemDefinition blockItem)
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

            // Colocar e sempre livre (so limitado pelo alcance) - sem
            // checar RestrictMiningToAccessible, que so vale pra quebrar.
            var cell = player.ResolveCellInRange(layer, blockItem.Reach);

            if (layer.GetCellSourceId(cell) != -1)
            {
                Hide(player);

                return;
            }

            var indicator = player.GetOrCreateIndicator<Polygon2D>(nameof(PlacementIndicator), p =>
            {
                p.ZIndex = 10;
                p.Color = FillColor;
                p.Polygon = TileQuad.Build(layer);
            }, layer);

            indicator.Position = layer.MapToLocal(cell);
            indicator.Visible = true;
        }

        public void Hide(Player player)
        {
            var indicator = player.GetIndicatorOrNull<Polygon2D>(nameof(PlacementIndicator));

            if (indicator != null)
            {
                indicator.Visible = false;
            }
        }
    }
}
