using Godot;
using Jogo25D.Characters;
using Jogo25D.Core;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class DeathScreenUI : ScreenUI
    {
        #region Node children references

        public Panel Background { get; private set; }
        public Button ReviveButton { get; private set; }

        #endregion

        #region Dinamic properties

        public Player LocalPlayer { get; set; }

        #endregion

        #region Godot implementation

        public override bool IsOverlay => true;

        public override void _Ready()
        {
            ResolveChildren();
            ProcessMode = ProcessModeEnum.Always;

            Initialize();
        }

        public override void _Process(double delta)
        {
            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                LocalPlayer = Players.GetLocal();

                return;
            }

            var isDead = LocalPlayer.CurrentHealth <= 0
                && LocalPlayer.Sprite != null
                && LocalPlayer.Sprite.Animation == "dead"
                && !LocalPlayer.Sprite.IsPlaying();

            if (isDead)
            {
                RouterContext.Open(this);
            }
            else
            {
                RouterContext.Close(this);
            }
        }

        #endregion

        #region Core - Setup

        private void ResolveChildren()
        {
            Background = GetNode<Panel>("Background");
            ReviveButton = GetNode<Button>("Background/CenterContainer/Panel/MarginContainer/Root/ReviveButton");
        }

        private void Initialize()
        {
            ReviveButton.Pressed += OnRevivePressed;

            LocalPlayer = Players.GetLocal();
        }

        #endregion

        #region UI - Events

        public void OnRevivePressed()
        {
            Players.GetLocal()?.TeleportClientRequest(Vector2.Zero);
        }

        #endregion
    }
}
