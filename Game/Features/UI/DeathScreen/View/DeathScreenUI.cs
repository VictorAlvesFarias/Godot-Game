using Godot;
using Jogo25D.Characters;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class DeathScreenUI : CanvasLayer
    {
        #region Properties

        public Player LocalPlayer { get; set; }
        public WorldManager NetworkManager { get; set; }

        public Panel Background { get; set; }
        public Button ReviveButton { get; set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            // Acima de Inventory/SkillTree (10/20) - a tela de morte deve
            // dominar mesmo se outra UI estiver aberta quando o player morre.
            Layer = 25;
            ProcessMode = ProcessModeEnum.Always;
            Visible = false;

            Background = GetNode<Panel>("Background");
            ReviveButton = GetNode<Button>("Background/CenterContainer/Panel/MarginContainer/Root/ReviveButton");

            ReviveButton.Pressed += OnRevivePressed;

            CallDeferred(nameof(FindLocalPlayer));
        }

        public override void _Process(double delta)
        {
            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                FindLocalPlayer();

                return;
            }

            // So aparece quando a animacao "dead" realmente terminou de
            // tocar (nao loop, para sozinha no ultimo frame) - nao junto com
            // a vida chegando a zero, pra nao cobrir a animacao de morte.
            Visible = LocalPlayer.Data.CurrentHealth <= 0
                && LocalPlayer.Sprite != null
                && LocalPlayer.Sprite.Animation == "dead"
                && !LocalPlayer.Sprite.IsPlaying();
        }

        #endregion

        #region Core - Setup

        public void FindLocalPlayer()
        {
            NetworkManager = GetTree().Root.GetNodeOrNull<WorldManager>(WorldManager.DEFAULT_NODE_PATH);
            LocalPlayer = NetworkManager?.GetLocalPlayer();
        }

        #endregion

        #region Core - Actions

        public void OnRevivePressed()
        {
            NetworkManager?.ResetPlayerClientRequest();
        }

        #endregion
    }
}
