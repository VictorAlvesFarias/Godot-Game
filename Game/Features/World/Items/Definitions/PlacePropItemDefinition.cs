using Godot;
using Jogo25D.Biomes;
using Jogo25D.Characters;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Entities;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Props;

namespace Jogo25D.Items
{
    // Item que coloca um prop do PropDB (portal, mesa de criacao) onde a forma inteira dele cabe (PropPlacement).
    public class PlacePropItemDefinition : ItemDefinition
    {
        #region Dinamic properties

        public string PropId { get; init; }
        public float Reach { get; init; } = 120f;
        public Polygon2D Indicator { get; set; }

        #endregion

        #region Core - Placement

        public override void UseAt(Player player, ItemData data, Vector2 position)
        {
            if (!EntitySpawner.SpawnPropAuthoritative(PropId, position, player.GetActiveDimensionId()))
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

            if (!PropPlacement.CanPlace(Dimension.Get(player.GetActiveDimensionId()), PropId, targetCell))
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

            if (!PropPlacement.CanPlace(Dimension.Get(player.GetActiveDimensionId()), PropId, cell))
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
            };

            BuildFootprintQuads(Indicator, layer, PropFootprints.Of(PropId));

            layer.AddChild(Indicator);
        }

        // Um quadrado por celula da forma do prop, relativo a celula da ancora (a posicao do indicador).
        private static void BuildFootprintQuads(Polygon2D indicator, TileMapLayer layer, System.Collections.Generic.IReadOnlyList<Vector2I> cells)
        {
            var size = (Vector2)layer.TileSet.TileSize;
            var half = size / 2f;
            var points = new Vector2[cells.Count * 4];
            var polygons = new Godot.Collections.Array();

            for (var i = 0; i < cells.Count; i++)
            {
                var center = new Vector2(cells[i].X * size.X, cells[i].Y * size.Y);

                points[i * 4] = center + new Vector2(-half.X, -half.Y);
                points[i * 4 + 1] = center + new Vector2(half.X, -half.Y);
                points[i * 4 + 2] = center + new Vector2(half.X, half.Y);
                points[i * 4 + 3] = center + new Vector2(-half.X, half.Y);
                polygons.Add(new[] { i * 4, i * 4 + 1, i * 4 + 2, i * 4 + 3 });
            }

            indicator.Polygon = points;
            indicator.Polygons = polygons;
        }

        #endregion
    }
}
