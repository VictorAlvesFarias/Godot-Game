using Godot;
using Jogo25D.Biomes;
using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items
{
    public class BlockItemDefinition : ItemDefinition
    {
        #region Dinamic properties

        public string BlockId { get; init; }
        public float Reach { get; init; } = 120f;

        #endregion

        #region Node children references

        public Polygon2D Indicator { get; set; }

        #endregion

        #region Core - Placement

        // Lado autoritativo: converte a posicao de volta pra celula, pinta e consome.
        public override void UseAt(Player player, ItemData data, Vector2 position)
        {
            if (player.GetActiveTileLayer() is not TerrainLayer layer)
            {
                return;
            }

            var cell = layer.LocalToMap(layer.ToLocal(position));

            if (!layer.PlaceBlockAuthoritative(cell, BlockId))
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

            TriggerCooldownTimer(instance);

            player.UseItemAtRequest(instance.InstanceId, layer.ToGlobal(layer.MapToLocal(targetCell)));
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
            var baseLayer = player.GetActiveBaseLayer();

            if (layer.GetCellSourceId(cell) != -1 || (baseLayer != null && baseLayer.GetCellSourceId(cell) != -1))
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
                Color = new Color(0.3f, 1f, 0.4f, 0.3f),
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
