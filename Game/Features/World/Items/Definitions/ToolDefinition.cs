using Godot;
using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Hitboxes;
using Jogo25D.Portals;

namespace Jogo25D.Items
{
    public class ToolDefinition : ItemDefinition
    {
        private const float AimingAlpha = 0.15f;
        private const float MiningAlpha = 0.45f;

        public float Reach { get; init; } = 120f;
        public float BreakTimeSeconds { get; init; } = 1.2f;
        public float SwingRange { get; init; } = 50f;

        private bool _isMining;
        private Vector2I _miningCell;
        private float _miningElapsed;
        private Polygon2D _indicator;

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

            if (layer == null)
            {
                ResetMining();

                return;
            }

            var (found, targetCell) = ResolveMiningTargetCell(player, layer, Reach);

            if (!found)
            {
                ResetMining();

                return;
            }

            UpdateMining(player, layer, targetCell, BreakTimeSeconds);
        }

        public void ResetMining()
        {
            _isMining = false;
            _miningElapsed = 0f;
        }

        private void UpdateMining(Player player, TileMapLayer layer, Vector2I targetCell, float breakTimeSeconds)
        {
            var portal = ResolveMiningTargetPortal(player, layer, targetCell);

            if (portal == null && layer.GetCellSourceId(targetCell) == -1)
            {
                ResetMining();

                return;
            }

            if (!_isMining || _miningCell != targetCell)
            {
                _isMining = true;
                _miningCell = targetCell;
                _miningElapsed = 0f;
            }

            _miningElapsed += (float)player.GetPhysicsProcessDeltaTime();

            if (_miningElapsed < breakTimeSeconds)
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

        private static (bool FoundSolid, Vector2I SolidCell, Vector2I LastEmptyCell) RaycastTiles(TileMapLayer layer, Vector2 origin, Vector2 aimPosition, float reach)
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

                if (layer.GetCellSourceId(cell) != -1)
                {
                    return (true, cell, lastEmptyCell);
                }

                lastEmptyCell = cell;
            }

            return (false, default, lastEmptyCell);
        }

        private static (bool Found, Vector2I Cell) ResolveMiningTargetCell(Player player, TileMapLayer layer, float reach)
        {
            if (player.Input.RestrictMiningToAccessible)
            {
                var hit = RaycastTiles(layer, player.GlobalPosition, player.Input.MousePosition, reach);

                if (hit.FoundSolid)
                {
                    return (true, hit.SolidCell);
                }

                var aimCell = ResolveCellInRange(player, layer, reach);

                return (ResolveMiningTargetPortal(player, layer, aimCell) != null, aimCell);
            }

            var cell = ResolveCellInRange(player, layer, reach);

            return (layer.GetCellSourceId(cell) != -1 || ResolveMiningTargetPortal(player, layer, cell) != null, cell);
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

            var (found, cell) = ResolveMiningTargetCell(player, layer, Reach);

            if (!found)
            {
                HideIndicator(player);

                return;
            }

            EnsureIndicator(layer);

            _indicator.Color = new Color(1f, 1f, 1f, _isMining ? MiningAlpha : AimingAlpha);
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
                Polygon = BuildTileQuad(layer),
            };

            layer.AddChild(_indicator);
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
    }
}
