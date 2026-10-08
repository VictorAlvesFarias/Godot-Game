using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Utils.Window
{
    public static class WindowUtilities
    {
        #region Core - Cursor

        public static void ApplyCursor(bool inGame)
        {
            var path = inGame ? UiConstants.CROSSHAIR_PATH : UiConstants.CURSOR_PATH;
            var texture = GD.Load<Texture2D>(path);

            if (texture == null)
            {
                GD.PushError($"[WindowUtilities] cursor nao encontrado em {path}");

                return;
            }

            var hotspot = inGame ? new Vector2(texture.GetWidth() / 2f, texture.GetHeight() / 2f) : Vector2.Zero;

            Input.SetCustomMouseCursor(texture, Input.CursorShape.Arrow, hotspot);

            ApplyPointer();
        }

        public static void ApplyPointer()
        {
            var texture = GD.Load<Texture2D>(UiConstants.POINTER_PATH);

            if (texture == null)
            {
                GD.PushError($"[WindowUtilities] ponteiro nao encontrado em {UiConstants.POINTER_PATH}");

                return;
            }

            Input.SetCustomMouseCursor(texture, Input.CursorShape.PointingHand, new Vector2(UiConstants.POINTER_HOTSPOT_X, 0f));
        }

        #endregion

        #region Core - Fullscreen

        public static void ToggleFullscreen()
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
