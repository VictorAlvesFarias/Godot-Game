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

        public Node LocalPlayer { get; set; }
        public int LocalPeerId { get; set; } = 1;

        // Deslocamento (em unidades de mundo) do centro da visao em relacao
        // a posicao do player - alterado por quem arrasta o mapa (ver
        // FullscreenMapUI). Zero = centralizado no player, como sempre foi.
        public Vector2 PanOffset { get; set; } = Vector2.Zero;

        // Escala calculada no ultimo _Draw() - exposta pra quem precisa
        // converter um arrasto em tela (pixels) pra um deslocamento em
        // unidades de mundo (FullscreenMapUI.PanDrag).
        public float LastScale { get; private set; }

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

        public void SetLocalPlayer(Node player)
        {
            LocalPlayer = player;
        }

        public override void _Draw()
        {
            float margin = 4f;

            // Preenche o retangulo inteiro do Control (nao so um quadrado
            // no canto) - importante pro mapa em tela cheia (FullscreenMapUI),
            // que nao e quadrado. A escala usa o MENOR eixo pra ViewRadius
            // continuar representando um circulo sem distorcer.
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

        public void ScanTree(Node node, Vector2 playerPos, Vector2 center, float scale)
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

        public void DrawTileMapLayer(TileMapLayer layer, Vector2 playerPos, Vector2 center, float scale)
        {
            var usedCells = layer.GetUsedCells();

            if (usedCells == null || usedCells.Count == 0 || !layer.Enabled)
            {
                return;
            }

            var tileSize = layer.TileSet.TileSize;

            // Raio um pouco maior que ViewRadius (a visao pode nao ser
            // quadrada) - corta o trabalho por celula (transform + draw)
            // pras que estao fora da area visivel. Sem isso, um mundo ja
            // bastante explorado (dezenas de milhares de celulas geradas)
            // fazia esse loop desenhar TUDO a cada frame, mesmo o que
            // nunca aparece na tela - era a causa do FPS caindo muito ao
            // abrir o mapa em tela cheia (2a instancia de MinimapUI
            // rodando esse mesmo loop sem corte, em paralelo a do HUD).
            var cullRadius = ViewRadius * 1.5f;
            var cullRadiusSquared = cullRadius * cullRadius;

            foreach (Vector2I cell in usedCells)
            {
                var localPos = layer.MapToLocal(cell);
                var worldPos = layer.ToGlobal(localPos);

                if (worldPos.DistanceSquaredTo(playerPos) > cullRadiusSquared)
                {
                    continue;
                }

                var mapPos = WorldToMap(worldPos, playerPos, center, scale);
                var size = tileSize.X * scale;
                var rect = new Rect2(
                    mapPos - new Vector2(size / 2f, size / 2f),
                    new Vector2(size, size)
                );

                DrawRect(rect, TileColor);
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

        public override void _Process(double delta)
        {
            QueueRedraw();
        }
    }
}