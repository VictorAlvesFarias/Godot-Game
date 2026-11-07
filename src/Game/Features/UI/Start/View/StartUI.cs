using Godot;
using Jogo25D.Core;
using Jogo25D.Session;

namespace Jogo25D.UI
{
    public partial class StartUI : ScreenUI
    {
        #region Node children references

        public Button PlayButton { get; private set; }
        public Button ExitButton { get; private set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            ResolveChildren();
            Initialize();

            SessionContext.Bind();

            RouterContext.Open(this);
        }

        #endregion

        #region Core - Setup

        private void ResolveChildren()
        {
            PlayButton = GetNode<Button>("MarginContainer/Root/MenuColumn/PlayButton");
            ExitButton = GetNode<Button>("MarginContainer/Root/MenuColumn/ExitButton");
        }

        private void Initialize()
        {
            SessionContext.SessionEnded += OnSessionEnded;
            PlayButton.Pressed += OnPlayPressed;
            ExitButton.Pressed += OnExitPressed;

            BindHoverMarker(PlayButton);
            BindHoverMarker(ExitButton);
        }

        private void BindHoverMarker(Button button)
        {
            var marker = button?.GetNodeOrNull<Control>("HoverMarker");

            if (marker == null)
            {
                return;
            }

            marker.Visible = false;

            button.MouseEntered += () => marker.Visible = true;
            button.MouseExited += () => marker.Visible = false;
        }

        #endregion

        #region Managers - Events

        private void OnSessionEnded()
        {
            GetTree().Paused = false;

            RouterContext.Close(Ui.Get<HudUI>());
            RouterContext.Reset();
            RouterContext.Replace(this);
        }

        #endregion

        #region UI - Events

        public void OnPlayPressed()
        {
            RouterContext.Open(Ui.Get<WorldSelectUI>());
        }

        public void OnExitPressed()
        {
            GetTree().Quit();
        }

        #endregion
    }
}
