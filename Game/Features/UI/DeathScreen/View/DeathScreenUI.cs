using Godot;
using Jogo25D.Characters;
using Jogo25D.Core;

namespace Jogo25D.UI
{
    public partial class DeathScreenUI : ScreenUI
    {
        #region Dinamic properties

        public Player LocalPlayer { get; set; }

        #endregion

        #region Godot implementation

        public override bool IsOverlay => true;

		public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;

            Game.WhenReady(Initialize);
        }

        public override void _Process(double delta)
        {
            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                LocalPlayer = Game.Managers.WorldManager.Node?.GetLocalPlayer();

                return;
            }

            var isDead = LocalPlayer.Data.CurrentHealth <= 0
                && LocalPlayer.Sprite != null
                && LocalPlayer.Sprite.Animation == "dead"
                && !LocalPlayer.Sprite.IsPlaying();

            if (isDead)
            {
                Game.Managers.RouterManager.Node.Open(this);
            }
            else
            {
                Game.Managers.RouterManager.Node.Close(this);
            }
        }

        #endregion

        #region Core - Setup

        private void Initialize()
        {
            Game.Ui.DeathScreenUI.ReviveButton.Node.Pressed += OnRevivePressed;

            LocalPlayer = Game.Managers.WorldManager.Node?.GetLocalPlayer();
        }

        #endregion

        #region Core - Actions

        public void OnRevivePressed()
        {
            Game.Managers.WorldManager.Node.GetLocalPlayer()?.TeleportClientRequest(Vector2.Zero);
        }

        #endregion
    }
}
