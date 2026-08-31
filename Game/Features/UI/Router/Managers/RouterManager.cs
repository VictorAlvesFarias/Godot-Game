using Godot;
using Jogo25D.Core;
using System.Collections.Generic;

namespace Jogo25D.UI
{
    public partial class RouterManager : Node
    {
        #region Dinamic properties

        public ScreenUI Current { get; private set; }

        private readonly List<ScreenUI> _history = new();

        private readonly HashSet<ScreenUI> _overlays = new();

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

        private void Show(ScreenUI screen)
        {
            if (screen.Visible)
            {
                return;
            }

            screen.Visible = true;

            screen.OnOpened();

            if (screen.IsOverlay)
            {
                _overlays.Add(screen);
            }

            AtualizarCursor();
        }

        private void Hide(ScreenUI screen)
        {
            if (!screen.Visible)
            {
                return;
            }

            screen.Visible = false;

            screen.OnClosed();

            _overlays.Remove(screen);

            AtualizarCursor();
        }

        // Mira so quando o jogo esta na frente: com qualquer overlay aberto, volta a seta.
        private void AtualizarCursor()
        {
            var emJogo = Current is HudUI && _overlays.Count == 0;

            Game.Managers.WindowManager.Node?.AplicarCursor(emJogo);
        }

        #endregion
    }
}
