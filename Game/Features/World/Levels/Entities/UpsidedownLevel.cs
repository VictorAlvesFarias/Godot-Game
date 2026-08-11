using Godot;
using Jogo25D.Biomes;
using Jogo25D.Chunks;
using Jogo25D.Constants;

namespace Jogo25D.Levels
{
    // Script SO do editor (roda em jogo tambem, mas o guard Engine.IsEditorHint() no topo do
    // setter garante que nunca faz nada em runtime de verdade - ver comentario la embaixo).
    //
    // Marca "Generate Editor Terrain" no Inspector -> gera um mapa de 1000x500 tiles (centrado
    // na origem) usando ChunkGenerator.Paint, o MESMO metodo que o jogo de verdade chama pra
    // gerar chunk (ChunkStreamingManager.LoadChunkAsync -> ChunkGenerator.PaintAsync usa a
    // mesma logica, so cede frame por frame) - direto nas 3 layers (Base/Bordercap/Texture) da
    // cena, pra poder ver no editor sem precisar rodar o jogo.
    //
    // Desmarca -> restaura os tiles ORIGINAIS do projeto (o que estava desenhado a mao na cena
    // antes da primeira geracao) - guardados em BaseBackup/BordercapBackup/TextureBackup
    // (PackedByteArray, o mesmo formato binario que o proprio Godot usa pra serializar
    // tile_map_data no .tscn) na PRIMEIRA vez que gera, pra nao perder o desenho original
    // mesmo trocando de seed/gerando de novo varias vezes.
    [Tool]
    public partial class UpsidedownLevel : Node2D
    {
        private const int GeneratedWidthTiles = 1000;
        private const int GeneratedHeightTiles = 500;
        private const string TileMapDataProperty = "tile_map_data";

        [Export]
        public long PreviewSeed { get; set; } = 1;

        private bool _generateEditorTerrain;

        [Export]
        public bool GenerateEditorTerrain
        {
            get => _generateEditorTerrain;
            set
            {
                if (_generateEditorTerrain == value)
                {
                    return;
                }

                _generateEditorTerrain = value;

                // So faz alguma coisa dentro do editor - se essa flag ficar salva como true na
                // cena (usuario esqueceu de desmarcar antes de salvar) e o jogo rodar de
                // verdade, isso aqui NUNCA pode gerar/apagar tile por cima da geracao
                // procedural real do jogo.
                if (!Engine.IsEditorHint())
                {
                    return;
                }

                // Adiado - durante o carregamento da cena, essa propriedade pode ser aplicada
                // ANTES dos filhos (Base/Bordercap/Texture) existirem ainda; CallDeferred roda
                // depois que a arvore de nos termina de montar.
                CallDeferred(nameof(ApplyGenerateState), value);
            }
        }

        [Export]
        public bool BackupCaptured { get; set; }

        [Export]
        public byte[] BaseBackup { get; set; } = System.Array.Empty<byte>();

        [Export]
        public byte[] BordercapBackup { get; set; } = System.Array.Empty<byte>();

        [Export]
        public byte[] TextureBackup { get; set; } = System.Array.Empty<byte>();

        private void ApplyGenerateState(bool generate)
        {
            var baseLayer = GetNodeOrNull<TerrainLayer>(ChunkStreamingConstants.PROCEDURAL_BASE_LAYER_NAME);
            var borderCapLayer = GetNodeOrNull<TerrainLayer>(ChunkStreamingConstants.PROCEDURAL_BORDER_CAP_LAYER_NAME);
            var textureLayer = GetNodeOrNull<TerrainLayer>(ChunkStreamingConstants.PROCEDURAL_LAYER_NAME);

            if (textureLayer == null || baseLayer == null)
            {
                GD.PrintErr("[UpsidedownLevel] Nao achei as layers Base/Texture - abortando.");

                return;
            }

            if (generate)
            {
                GenerateInEditor(baseLayer, borderCapLayer, textureLayer);
            }
            else
            {
                RestoreOriginal(baseLayer, borderCapLayer, textureLayer);
            }
        }

        private void GenerateInEditor(TerrainLayer baseLayer, TerrainLayer borderCapLayer, TerrainLayer textureLayer)
        {
            // So guarda o desenho original NA PRIMEIRA VEZ - gerar de novo com outra seed nao
            // deve sobrescrever o backup com o resultado da geracao anterior.
            if (!BackupCaptured)
            {
                BaseBackup = baseLayer.Get(TileMapDataProperty).AsByteArray();
                BordercapBackup = borderCapLayer != null ? borderCapLayer.Get(TileMapDataProperty).AsByteArray() : System.Array.Empty<byte>();
                TextureBackup = textureLayer.Get(TileMapDataProperty).AsByteArray();
                BackupCaptured = true;
            }

            baseLayer.Clear();
            borderCapLayer?.Clear();
            textureLayer.Clear();

            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var widthChunks = Mathf.CeilToInt(GeneratedWidthTiles / (float)chunkSize);
            var heightChunks = Mathf.CeilToInt(GeneratedHeightTiles / (float)chunkSize);
            var minChunkX = -widthChunks / 2;
            var minChunkY = -heightChunks / 2;

            for (int cx = minChunkX; cx < minChunkX + widthChunks; cx++)
            {
                for (int cy = minChunkY; cy < minChunkY + heightChunks; cy++)
                {
                    // Mesmo ChunkGenerator.Paint que ChunkStreamingManager.LoadChunkAsync chama
                    // (via PaintAsync, so que cedendo frame) pra gerar chunk de verdade em jogo -
                    // aqui roda tudo de uma vez soh porque nao precisa ceder frame num editor
                    // tool rodando fora do loop de jogo.
                    ChunkGenerator.Paint(textureLayer, borderCapLayer, baseLayer, PreviewSeed, ChunkStreamingConstants.UPSIDEDOWN_ID, new Vector2I(cx, cy), chunkSize);
                }
            }

            GD.Print($"[UpsidedownLevel] Terreno de preview gerado ({GeneratedWidthTiles}x{GeneratedHeightTiles} tiles, seed={PreviewSeed}).");
        }

        private void RestoreOriginal(TerrainLayer baseLayer, TerrainLayer borderCapLayer, TerrainLayer textureLayer)
        {
            if (!BackupCaptured)
            {
                return;
            }

            baseLayer.Set(TileMapDataProperty, BaseBackup);
            borderCapLayer?.Set(TileMapDataProperty, BordercapBackup);
            textureLayer.Set(TileMapDataProperty, TextureBackup);

            GD.Print("[UpsidedownLevel] Tiles originais do projeto restaurados.");
        }
    }
}
