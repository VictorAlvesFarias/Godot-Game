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

        // Passos por segundo. O giro avanca de casa em casa em vez de interpolar: rotacao
        // continua embaralha os pixels e destoa do resto da interface.
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

            var passo = 1f / StepsPerSecond;

            if (_elapsed < passo)
            {
                return;
            }

            while (_elapsed >= passo)
            {
                _elapsed -= passo;
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

            var centro = Size / 2f;

            for (int i = 0; i < Dots; i++)
            {
                // comeca no topo e gira no sentido horario
                var angulo = Mathf.Tau * i / Dots - Mathf.Tau / 4f;
                var ponto = centro + new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * Radius;

                var atras = (_head - i + Dots) % Dots;
                var forca = Dots > 1 ? 1f - (float)atras / (Dots - 1) : 1f;

                var cor = DotColor;
                cor.A = Mathf.Lerp(TrailAlpha, 1f, forca);

                // arredonda para inteiro: meio pixel borra o quadrado
                var canto = new Vector2(
                    Mathf.Round(ponto.X - DotSize / 2f),
                    Mathf.Round(ponto.Y - DotSize / 2f));

                DrawRect(new Rect2(canto, new Vector2(DotSize, DotSize)), cor);
            }
        }
    }
}
