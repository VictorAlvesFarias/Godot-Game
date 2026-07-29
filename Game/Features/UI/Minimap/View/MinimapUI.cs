using Godot;
using Jogo25D.Characters;
using Jogo25D.Chunks;

namespace Jogo25D.UI
{
    public partial class MinimapUI : Control
    {
        public string PlayerGroupName { get; set; } = "players";
        public float ViewRadius { get; set; } = 1200f;
        public Color LocalPlayerColor { get; set; } = new Color(0.2f, 0.8f, 1f, 1f);
        public Color OtherPlayerColor { get; set; } = new Color(0.6f, 0.6f, 0.6f, 1f);
        public Color TileColor { get; set; } = new Color(0.4f, 0.4f, 0.45f, 0.9f);

        // Celula ja explorada mas fora do raio de streaming agora (o chunk
        // descarregou) - um pouco mais escura/apagada que uma carregada de
        // verdade, pra dar a sensacao de "memoria" do mapa (fog of war).
        public Color DiscoveredTileColor { get; set; } = new Color(0.4f, 0.4f, 0.45f, 0.45f);
        public Color BackgroundColor { get; set; } = new Color(0.08f, 0.1f, 0.12f, 0.95f);
        public float PlayerDotRadius { get; set; } = 4f;

        public Node LocalPlayer { get; set; }
        public int LocalPeerId { get; set; } = 1;

        private ChunkStreamingManager _chunkStreamingManager;

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

            _chunkStreamingManager = GetTree().Root.GetNodeOrNull<ChunkStreamingManager>(ChunkStreamingManager.DEFAULT_NODE_PATH);

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
            if (!layer.Enabled)
            {
                return;
            }

            var tileSize = layer.TileSet.TileSize;

            // Raio um pouco maior que ViewRadius (a visao pode nao ser
            // quadrada). Antes isso so cortava o DESENHO depois de ja ter
            // iterado layer.GetUsedCells() inteiro (transform+distancia
            // pra CADA celula ja pintada no mundo, nao so a visivel) - num
            // mundo com streaming de chunks isso cresce com a area total
            // ja explorada por TODOS os players (nao so o proprio), entao
            // quanto mais os players se espalhavam mais esse loop ficava
            // caro TODO FRAME (era a causa real da queda de FPS). Agora o
            // loop em si so percorre a caixa de celulas dentro do raio -
            // custo fixo, nao cresce com o tamanho do mundo carregado.
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

            // O mapa em tela cheia usa um ViewRadius bem maior (ate 12000,
            // contra ~1200 do minimapa do HUD) pra dar zoom-out - a caixa
            // de celulas acima cresce em AREA (ao quadrado) com o raio, e
            // com um raio grande isso virava centenas de milhares de
            // celulas por frame (era a causa da queda de FPS ao abrir o
            // mapa cheio). Sem stride, cada celula fica menor que 1 pixel
            // na tela quando bem afastado, entao nao faz sentido processar
            // uma por uma - passo (em celulas) escolhido pra amostrar
            // aproximadamente 1 celula por pixel de tela, mantendo o custo
            // do loop proporcional ao tamanho do painel, nao ao ViewRadius.
            var worldUnitsPerPixel = scale > 0f ? 1f / scale : tileSize.X;
            var strideCells = Mathf.Max(1, Mathf.RoundToInt(worldUnitsPerPixel / tileSize.X));

            for (int x = minX; x <= maxX; x += strideCells)
            {
                for (int y = minY; y <= maxY; y += strideCells)
                {
                    var cell = new Vector2I(x, y);
                    var isLoaded = layer.GetCellSourceId(cell) != -1;
                    var isDiscovered = isLoaded || (_chunkStreamingManager != null && _chunkStreamingManager.IsDiscovered(layer, cell));

                    if (!isDiscovered)
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

                    DrawRect(rect, isLoaded ? TileColor : DiscoveredTileColor);
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

        public override void _Process(double delta)
        {
            QueueRedraw();
        }
    }
}