using Godot;
using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items.Indicators
{
    public class MiningIndicator : IItemIndicator
    {
        private const float AimingAlpha = 0.15f;
        private const float MiningAlpha = 0.45f;

        private readonly ToolDefinition _tool;
        private Polygon2D _indicator;

        public MiningIndicator(ToolDefinition tool)
        {
            _tool = tool;
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

            var (found, cell) = player.ResolveMiningTargetCell(layer, _tool.Reach);

            if (!found)
            {
                Hide(player);

                return;
            }

            EnsureIndicator(layer);

            _indicator.Color = new Color(1f, 1f, 1f, player.IsMining ? MiningAlpha : AimingAlpha);
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
                Polygon = TileQuad.Build(layer),
            };

            layer.AddChild(_indicator);
        }
    }
}
