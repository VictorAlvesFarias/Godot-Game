using Godot;
using Jogo25D.Core;

namespace Jogo25D.UI
{
    public partial class LoadingUI : ScreenUI
    {
        #region Dinamic properties

        public float DotsTimer { get; set; }
        public int DotsCount { get; set; }

        #endregion

        #region Godot implementation

        public override bool IsOverlay => true;

        public override void _Ready()
        {
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

            Game.Ui.LoadingUI.StatusLabel.Node.Text = "Carregando" + new string('.', DotsCount);
        }

        #endregion

        #region Public API

        public void Open()
        {
            DotsTimer = 0f;
            DotsCount = 0;

            Game.Ui.LoadingUI.StatusLabel.Node.Text = "Carregando";

            Game.Managers.RouterManager.Node.Open(this);
        }

        public void Close()
        {
            Game.Managers.RouterManager.Node.Close(this);
        }

        #endregion
    }
}
