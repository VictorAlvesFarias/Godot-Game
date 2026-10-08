using Godot;

namespace Jogo25D.UI
{
    [Tool]
    public partial class PixelSpinner : Control
    {
        [Export] public Color DotColor { get; set; } = new Color(0.93f, 0.72f, 0.31f);
        [Export] public int Dots { get; set; } = 8;
        [Export] public int DotSize { get; set; } = 6;
        [Export] public float Radius { get; set; } = 20f;
        [Export] public float StepsPerSecond { get; set; } = 10f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float TrailAlpha { get; set; } = 0.15f;

        private float _elapsed;
        private int _head;

        public override void _Process(double delta)
        {
            if (StepsPerSecond <= 0f || Dots <= 0)
            {
                return;
            }

            _elapsed += (float)delta;

            var step = 1f / StepsPerSecond;

            if (_elapsed < step)
            {
                return;
            }

            while (_elapsed >= step)
            {
                _elapsed -= step;
                _head = (_head + 1) % Dots;
            }

            QueueRedraw();
        }

        public override void _Draw()
        {
            if (Dots <= 0 || DotSize <= 0)
            {
                return;
            }

            var center = Size / 2f;

            for (int i = 0; i < Dots; i++)
            {
                var angle = Mathf.Tau * i / Dots - Mathf.Tau / 4f;
                var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius;
                var behind = (_head - i + Dots) % Dots;
                var strength = Dots > 1 ? 1f - (float)behind / (Dots - 1) : 1f;
                var color = DotColor;

                color.A = Mathf.Lerp(TrailAlpha, 1f, strength);

                var corner = new Vector2(
                    Mathf.Round(point.X - DotSize / 2f),
                    Mathf.Round(point.Y - DotSize / 2f)
                );

                DrawRect(new Rect2(corner, new Vector2(DotSize, DotSize)), color);
            }
        }
    }
}
