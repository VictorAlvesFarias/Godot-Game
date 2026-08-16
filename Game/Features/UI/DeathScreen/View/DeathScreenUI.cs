using Godot;
using Jogo25D.Characters;
using Jogo25D.Core;

namespace Jogo25D.UI
{
    public partial class DeathScreenUI : CanvasLayer
    {
        #region Dinamic properties

        public Player LocalPlayer { get; set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            Layer = 25;
            ProcessMode = ProcessModeEnum.Always;
            Visible = false;

            Game.WhenReady(Initialize);
        }

        public override void _Process(double delta)
        {
            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                FindLocalPlayer();

                return;
            }

            Visible = LocalPlayer.Data.CurrentHealth <= 0
                && LocalPlayer.Sprite != null
                && LocalPlayer.Sprite.Animation == "dead"
                && !LocalPlayer.Sprite.IsPlaying();
        }

        #endregion

        #region Core - Setup

        private void Initialize()
        {
            Game.Ui.DeathScreenUI.ReviveButton.Node.Pressed += OnRevivePressed;

            FindLocalPlayer();
        }

        public void FindLocalPlayer()
        {
            LocalPlayer = Game.Managers.WorldManager.Node.GetLocalPlayer();
        }

        #endregion

        #region Core - Actions

        public void OnRevivePressed()
        {
            Game.Managers.WorldManager.Node.TeleportPlayerClientRequest(Vector2.Zero);
        }

        #endregion
    }
}
