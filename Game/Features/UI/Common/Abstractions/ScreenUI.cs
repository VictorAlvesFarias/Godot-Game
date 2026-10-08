using Godot;
using Jogo25D.Utils.Window;

namespace Jogo25D.UI
{
    public partial class ScreenUI : CanvasLayer
    {
        #region Dinamic properties

        public virtual bool IsOverlay => false;

        #endregion

        #region Godot implementation

        public override void _EnterTree()
        {
            Ui.Register(this);
        }

        public override void _ExitTree()
        {
            Ui.Unregister(this);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is not InputEventKey key || !key.Pressed || key.Echo || key.Keycode != Key.F11)
            {
                return;
            }

            WindowUtilities.ToggleFullscreen();

            GetViewport().SetInputAsHandled();
        }

        #endregion

        #region Core - Contrato de tela

        public virtual bool CanOpen()
        {
            return true;
        }

        public virtual void OnOpened()
        {
        }

        public virtual void OnClosed()
        {
        }

        #endregion
    }
}
