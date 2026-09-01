using Godot;

namespace Jogo25D.UI
{
    public enum TriangleDirection
    {
        Down,
        Up,
        Left,
        Right
    }

    [Tool]
    public partial class PixelTriangleRect : Control
    {
        [Export] public Color FillColor { get; set; } = new Color(0.93f, 0.72f, 0.31f);
        [Export] public Color BorderColor { get; set; } = new Color(0.35f, 0.25f, 0.09f);

        [Export]
        public TriangleDirection Direction
        {
            get => _direction;
            set
            {
                _direction = value;

                QueueRedraw();
            }
        }

        // Pixels que cada linha perde de cada lado. 1 da a diagonal de 45 graus do resto da interface.
        [Export] public int Step { get; set; } = 1;
        [Export] public int BorderWidth { get; set; } = 2;

        private TriangleDirection _direction = TriangleDirection.Down;

        public bool IsHorizontal => _direction == TriangleDirection.Left || _direction == TriangleDirection.Right;

        public override void _Draw()
        {
            DrawTriangle(0f, 0f, Size.X, Size.Y, BorderColor);

            // A borda some por dentro recuando duas colunas de base e uma de profundidade por pixel
            // de espessura, que e o que mantem a espessura constante numa diagonal de 45 graus.
            // O recuo na profundidade vale para os dois lados: sem ele o bico do triangulo interno
            // nasce no mesmo pixel do externo e a ponta fica sem contorno.
            var b = BorderWidth;

            if (IsHorizontal)
            {
                DrawTriangle(b, b * 2, Size.X - b * 2, Size.Y - b * 4, FillColor);
            }
            else
            {
                DrawTriangle(b * 2, b, Size.X - b * 4, Size.Y - b * 2, FillColor);
            }
        }

        private void DrawTriangle(float x, float y, float width, float height, Color color)
        {
            if (width <= 0f || height <= 0f || Step <= 0)
            {
                return;
            }

            // A base e o lado oposto ao bico; a profundidade e quantas linhas cabem ate ele fechar.
            var baseSize = IsHorizontal ? height : width;
            var rows = (int)(baseSize / (Step * 2));

            for (int i = 0; i < rows; i++)
            {
                var start = i * Step;
                var length = baseSize - i * Step * 2;

                // Onde a linha cai na profundidade: crescendo a partir da base, ou a partir do fim.
                var apexFirst = _direction == TriangleDirection.Down || _direction == TriangleDirection.Right;
                var depth = apexFirst ? i : rows - 1 - i;

                if (IsHorizontal)
                {
                    DrawRect(new Rect2(x + depth, y + start, 1f, length), color);
                }
                else
                {
                    DrawRect(new Rect2(x + start, y + depth, length, 1f), color);
                }
            }
        }
    }
}
