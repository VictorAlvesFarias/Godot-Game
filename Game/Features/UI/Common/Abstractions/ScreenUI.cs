using Godot;

namespace Jogo25D.UI
{
    public partial class ScreenUI : CanvasLayer
    {
        #region Dinamic properties

        public virtual bool IsOverlay => false;

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
