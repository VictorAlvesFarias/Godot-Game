using Godot;
using Jogo25D.Characters;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using Jogo25D.Core;

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

        #region Godot implementation

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(160, 160);

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

        private float _redrawTimer;

        public override void _Process(double delta)
        {
            _redrawTimer += (float)delta;

            if (_redrawTimer < MinimapConstants.REDRAW_INTERVAL_SECONDS)
            {
                return;
            }

            _redrawTimer = 0f;

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

            if (Game.Managers.TileStreamingManager.Node != null)
            {
                texture = Game.Managers.TileStreamingManager.Node.GetDiscoveredTexture(layer, out origin);
            }

            if (texture != null)
            {
                DrawDiscoveredTexture(layer, texture, origin, playerPos, center, scale);

                return;
            }
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
