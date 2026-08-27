using Godot;
using System.Collections.Generic;

namespace Jogo25D.UI
{
    // Dono de qual tela esta aberta - estado que nao pertence a nenhuma tela, por isso e manager.
    // Ninguem mais escreve Visible de tela: quem quer trocar de tela chama Open/Back/Close aqui.
    public partial class RouterManager : Node
    {
        #region Dinamic properties

        public ScreenUI Current { get; private set; }

        private readonly List<ScreenUI> _history = new();

        #endregion

        #region Core - Navegacao

        public bool Open(ScreenUI screen)
        {
            if (screen == null || !screen.CanOpen())
            {
                return false;
            }

            if (screen.IsOverlay)
            {
                Show(screen);

                return true;
            }

            if (Current == screen)
            {
                return true;
            }

            // Abrir a tela imediatamente anterior e voltar: consome o historico em vez de crescer.
            if (_history.Count > 0 && _history[^1] == screen)
            {
                _history.RemoveAt(_history.Count - 1);

                if (Current != null)
                {
                    Hide(Current);
                }

                Current = screen;

                Show(screen);

                return true;
            }

            if (Current != null)
            {
                _history.Add(Current);

                Hide(Current);
            }

            Current = screen;

            Show(screen);

            return true;
        }

        public bool Replace(ScreenUI screen)
        {
            _history.Clear();

            return Open(screen);
        }

        public void Close(ScreenUI screen)
        {
            if (screen == null || !screen.Visible)
            {
                return;
            }

            Hide(screen);

            if (Current == screen)
            {
                Current = null;
            }
        }

        public bool Back()
        {
            if (_history.Count == 0)
            {
                return false;
            }

            var previous = _history[^1];

            _history.RemoveAt(_history.Count - 1);

            if (Current != null)
            {
                Hide(Current);
            }

            Current = previous;

            Show(previous);

            return true;
        }

        #endregion

        #region Core - Visibilidade

        private static void Show(ScreenUI screen)
        {
            if (screen.Visible)
            {
                return;
            }

            screen.Visible = true;

            screen.OnOpened();
        }

        private static void Hide(ScreenUI screen)
        {
            if (!screen.Visible)
            {
                return;
            }

            screen.Visible = false;

            screen.OnClosed();
        }

        #endregion
    }
}
