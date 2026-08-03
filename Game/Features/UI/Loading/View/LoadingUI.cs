using Godot;

namespace Jogo25D.UI
{
    public partial class LoadingUI : CanvasLayer
    {
        public Label StatusLabel { get; set; }

        public float DotsTimer { get; set; }
        public int DotsCount { get; set; }

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

            DotsTimer += (float)delta;

            if (DotsTimer < 0.4f)
            {
                return;
            }

            DotsTimer = 0f;
            DotsCount = (DotsCount + 1) % 4;

            StatusLabel.Text = "Carregando" + new string('.', DotsCount);
        }

        public void Open()
        {
            DotsTimer = 0f;
            DotsCount = 0;
            StatusLabel.Text = "Carregando";
            Visible = true;
        }

        public void Close()
        {
            Visible = false;
        }
    }
}
