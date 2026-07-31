using Godot;

namespace Jogo25D.Chunks
{
    public partial class ChunkGridOverlay : Node2D
    {
        private const float ChunkPixels = ChunkStreamingManager.ChunkSize * 32f;
        private const float DrawRadius = 4000f;

        private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.2f);
        private static readonly Color CurrentChunkColor = new Color(0.3f, 1f, 1f, 0.6f);

        private static bool _enabled;

        public override void _Ready()
        {
            Visible = _enabled;
            ZIndex = 100;

            GD.Print($"[ChunkGridOverlay] ready, parent={GetParent()?.Name}, globalPos={GlobalPosition}");
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.PhysicalKeycode == Key.F3)
            {
                GD.Print($"[ChunkGridOverlay] raw F3 keydown detected, echo={keyEvent.Echo}, actionPressed={@event.IsActionPressed("toggle_chunk_grid")}");
            }

            if (@event.IsActionPressed("toggle_chunk_grid") && !@event.IsEcho())
            {
                _enabled = !_enabled;
                Visible = _enabled;

                GD.Print($"[ChunkGridOverlay] toggled, enabled={_enabled}, visible={Visible}");

                GetViewport().SetInputAsHandled();
            }
        }

        public override void _Process(double delta)
        {
            if (Visible != _enabled)
            {
                Visible = _enabled;
            }

            if (Visible)
            {
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            var center = GlobalPosition;

            GD.Print($"[ChunkGridOverlay] drawing, center={center}, currentChunk=({Mathf.FloorToInt(center.X / ChunkPixels)},{Mathf.FloorToInt(center.Y / ChunkPixels)})");

            var startChunkX = Mathf.FloorToInt((center.X - DrawRadius) / ChunkPixels);
            var endChunkX = Mathf.CeilToInt((center.X + DrawRadius) / ChunkPixels);
            var startChunkY = Mathf.FloorToInt((center.Y - DrawRadius) / ChunkPixels);
            var endChunkY = Mathf.CeilToInt((center.Y + DrawRadius) / ChunkPixels);

            var top = startChunkY * ChunkPixels - center.Y;
            var bottom = endChunkY * ChunkPixels - center.Y;
            var left = startChunkX * ChunkPixels - center.X;
            var right = endChunkX * ChunkPixels - center.X;

            for (var chunkX = startChunkX; chunkX <= endChunkX; chunkX++)
            {
                var x = chunkX * ChunkPixels - center.X;

                DrawLine(new Vector2(x, top), new Vector2(x, bottom), LineColor, 1f);
            }

            for (var chunkY = startChunkY; chunkY <= endChunkY; chunkY++)
            {
                var y = chunkY * ChunkPixels - center.Y;

                DrawLine(new Vector2(left, y), new Vector2(right, y), LineColor, 1f);
            }

            var currentChunkX = Mathf.FloorToInt(center.X / ChunkPixels);
            var currentChunkY = Mathf.FloorToInt(center.Y / ChunkPixels);

            var chunkLeft = currentChunkX * ChunkPixels - center.X;
            var chunkTop = currentChunkY * ChunkPixels - center.Y;

            DrawRect(new Rect2(chunkLeft, chunkTop, ChunkPixels, ChunkPixels), CurrentChunkColor, false, 2f);

            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(chunkLeft + 8f, chunkTop + 20f),
                $"chunk ({currentChunkX}, {currentChunkY})",
                HorizontalAlignment.Left,
                -1f,
                16,
                CurrentChunkColor);
        }
    }
}
