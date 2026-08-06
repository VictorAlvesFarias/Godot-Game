using Godot;
using Jogo25D.Characters;
using Jogo25D.Chunks;
using Jogo25D.Constants;

namespace Jogo25D.UI
{
    public partial class MinimapUI : Control
    {
        #region Dinamic properties

        public string PlayerGroupName { get; set; } = "players";
        public float ViewRadius { get; set; } = 1200f;
        public Color LocalPlayerColor { get; set; } = new Color(0.2f, 0.8f, 1f, 1f);
        public Color OtherPlayerColor { get; set; } = new Color(0.6f, 0.6f, 0.6f, 1f);
        public Color TileColor { get; set; } = new Color(0.4f, 0.4f, 0.45f, 0.9f);
        public Color BackgroundColor { get; set; } = new Color(0.08f, 0.1f, 0.12f, 0.95f);
        public float PlayerDotRadius { get; set; } = 4f;

        public Node LocalPlayer { get; set; }
        public int LocalPeerId { get; set; } = 1;

        public Vector2 PanOffset { get; set; } = Vector2.Zero;

        public float LastScale { get; private set; }

        #endregion

        #region Node references

        public ChunkStreamingManager ChunkStreamingManager { get; set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(160, 160);

            ChunkStreamingManager = GetTree().Root.GetNodeOrNull<ChunkStreamingManager>(StaticNodePathsConstants.ChunkStreamingManager);

            if (Multiplayer != null &&
                Multiplayer.MultiplayerPeer != null &&
                Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected)
            {
                LocalPeerId = Multiplayer.GetUniqueId();
            }
        }

        public override void _Draw()
        {
            float margin = 4f;

            Rect2 backgroundRect = new Rect2(
                margin,
                margin,
                Size.X - margin * 2,
                Size.Y - margin * 2
            );

            DrawRect(backgroundRect, BackgroundColor);

            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                return;
            }

            Vector2 playerPos = (LocalPlayer as Node2D)?.GlobalPosition ?? Vector2.Zero;
            Vector2 viewCenterWorldPos = playerPos + PanOffset;
            Vector2 center = new Vector2(Size.X / 2f, Size.Y / 2f);
            float innerSize = Mathf.Min(Size.X, Size.Y) - margin * 2;
            float scale = innerSize / (ViewRadius * 2f);

            LastScale = scale;

            if (scale <= 0f)
            {
                return;
            }

            ScanTree(GetTree().Root, viewCenterWorldPos, center, scale);
            DrawPlayers(viewCenterWorldPos, center, scale);
        }

        public override void _Process(double delta)
        {
            QueueRedraw();
        }

        #endregion

        #region Core - Player tracking

        public void SetLocalPlayer(Node player)
        {
            LocalPlayer = player;
        }

        #endregion

        #region Core - Rendering

        public void ScanTree(Node node, Vector2 playerPos, Vector2 center, float scale)
        {
            if (node is TileMapLayer layer && IsInstanceValid(layer))
            {
                // Nem todo TileMapLayer da arvore fica embaixo de um SubViewportContainer (ex.:
                // um layer direto na raiz da dimensao) - GetParent<T>() do proprio Godot lanca
                // InvalidCastException se o ancestral nesse nivel nao for do tipo pedido, entao
                // usa GetParentOrNull e so desenha se o container realmente existir e existir e
                // estiver visivel.
                var container = layer.GetParent()?.GetParent()?.GetParentOrNull<SubViewportContainer>();

                if (container != null && container.Visible)
                {
                    DrawTileMapLayer(layer, playerPos, center, scale);
                }
            }

            foreach (Node child in node.GetChildren())
            {
                ScanTree(child, playerPos, center, scale);
            }
        }

        public void DrawTileMapLayer(TileMapLayer layer, Vector2 playerPos, Vector2 center, float scale)
        {
            if (!layer.Enabled)
            {
                return;
            }

            Texture2D texture = null;
            var origin = Vector2I.Zero;

            if (ChunkStreamingManager != null)
            {
                texture = ChunkStreamingManager.GetDiscoveredTexture(layer, out origin);
            }

            if (texture != null)
            {
                DrawDiscoveredTexture(layer, texture, origin, playerPos, center, scale);

                return;
            }

            DrawStaticLayerCells(layer, playerPos, center, scale);
        }

