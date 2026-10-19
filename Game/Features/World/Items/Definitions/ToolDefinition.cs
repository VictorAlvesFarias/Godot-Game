using Godot;
using Jogo25D.Biomes;
using Jogo25D.Blocks;
using Jogo25D.Core;
using Jogo25D.Characters;
using Jogo25D.Dimensions;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Hitboxes;
using Jogo25D.Props;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Items
{
    public class ToolDefinition : ItemDefinition
    {
        #region Dinamic properties

        public float Reach { get; init; } = 120f;
        public float BreakTimeSeconds { get; init; } = 1.2f;
        public float SwingRange { get; init; } = 50f;

        // 0 = so o bloco mirado; N = quebra o circulo de raio N em volta dele (dx^2 + dy^2 <= N^2).
        public int BreakRadius { get; init; }

        // Nivel da ferramenta: so quebra blocos cujo RequiredTier seja menor ou igual.
        public int Tier { get; init; } = 1;

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
            var prop = ResolveMiningTargetProp(player, layer, targetCell);

            if (prop == null && (!TileQueries.HasBlock(layer, baseLayer, targetCell) || !CanBreak(player, targetCell)))
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

            if (MiningElapsed < breakTimeSeconds * (prop == null ? BreakTimeScaleAt(player, targetCell) : 1f))
            {
                return;
            }

            ResetMining();

            if (prop != null)
            {
                prop.BreakClientRequest();
            }
            else
            {
                BreakArea(player, layer, baseLayer, targetCell);
            }
        }

        private void BreakArea(Player player, TileMapLayer layer, TileMapLayer baseLayer, Vector2I center)
        {
            if (layer is not TerrainLayer terrain)
            {
                return;
            }

            for (var dy = -BreakRadius; dy <= BreakRadius; dy++)
            {
                for (var dx = -BreakRadius; dx <= BreakRadius; dx++)
                {
                    var cell = center + new Vector2I(dx, dy);

                    if (IsInBreakArea(dx, dy, BreakRadius) && TileQueries.HasBlock(layer, baseLayer, cell) && CanBreak(player, cell))
                    {
                        terrain.BreakBlockClientRequest(cell);
                    }
                }
            }
        }

        // Prop (portal, mesa de criacao) cuja celula e a celula mirada.
        private static Prop ResolveMiningTargetProp(Player player, TileMapLayer layer, Vector2I targetCell)
        {
            var parent = player.GetParent();

            if (parent == null)
            {
                return null;
            }

            foreach (var prop in Prop.Active)
            {
                if (prop.GetParent() == parent && layer.LocalToMap(layer.ToLocal(prop.GlobalPosition)) == targetCell)
                {
                    return prop;
                }
            }

            return null;
        }

        // Bloco da celula (o do terreno resolvido: gerado, minerio ou colocado), ou nulo.
        private static BlockDefinition BlockAt(Player player, Vector2I cell)
        {
            var terrain = Dimension.Get(player.GetActiveDimensionId())?.EnsureTerrain().Terrain(cell.X, cell.Y) ?? -1;

            return terrain < 0 ? null : BlockDB.FindByTerrain(terrain);
        }

        // Escala do tempo de quebra do bloco da celula (a tocha quebra bem mais rapido que a terra).
        private static float BreakTimeScaleAt(Player player, Vector2I cell)
        {
            return BlockAt(player, cell)?.BreakTimeScale ?? 1f;
        }

        // A ferramenta tem nivel para o bloco da celula.
        public bool CanBreak(Player player, Vector2I cell)
        {
            return (BlockAt(player, cell)?.RequiredTier ?? 1) <= Tier;
        }

        private static Vector2 CellCenter(TileMapLayer layer, Vector2I cell)
        {
            return layer.ToGlobal(layer.MapToLocal(cell));
        }

        // Caixa de colisao do jogador, um pouco encolhida para nao contar o chao e as paredes em que ela so encosta.
        private static Rect2 BodyBox(Player player)
        {
            if (player.Shape?.Shape is RectangleShape2D rectangle)
            {
                var size = rectangle.Size - Vector2.One * 2f;

                return new Rect2(player.Shape.GlobalPosition - size / 2f, size);
            }

            return new Rect2(player.GlobalPosition - Vector2.One * 7f, Vector2.One * 14f);
        }

        private static IEnumerable<Vector2I> CellsIn(TileMapLayer layer, Rect2 box)
        {
            var first = layer.LocalToMap(layer.ToLocal(box.Position));
            var last = layer.LocalToMap(layer.ToLocal(box.End - Vector2.One * 0.01f));

            for (var y = first.Y; y <= last.Y; y++)
            {
                for (var x = first.X; x <= last.X; x++)
                {
                    yield return new Vector2I(x, y);
                }
            }
        }

        // Primeiro bloco que colide e que o corpo do jogador encontra indo ate o ponto mirado (no maximo o alcance): e ele
        // que impede o jogador de chegar la. Um vao mais estreito que o corpo nao deixa passar. Entre os blocos tocados no
        // mesmo passo, fica o mais perto do caminho do centro do corpo. Sem obstaculo ate o ponto, o alvo e o bloco dele,
        // com ou sem colisao (a tocha).
        private static (bool Found, Vector2I Cell) FirstSolidOnSweep(Player player, TileMapLayer layer, TileMapLayer baseLayer, Vector2 aim, float reach)
        {
            const float step = 2f;

            var box = BodyBox(player);
            var start = box.GetCenter();
            var toAim = aim - start;
            var distance = Mathf.Min(toAim.Length(), reach);
            var direction = toAim.LengthSquared() > 0.001f ? toAim.Normalized() : Vector2.Down;
            var touching = CellsIn(layer, box).ToHashSet();

            for (var traveled = step; traveled < distance + step; traveled += step)
            {
                var moved = new Rect2(box.Position + direction * Mathf.Min(traveled, distance), box.Size);
                var hits = CellsIn(layer, moved).Where(cell => !touching.Contains(cell) && TileQueries.Collides(layer, baseLayer, cell)).ToList();

                if (hits.Count == 0)
                {
                    continue;
                }

                var cell = hits
                    .OrderBy(c => Mathf.Abs(direction.Cross(CellCenter(layer, c) - start)))
                    .ThenBy(c => CellCenter(layer, c).DistanceSquaredTo(start))
                    .First();

                return CellCenter(layer, cell).DistanceTo(player.GlobalPosition) <= reach ? (true, cell) : (false, default);
            }

            var aimCell = layer.LocalToMap(layer.ToLocal(aim));

            return TileQueries.HasBlock(layer, baseLayer, aimCell) && CellCenter(layer, aimCell).DistanceTo(player.GlobalPosition) <= reach
                ? (true, aimCell)
                : (false, default);
        }

        // Alvo do indicador e da quebra. O prop sob o mouse (portal, mesa) tem prioridade. Depois, so ha alvo com o mouse na area de conexao
        // de um bloco (a celula dele ou uma das 8 vizinhas tem bloco, com ou sem colisao). Modo livre: o bloco sob o mouse, ou a vizinha mais proxima
        // do mouse. Modo de aproximacao: o primeiro bloco que colide e que o corpo do jogador encontra indo ate o mouse (ou, com o mouse no ar,
        // ate a vizinha mais proxima dele).
        private static (bool Found, Vector2I Cell) ResolveMiningTargetCell(Player player, TileMapLayer layer, TileMapLayer baseLayer, float reach)
        {
            var origin = player.GlobalPosition;
            var mouse = player.Input.MousePosition;
            var mouseCell = layer.LocalToMap(layer.ToLocal(mouse));

            bool InReach(Vector2I cell) => CellCenter(layer, cell).DistanceTo(origin) <= reach;

            if (ResolveMiningTargetProp(player, layer, mouseCell) != null && InReach(mouseCell))
            {
                return (true, mouseCell);
            }

            var mouseBlock = TileQueries.HasBlock(layer, baseLayer, mouseCell);
            var neighbors = new List<Vector2I>();

            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var cell = mouseCell + new Vector2I(dx, dy);

                    if ((dx != 0 || dy != 0) && TileQueries.HasBlock(layer, baseLayer, cell))
                    {
                        neighbors.Add(cell);
                    }
                }
            }

            if (!mouseBlock && neighbors.Count == 0)
            {
                return (false, default);
            }

            var nearest = neighbors.Where(InReach).OrderBy(cell => CellCenter(layer, cell).DistanceSquaredTo(mouse));

            if (!player.Input.RestrictMiningToAccessible)
            {
                if (mouseBlock)
                {
                    return InReach(mouseCell) ? (true, mouseCell) : (false, default);
                }

                foreach (var cell in nearest)
                {
                    return (true, cell);
                }

                return (false, default);
            }

            var aim = mouseBlock ? mouse : CellCenter(layer, neighbors.OrderBy(cell => CellCenter(layer, cell).DistanceSquaredTo(mouse)).First());

            return FirstSolidOnSweep(player, layer, baseLayer, aim, reach);
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

            // Ferramenta fraca para o bloco: mesmo alvo, em vermelho, e a quebra nao avanca.
            Indicator.Color = CanBreak(player, cell)
                ? new Color(1f, 1f, 1f, IsMining ? 0.45f : 0.15f)
                : new Color(1f, 0.3f, 0.3f, 0.35f);
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

            var (points, quads) = BuildAreaTiles(layer, BreakRadius);

            Indicator = new Polygon2D
            {
                ZIndex = 10,
                Polygon = points,
                Polygons = quads,
            };

            layer.AddChild(Indicator);
        }

        // Um quadrado por tile da area de quebra: o indicador mostra exatamente os blocos que caem.
        private static (Vector2[] Points, Godot.Collections.Array Quads) BuildAreaTiles(TileMapLayer layer, int radius)
        {
            var size = (Vector2)layer.TileSet.TileSize;
            var half = size / 2f;
            var points = new System.Collections.Generic.List<Vector2>();
            var quads = new Godot.Collections.Array();

            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (!IsInBreakArea(dx, dy, radius))
                    {
                        continue;
                    }

                    var center = new Vector2(dx * size.X, dy * size.Y);
                    var first = points.Count;

                    points.Add(center + new Vector2(-half.X, -half.Y));
                    points.Add(center + new Vector2(half.X, -half.Y));
                    points.Add(center + new Vector2(half.X, half.Y));
                    points.Add(center + new Vector2(-half.X, half.Y));

                    quads.Add(new[] { first, first + 1, first + 2, first + 3 });
                }
            }

            return (points.ToArray(), quads);
        }

        // Circulo em pixel arte: entra a celula com dx^2 + dy^2 <= r^2 (raio 1 = cruz, 2 = losango, maiores com o biquinho nos eixos).
        private static bool IsInBreakArea(int dx, int dy, int radius)
        {
            return dx * dx + dy * dy <= radius * radius;
        }

        #endregion
    }
}
