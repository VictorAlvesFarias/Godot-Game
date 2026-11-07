using Godot;
using Jogo25D.Core;
using Jogo25D.Session;

namespace Jogo25D.UI
{
    public partial class LoadingUI : ScreenUI
    {
        #region Node children references

        public Label StatusLabel { get; private set; }

        #endregion

        #region Dinamic properties

        public float DotsTimer { get; set; }
        public int DotsCount { get; set; }

        #endregion

        #region Godot implementation

        public override bool IsOverlay => true;

        public override void _Ready()
        {
            ResolveChildren();

            SessionContext.LoadingStarted += Open;
            SessionContext.LoadingFinished += Close;
        }

        public override void _ExitTree()
        {
            SessionContext.LoadingStarted -= Open;
            SessionContext.LoadingFinished -= Close;
        }

        private void ResolveChildren()
        {
            StatusLabel = GetNode<Label>("Background/CenterContainer/Column/StatusLabel");
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

        #endregion

        #region Public API

        public void Open()
        {
            DotsTimer = 0f;
            DotsCount = 0;

            StatusLabel.Text = "Carregando";

            RouterContext.Open(this);
        }

        public void Close()
        {
            RouterContext.Close(this);
        }

        #endregion
    }
}
