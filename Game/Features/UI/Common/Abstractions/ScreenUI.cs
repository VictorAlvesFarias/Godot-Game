using Godot;

namespace Jogo25D.UI
{
    // Base de toda tela. A tela nao decide quando aparece: ela nasce escondida, responde se pode
    // abrir, e reage a ter sido aberta ou fechada. Quem troca Visible e o RouterManager - e so ele.
    public partial class ScreenUI : CanvasLayer
    {
        #region Dinamic properties

        // Overlay aparece por cima da tela atual em vez de substituir (Hud, Pause, Console...).
        // Tela exclusiva fecha a anterior e entra na pilha do router.
        public virtual bool IsOverlay => false;

        #endregion

        #region Core - Contrato de tela

        // Chamado pelo router antes de abrir. Retornar false cancela a abertura - e aqui que a
        // tela exige o que precisa pra existir (mundo escolhido, personagem carregado, etc.).
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
