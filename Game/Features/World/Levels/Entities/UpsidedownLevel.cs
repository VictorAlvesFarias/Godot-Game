using Godot;
using Jogo25D.Biomes;
using Jogo25D.Chunks;
using Jogo25D.Constants;

namespace Jogo25D.Levels
{

    [Tool]
    public partial class UpsidedownLevel : Node2D
    {
        private const int GeneratedWidthTiles = 1000;
        private const int GeneratedHeightTiles = 500;
        private const string TileMapDataProperty = "tile_map_data";

        [Export]
        public long PreviewSeed { get; set; } = 1;

        private bool _generateEditorTerrain;
        private bool _isReady;

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

                if (!Engine.IsEditorHint() || !_isReady)
                {
                    return;
                }

                CallDeferred(nameof(ApplyGenerateState), value);
            }
        }

        [Export]
        public bool BackupCaptured { get; set; }

        [Export]
        public byte[] BaseBackup { get; set; } = System.Array.Empty<byte>();

        [Export]
        public byte[] ComposeBackup { get; set; } = System.Array.Empty<byte>();

        [Export]
        public byte[] BordercapBackup { get; set; } = System.Array.Empty<byte>();

        [Export]
        public byte[] TextureBackup { get; set; } = System.Array.Empty<byte>();

        public override void _Ready()
        {
            _isReady = true;
        }

        private void ApplyGenerateState(bool generate)
        {
            var baseLayer = GetNodeOrNull<TerrainLayer>(ChunkStreamingConstants.PROCEDURAL_BASE_LAYER_NAME);
            var composeLayer = GetNodeOrNull<TerrainLayer>(ChunkStreamingConstants.PROCEDURAL_LAYER_NAME);

            if (composeLayer == null || baseLayer == null)
            {
                GD.PrintErr("[UpsidedownLevel] Nao achei as layers Base/Compose - abortando.");

                return;
            }

            if (generate)
            {
                GenerateInEditor(baseLayer, composeLayer);
            }
            else
            {
                RestoreOriginal(baseLayer, composeLayer);
            }
        }

        private void GenerateInEditor(TerrainLayer baseLayer, TerrainLayer composeLayer)
        {

            if (!BackupCaptured)
            {
                BaseBackup = baseLayer.Get(TileMapDataProperty).AsByteArray();
                ComposeBackup = composeLayer.Get(TileMapDataProperty).AsByteArray();
                BackupCaptured = true;
            }

            baseLayer.Clear();
            composeLayer.Clear();

            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var widthChunks = Mathf.CeilToInt(GeneratedWidthTiles / (float)chunkSize);
            var heightChunks = Mathf.CeilToInt(GeneratedHeightTiles / (float)chunkSize);
            var minChunkX = -widthChunks / 2;
            var minChunkY = -heightChunks / 2;

            for (int cx = minChunkX; cx < minChunkX + widthChunks; cx++)
            {
                for (int cy = minChunkY; cy < minChunkY + heightChunks; cy++)
                {

                    ChunkGenerator.Paint(composeLayer, baseLayer, PreviewSeed, ChunkStreamingConstants.UPSIDEDOWN_ID, new Vector2I(cx, cy), chunkSize);
                }
            }

            GD.Print($"[UpsidedownLevel] Terreno de preview gerado ({GeneratedWidthTiles}x{GeneratedHeightTiles} tiles, seed={PreviewSeed}).");
        }

        private void RestoreOriginal(TerrainLayer baseLayer, TerrainLayer composeLayer)
        {
            if (!BackupCaptured)
            {
                return;
            }

            baseLayer.Set(TileMapDataProperty, BaseBackup);
            composeLayer.Set(TileMapDataProperty, ComposeBackup);

            GD.Print("[UpsidedownLevel] Tiles originais do projeto restaurados.");
        }
    }
}
