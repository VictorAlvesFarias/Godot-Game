using Godot;

namespace Jogo25D.Core
{
    public static class GameLoop
    {
        #region Dinamic properties

        public static SceneTree Tree => Engine.GetMainLoop() as SceneTree;

        public static MultiplayerApi Multiplayer => Tree?.GetMultiplayer();

        #endregion
    }
}
