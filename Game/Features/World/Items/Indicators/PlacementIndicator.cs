using Godot;
using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items.Indicators
{
    public class PlacementIndicator : IItemIndicator
    {
        private static readonly Color FillColor = new Color(0.3f, 1f, 0.4f, 0.3f);

        private readonly BlockItemDefinition _blockItem;
        private Polygon2D _indicator;

        public PlacementIndicator(BlockItemDefinition blockItem)
        {
            _blockItem = blockItem;
        }

        public void Update(Player player, ItemDefinitionData data, float delta)
        {
            if (!player.IsOwner())
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

            var cell = player.ResolveCellInRange(layer, _blockItem.Reach);

            if (layer.GetCellSourceId(cell) != -1)
            {
                Hide(player);

                return;
            }

            EnsureIndicator(layer);

            _indicator.Position = layer.MapToLocal(cell);
            _indicator.Visible = true;
        }

        public void Hide(Player player)
        {
            if (_indicator != null && GodotObject.IsInstanceValid(_indicator))
            {
                _indicator.Visible = false;
            }
        }

        public void Destroy()
        {
            if (_indicator != null && GodotObject.IsInstanceValid(_indicator))
            {
                _indicator.QueueFree();
            }

            _indicator = null;
        }

        private void EnsureIndicator(TileMapLayer layer)
        {
            if (_indicator != null && GodotObject.IsInstanceValid(_indicator) && _indicator.GetParent() == layer)
            {
                return;
            }

            if (_indicator != null && GodotObject.IsInstanceValid(_indicator))
            {
                _indicator.QueueFree();
            }

            _indicator = new Polygon2D
            {
                ZIndex = 10,
                Color = FillColor,
                Polygon = TileQuad.Build(layer),
            };

            layer.AddChild(_indicator);
        }
    }
}
