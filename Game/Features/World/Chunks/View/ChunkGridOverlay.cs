using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Chunks
{
    public partial class ChunkGridOverlay : Node2D
    {
        public static bool Enabled { get; set; }

        private ChunkStreamingManager _chunkStreamingManager;
        private float _chunkPixels = ChunkStreamingConstants.CHUNK_SIZE * 32f;

        #region Godot implementation

        public override void _Ready()
        {
            Visible = Enabled;
            ZIndex = 100;

            _chunkStreamingManager = GetTree().Root.GetNodeOrNull<ChunkStreamingManager>(StaticNodePathsConstants.ChunkStreamingManager);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event.IsActionPressed("toggle_chunk_grid") && !@event.IsEcho())
            {
                Enabled = !Enabled;
                Visible = Enabled;

                GetViewport().SetInputAsHandled();
            }
        }

        public override void _Process(double delta)
        {
            if (Visible != Enabled)
            {
                Visible = Enabled;
            }

            if (Visible)
            {
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            var center = GlobalPosition;

            if (_chunkStreamingManager != null)
            {
                _chunkPixels = ChunkStreamingConstants.CHUNK_SIZE * _chunkStreamingManager.TileSize;
            }

            var ChunkPixels = _chunkPixels;

            var startChunkX = Mathf.FloorToInt((center.X - 4000f) / ChunkPixels);
            var endChunkX = Mathf.CeilToInt((center.X + 4000f) / ChunkPixels);
            var startChunkY = Mathf.FloorToInt((center.Y - 4000f) / ChunkPixels);
            var endChunkY = Mathf.CeilToInt((center.Y + 4000f) / ChunkPixels);

            var top = startChunkY * ChunkPixels - center.Y;
            var bottom = endChunkY * ChunkPixels - center.Y;
            var left = startChunkX * ChunkPixels - center.X;
            var right = endChunkX * ChunkPixels - center.X;

            for (var chunkX = startChunkX; chunkX <= endChunkX; chunkX++)
            {
                var x = chunkX * ChunkPixels - center.X;

                DrawLine(new Vector2(x, top), new Vector2(x, bottom), new Color(1f, 1f, 1f, 0.2f), 1f);
            }

            for (var chunkY = startChunkY; chunkY <= endChunkY; chunkY++)
            {
                var y = chunkY * ChunkPixels - center.Y;

                DrawLine(new Vector2(left, y), new Vector2(right, y), new Color(1f, 1f, 1f, 0.2f), 1f);
            }

            var currentChunkX = Mathf.FloorToInt(center.X / ChunkPixels);
            var currentChunkY = Mathf.FloorToInt(center.Y / ChunkPixels);

            var chunkLeft = currentChunkX * ChunkPixels - center.X;
            var chunkTop = currentChunkY * ChunkPixels - center.Y;

            DrawRect(new Rect2(chunkLeft, chunkTop, ChunkPixels, ChunkPixels), new Color(0.3f, 1f, 1f, 0.6f), false, 2f);

            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(chunkLeft + 8f, chunkTop + 20f),
                $"chunk ({currentChunkX}, {currentChunkY})",
                HorizontalAlignment.Left,
                -1f,
                16,
                new Color(0.3f, 1f, 1f, 0.6f));
        }

        #endregion
    }
}
