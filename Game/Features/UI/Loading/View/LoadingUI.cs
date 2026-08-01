using Godot;

namespace Jogo25D.UI
{
    public partial class LoadingUI : CanvasLayer
    {
        private const string BaseText = "Carregando";
        private const float DotsIntervalSeconds = 0.4f;

        public Label StatusLabel { get; set; }

        private float _dotsTimer;
        private int _dotsCount;

        public override void _Ready()
        {
            Layer = 30;
            Visible = false;

            StatusLabel = GetNode<Label>("Background/CenterContainer/StatusLabel");
        }

        public override void _Process(double delta)
        {
            if (!Visible)
            {
                return;
            }

            _dotsTimer += (float)delta;

            if (_dotsTimer < DotsIntervalSeconds)
            {
                return;
            }

            _dotsTimer = 0f;
            _dotsCount = (_dotsCount + 1) % 4;

            StatusLabel.Text = BaseText + new string('.', _dotsCount);
        }

        public void Open()
        {
            _dotsTimer = 0f;
            _dotsCount = 0;
            StatusLabel.Text = BaseText;
            Visible = true;
        }

        public void Close()
        {
            Visible = false;
        }
    }
}
