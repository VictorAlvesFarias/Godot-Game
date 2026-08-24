using Godot;
using Jogo25D.Core;
using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items
{
    public class PortalItemDefinition : ItemDefinition
    {
        #region Dinamic properties

        public float Reach { get; init; } = 120f;
        public Polygon2D Indicator { get; set; }

        #endregion

        #region Core - Placement

        // Lado autoritativo: pede pro DimensionManager posicionar o prop e consome o item.
        public override void UseAt(Player player, ItemData data, Vector2 position)
        {
            var dimensions = Game.Managers.DimensionManager.Node;

            if (dimensions == null || !dimensions.SpawnPropAuthoritative("portal", position, player.GetActiveDimensionId()))
            {
                return;
            }

            player.RemoveItemRequest(data.InstanceId, 1);
        }

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

            if (!IsGroundedCell(layer, targetCell))
            {
                return;
            }

            var targetPosition = layer.ToGlobal(layer.MapToLocal(targetCell));

            TriggerCooldownTimer(instance);

            player.UseItemAtRequest(instance.InstanceId, targetPosition);
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

        private static bool IsGroundedCell(TileMapLayer layer, Vector2I cell)
        {
            return layer.GetCellSourceId(cell) == -1 && layer.GetCellSourceId(cell + Vector2I.Down) != -1;
        }

        #endregion

        #region Core - Indicator

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

            if (!IsGroundedCell(layer, cell))
            {
                HideIndicator(player);

                return;
            }

            EnsureIndicator(layer);

            Indicator.Position = layer.MapToLocal(cell);
            Indicator.Visible = true;
        }

        public override void HideIndicator(Player player)
        {
            if (Indicator != null && GodotObject.IsInstanceValid(Indicator))
            {
                Indicator.Visible = false;
            }
        }

        public override void DestroyIndicator()
        {
            if (Indicator != null && GodotObject.IsInstanceValid(Indicator))
            {
                Indicator.QueueFree();
            }

            Indicator = null;
        }

        private void EnsureIndicator(TileMapLayer layer)
        {
            if (Indicator != null && GodotObject.IsInstanceValid(Indicator) && Indicator.GetParent() == layer)
            {
                return;
            }

            if (Indicator != null && GodotObject.IsInstanceValid(Indicator))
            {
                Indicator.QueueFree();
            }

            Indicator = new Polygon2D
            {
                ZIndex = 10,
                Color = new Color(0.6f, 0.3f, 1f, 0.3f),
                Polygon = BuildTileQuad(layer),
            };

            layer.AddChild(Indicator);
        }

        private static Vector2[] BuildTileQuad(TileMapLayer layer)
        {
            var half = (Vector2)layer.TileSet.TileSize / 2f;

            return new[]
            {
                new Vector2(-half.X, -half.Y),
                new Vector2(half.X, -half.Y),
                new Vector2(half.X, half.Y),
                new Vector2(-half.X, half.Y),
            };
        }

        #endregion
    }
}
