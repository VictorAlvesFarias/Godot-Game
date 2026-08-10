using Godot;
using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Hitboxes;
using Jogo25D.Portals;

namespace Jogo25D.Items
{
    public class ToolDefinition : ItemDefinition
    {
        #region Dinamic properties

        public float Reach { get; init; } = 120f;
        public float BreakTimeSeconds { get; init; } = 1.2f;
        public float SwingRange { get; init; } = 50f;

        public bool IsMining { get; set; }
        public Vector2I MiningCell { get; set; }
        public float MiningElapsed { get; set; }

        #endregion

        #region Node children references

        public Polygon2D Indicator { get; set; }

        #endregion

        #region Core - Mining

        public override void Use(Player player, ItemData instance)
        {
            var rawDir = player.Input.MousePosition - player.GlobalPosition;
            var dir = rawDir.LengthSquared() > 0.001f ? rawDir.Normalized() : Vector2.Right;
            var angle = dir.Angle();

            player.SetFacing(!(angle >= -1.5f && angle <= 1.5f));

            if (player.Sprite.Animation != "mining" || !player.Sprite.IsPlaying())
            {
                player.Sprite.Play("mining");
            }

            if (CanUse(instance) && HitboxScene != null && HitboxScene.Instantiate<Area2D>() is BaseHitbox swing)
            {
                swing.DirectionAngle = angle;
                swing.Owner = player;
                swing.DestroyInAllBodies = false;

                if (swing is MeleeHitbox melee)
                {
                    melee.Offset = dir * SwingRange;
                }

                player.GetParent().AddChild(swing);

                TriggerCooldownTimer(instance);
            }

            if (!player.IsOwner())
            {
                return;
            }

            var layer = player.GetActiveTileLayer();
            var baseLayer = player.GetActiveBaseLayer();

            if (layer == null)
            {
                ResetMining();

                return;
            }

            var (found, targetCell) = ResolveMiningTargetCell(player, layer, baseLayer, Reach);

            if (!found)
            {
                ResetMining();

                return;
            }

            UpdateMining(player, layer, baseLayer, targetCell, BreakTimeSeconds);
        }

        public void ResetMining()
        {
            IsMining = false;
            MiningElapsed = 0f;
        }

        private void UpdateMining(Player player, TileMapLayer layer, TileMapLayer baseLayer, Vector2I targetCell, float breakTimeSeconds)
        {
            var portal = ResolveMiningTargetPortal(player, layer, targetCell);

            if (portal == null && !IsSolid(layer, baseLayer, targetCell))
            {
                ResetMining();

                return;
            }

            if (!IsMining || MiningCell != targetCell)
            {
                IsMining = true;
                MiningCell = targetCell;
                MiningElapsed = 0f;
            }

            MiningElapsed += (float)player.GetPhysicsProcessDeltaTime();

            if (MiningElapsed < breakTimeSeconds)
            {
                return;
            }

            ResetMining();

            if (portal != null)
            {
                player.NetworkManager?.BreakPortalClientRequest(portal.Name, player.GetActiveDimensionId());
            }
            else
            {
                player.NetworkManager?.BreakBlockClientRequest(targetCell, player.GetActiveDimensionId());
            }
        }

        private static Portal ResolveMiningTargetPortal(Player player, TileMapLayer layer, Vector2I targetCell)
        {
            var parent = player.GetParent();

            if (parent == null)
            {
                return null;
            }

            foreach (var child in parent.GetChildren())
            {
                if (child is Portal portal && layer.LocalToMap(layer.ToLocal(portal.GlobalPosition)) == targetCell)
                {
                    return portal;
                }
            }

            return null;
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

        // Tronco/copa de arvore so existem na layer Base (sem espelho na Texture) - qualquer
        // checagem de "tem bloco aqui" pra mineracao precisa olhar as duas camadas, senao uma
        // celula que so existe na Base fica impossivel de mirar/quebrar.
        private static bool IsSolid(TileMapLayer layer, TileMapLayer baseLayer, Vector2I cell)
        {
            return layer.GetCellSourceId(cell) != -1 || (baseLayer != null && baseLayer.GetCellSourceId(cell) != -1);
        }

        private static (bool FoundSolid, Vector2I SolidCell, Vector2I LastEmptyCell) RaycastTiles(TileMapLayer layer, TileMapLayer baseLayer, Vector2 origin, Vector2 aimPosition, float reach)
        {
            var toAim = aimPosition - origin;
            var distance = Mathf.Min(toAim.Length(), reach);
            var direction = toAim.LengthSquared() > 0.001f ? toAim.Normalized() : Vector2.Right;

            var tileSize = Mathf.Max(1, layer.TileSet.TileSize.X);
            var stepSize = tileSize * 0.5f;
            var steps = Mathf.Max(1, Mathf.CeilToInt(distance / stepSize));

            var lastEmptyCell = layer.LocalToMap(layer.ToLocal(origin));

            for (int i = 1; i <= steps; i++)
            {
                var sampleDistance = Mathf.Min(distance, i * stepSize);
                var samplePos = origin + direction * sampleDistance;
                var cell = layer.LocalToMap(layer.ToLocal(samplePos));

                if (IsSolid(layer, baseLayer, cell))
                {
                    return (true, cell, lastEmptyCell);
                }

                lastEmptyCell = cell;
            }

            return (false, default, lastEmptyCell);
        }

        private static (bool Found, Vector2I Cell) ResolveMiningTargetCell(Player player, TileMapLayer layer, TileMapLayer baseLayer, float reach)
        {
            if (player.Input.RestrictMiningToAccessible)
            {
                var hit = RaycastTiles(layer, baseLayer, player.GlobalPosition, player.Input.MousePosition, reach);

                if (hit.FoundSolid)
                {
                    return (true, hit.SolidCell);
                }

                var aimCell = ResolveCellInRange(player, layer, reach);

                return (ResolveMiningTargetPortal(player, layer, aimCell) != null, aimCell);
            }

            var cell = ResolveCellInRange(player, layer, reach);

            return (IsSolid(layer, baseLayer, cell) || ResolveMiningTargetPortal(player, layer, cell) != null, cell);
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
            var baseLayer = player.GetActiveBaseLayer();

            if (layer == null)
            {
                HideIndicator(player);

                return;
            }

            var (found, cell) = ResolveMiningTargetCell(player, layer, baseLayer, Reach);

            if (!found)
            {
                HideIndicator(player);

                return;
            }

            EnsureIndicator(layer);

            Indicator.Color = new Color(1f, 1f, 1f, IsMining ? 0.45f : 0.15f);
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