        private void DrawDiscoveredTexture(TileMapLayer layer, Texture2D texture, Vector2I origin, Vector2 playerPos, Vector2 center, float scale)
        {
            var cullRadius = ViewRadius * 1.5f;

            var cellMin = layer.LocalToMap(layer.ToLocal(playerPos - new Vector2(cullRadius, cullRadius)));
            var cellMax = layer.LocalToMap(layer.ToLocal(playerPos + new Vector2(cullRadius, cullRadius)));

            var pixelMinX = Mathf.Min(cellMin.X, cellMax.X) - origin.X;
            var pixelMinY = Mathf.Min(cellMin.Y, cellMax.Y) - origin.Y;
            var pixelMaxX = Mathf.Max(cellMin.X, cellMax.X) - origin.X;
            var pixelMaxY = Mathf.Max(cellMin.Y, cellMax.Y) - origin.Y;

            var srcRegion = new Rect2(pixelMinX, pixelMinY, pixelMaxX - pixelMinX, pixelMaxY - pixelMinY);

            var destRect = new Rect2(
                center - new Vector2(cullRadius, cullRadius) * scale,
                new Vector2(cullRadius, cullRadius) * scale * 2f);

            DrawTextureRectRegion(texture, destRect, srcRegion);
        }

        private void DrawStaticLayerCells(TileMapLayer layer, Vector2 playerPos, Vector2 center, float scale)
        {
            var tileSize = layer.TileSet.TileSize;
            var cullRadius = ViewRadius * 1.5f;
            var cullRadiusSquared = cullRadius * cullRadius;

            var topLeftLocal = layer.ToLocal(playerPos - new Vector2(cullRadius, cullRadius));
            var bottomRightLocal = layer.ToLocal(playerPos + new Vector2(cullRadius, cullRadius));

            var boxStart = layer.LocalToMap(topLeftLocal);
            var boxEnd = layer.LocalToMap(bottomRightLocal);

            var minX = Mathf.Min(boxStart.X, boxEnd.X);
            var maxX = Mathf.Max(boxStart.X, boxEnd.X);
            var minY = Mathf.Min(boxStart.Y, boxEnd.Y);
            var maxY = Mathf.Max(boxStart.Y, boxEnd.Y);

            var boxWidthCells = maxX - minX + 1;
            var boxHeightCells = maxY - minY + 1;
            var boxCellsPerAxis = Mathf.Max(boxWidthCells, boxHeightCells);
            var strideCells = Mathf.Max(1, Mathf.CeilToInt(boxCellsPerAxis / (float)160));

            for (int x = minX; x <= maxX; x += strideCells)
            {
                for (int y = minY; y <= maxY; y += strideCells)
                {
                    var cell = new Vector2I(x, y);

                    if (layer.GetCellSourceId(cell) == -1)
                    {
                        continue;
                    }

                    var worldPos = layer.ToGlobal(layer.MapToLocal(cell));

                    if (worldPos.DistanceSquaredTo(playerPos) > cullRadiusSquared)
                    {
                        continue;
                    }

                    var mapPos = WorldToMap(worldPos, playerPos, center, scale);
                    var size = tileSize.X * scale * strideCells;
                    var rect = new Rect2(
                        mapPos - new Vector2(size / 2f, size / 2f),
                        new Vector2(size, size)
                    );

                    DrawRect(rect, TileColor);
                }
            }
        }

        public void DrawPlayers(Vector2 playerPos, Vector2 center, float scale)
        {
            var players = GetTree().GetNodesInGroup(PlayerGroupName);

            foreach (Node node in players)
            {
                if (node is Player player && IsInstanceValid(player))
                {
                    Vector2 worldPos = player.GlobalPosition;
                    Vector2 mapPos = WorldToMap(worldPos, playerPos, center, scale);

                    bool isLocal =
                        LocalPlayer == player ||
                        (Multiplayer != null &&
                         player.GetMultiplayerAuthority() == LocalPeerId);

                    Color color = isLocal ? LocalPlayerColor : OtherPlayerColor;

                    DrawCircle(mapPos, PlayerDotRadius, color);
                }
            }
        }

        public Vector2 WorldToMap(Vector2 worldPos, Vector2 playerPos, Vector2 center, float scale)
        {
            Vector2 relative = worldPos - playerPos;
            return center + relative * scale;
        }

        #endregion
    }
}
