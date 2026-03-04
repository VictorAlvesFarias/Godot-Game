using Godot;
using Jogo25D.Characters;

namespace Jogo25D.UI
{
    public partial class MinimapUI : Control
    {
        public string PlayerGroupName { get; set; } = "players";
        public float ViewRadius { get; set; } = 1200f;
        public Color LocalPlayerColor { get; set; } = new Color(0.2f, 0.8f, 1f, 1f);
        public Color OtherPlayerColor { get; set; } = new Color(0.6f, 0.6f, 0.6f, 1f);
        public Color TileColor { get; set; } = new Color(0.4f, 0.4f, 0.45f, 0.9f);
        public Color BackgroundColor { get; set; } = new Color(0.08f, 0.1f, 0.12f, 0.95f);
        public float PlayerDotRadius { get; set; } = 4f;

        private Node localPlayer;
        private int localPeerId = 1;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(160, 160);

            if (Multiplayer != null &&
                Multiplayer.MultiplayerPeer != null &&
                Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected)
            {
                localPeerId = Multiplayer.GetUniqueId();
            }
        }

        public void SetLocalPlayer(Node player)
        {
            localPlayer = player;
        }

        public override void _Draw()
        {
            float mapSize = Mathf.Min(Size.X, Size.Y);
            float margin = 4f;

            Rect2 backgroundRect = new Rect2(
                margin,
                margin,
                mapSize - margin * 2,
                mapSize - margin * 2
            );

            DrawRect(backgroundRect, BackgroundColor);

            if (localPlayer == null || !IsInstanceValid(localPlayer))
                return;

            Vector2 playerPos = (localPlayer as Node2D)?.GlobalPosition ?? Vector2.Zero;
            Vector2 center = new Vector2(mapSize / 2f, mapSize / 2f);
            float innerSize = mapSize - margin * 2;
            float scale = innerSize / (ViewRadius * 2f);

            if (scale <= 0f)
                return;

            ScanTree(GetTree().Root, playerPos, center, scale);
            DrawPlayers(playerPos, center, scale);
        }

        private void ScanTree(Node node, Vector2 playerPos, Vector2 center, float scale)
        {
            if (node is TileMapLayer layer && IsInstanceValid(layer) && layer.GetParent().GetParent().GetParent<SubViewportContainer>().Visible)
            {
                DrawTileMapLayer(layer, playerPos, center, scale);
            }

            foreach (Node child in node.GetChildren())
            {
                ScanTree(child, playerPos, center, scale);
            }
        }

        private void DrawTileMapLayer(TileMapLayer layer, Vector2 playerPos, Vector2 center, float scale)
        {
            var usedCells = layer.GetUsedCells();

            if (usedCells == null || usedCells.Count == 0 || !layer.Enabled)
            { 
                return;
            }

            var tileSize = layer.TileSet.TileSize;

            foreach (Vector2I cell in usedCells)
            {
                var localPos = layer.MapToLocal(cell);
                var worldPos = layer.ToGlobal(localPos);
                var mapPos = WorldToMap(worldPos, playerPos, center, scale);
                var size = tileSize.X * scale;
                var rect = new Rect2(
                    mapPos - new Vector2(size / 2f, size / 2f),
                    new Vector2(size, size)
                );

                DrawRect(rect, TileColor);
            }
        }

        private void DrawPlayers(Vector2 playerPos, Vector2 center, float scale)
        {
            var players = GetTree().GetNodesInGroup(PlayerGroupName);

            foreach (Node node in players)
            {
                if (node is Player player && IsInstanceValid(player))
                {
                    Vector2 worldPos = player.GlobalPosition;
                    Vector2 mapPos = WorldToMap(worldPos, playerPos, center, scale);

                    bool isLocal =
                        localPlayer == player ||
                        (Multiplayer != null &&
                         player.GetMultiplayerAuthority() == localPeerId);

                    Color color = isLocal ? LocalPlayerColor : OtherPlayerColor;

                    DrawCircle(mapPos, PlayerDotRadius, color);
                }
            }
        }

        private Vector2 WorldToMap(Vector2 worldPos, Vector2 playerPos, Vector2 center, float scale)
        {
            Vector2 relative = worldPos - playerPos;
            return center + relative * scale;
        }

        public override void _Process(double delta)
        {
            QueueRedraw();
        }
    }
}
