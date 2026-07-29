using Godot;
using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Items.Indicators;

namespace Jogo25D.Items
{
    public class BlockItemDefinition : ItemDefinition
    {
        private static readonly Color FillColor = new Color(0.3f, 1f, 0.4f, 0.3f);

        public string BlockId { get; init; }
        public float Reach { get; init; } = 120f;

        private Polygon2D _indicator;

        public override void Use(Player player, ItemData instance)
        {
            if (instance == null || instance.Quantity <= 0 || !CanUse(instance))
            {
                return;
            }

            if (!player.IsOwner())
            {
                return;
            }

            var layer = player.GetActiveTileLayer();

            if (layer == null)
            {
                return;
            }

            var targetCell = ResolveCellInRange(player, layer, Reach);

            TriggerCooldownTimer(instance);

            player.PlaceBlockRequest(targetCell, instance.InstanceId);
        }

        private static Vector2I ResolveCellInRange(Player player, TileMapLayer layer, float reach)
        {
            var targetWorldPos = player.Input.MousePosition;
            var toTarget = targetWorldPos - player.GlobalPosition;

            if (toTarget.Length() > reach)
            {
                targetWorldPos = player.GlobalPosition + toTarget.Normalized() * reach;
            }

            return layer.LocalToMap(layer.ToLocal(targetWorldPos));
        }

        public override void UpdateIndicator(Player player, ItemData data, float delta)
        {
            if (!player.IsOwner())
            {
                HideIndicator(player);

                return;
            }

            var layer = player.GetActiveTileLayer();

            if (layer == null)
            {
                HideIndicator(player);

                return;
            }

            var cell = ResolveCellInRange(player, layer, Reach);

            if (layer.GetCellSourceId(cell) != -1)
            {
                HideIndicator(player);

                return;
            }

            EnsureIndicator(layer);

            _indicator.Position = layer.MapToLocal(cell);
            _indicator.Visible = true;
        }

        public override void HideIndicator(Player player)
        {
            if (_indicator != null && GodotObject.IsInstanceValid(_indicator))
            {
                _indicator.Visible = false;
            }
        }

        public override void DestroyIndicator()
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
