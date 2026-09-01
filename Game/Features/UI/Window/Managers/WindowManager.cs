using Godot;
using Jogo25D.Constants;

namespace Jogo25D.UI
{
    public partial class WindowManager : Node
    {
        #region Godot implementation

        public override void _Ready()
        {
            AplicarCursor(emJogo: false);
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
            {
                if (keyEvent.Keycode == Key.F11)
                {
                    ToggleFullscreen();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        #endregion

        #region Core - Cursor

        // Tres ponteiros: a seta nas telas, a mira em jogo e a mao sobre o que e clicavel.
        // O hotspot da seta e a ponta, no canto de cima a esquerda; o da mira e o centro.
        public void AplicarCursor(bool emJogo)
        {
            var caminho = emJogo ? UiConstants.CROSSHAIR_PATH : UiConstants.CURSOR_PATH;
            var textura = GD.Load<Texture2D>(caminho);

            if (textura == null)
            {
                GD.PushError($"[WindowManager] cursor nao encontrado em {caminho}");

                return;
            }

            var hotspot = emJogo
                ? new Vector2(textura.GetWidth() / 2f, textura.GetHeight() / 2f)
                : Vector2.Zero;

            Input.SetCustomMouseCursor(textura, Input.CursorShape.Arrow, hotspot);

            AplicarPonteiro();
        }


        // A mao independe de estar em jogo ou numa tela: quem a escolhe e o proprio Control,
        // pelo mouse_default_cursor_shape.
        public void AplicarPonteiro()
        {
            var textura = GD.Load<Texture2D>(UiConstants.POINTER_PATH);

            if (textura == null)
            {
                GD.PushError($"[WindowManager] ponteiro nao encontrado em {UiConstants.POINTER_PATH}");

                return;
            }

            Input.SetCustomMouseCursor(textura, Input.CursorShape.PointingHand, new Vector2(UiConstants.POINTER_HOTSPOT_X, 0f));
        }

        #endregion

        #region Core - Fullscreen

        public void ToggleFullscreen()
        {
            var currentMode = DisplayServer.WindowGetMode();

            if (currentMode == DisplayServer.WindowMode.Fullscreen || currentMode == DisplayServer.WindowMode.ExclusiveFullscreen)
            {
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            }
            else
            {
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
            }
        }

        #endregion
    }
}