using Godot;

namespace Jogo25D.UI
{
    public partial class WindowManager : Node
    {
        #region Godot implementation

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