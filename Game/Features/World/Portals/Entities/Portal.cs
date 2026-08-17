using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Props;
using Jogo25D.Systems;

namespace Jogo25D.Portals
{
    public partial class Portal : Prop
    {
        #region Dinamic properties

        public ulong CooldownUntilMsec { get; set; }
        public Player OverlappingLocalPlayer { get; set; }

        #endregion

        #region Node references


        #endregion

        #region Node children references

        public Label PromptLabel { get; set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            PromptLabel = GetNodeOrNull<Label>("Labels/PromptLabel");

            BodyEntered += OnBodyEntered;
            BodyExited += OnBodyExited;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (OverlappingLocalPlayer == null || OverlappingLocalPlayer.Input == null || !OverlappingLocalPlayer.Input.Interact)
            {
                return;
            }

            var now = Time.GetTicksMsec();

            if (now < CooldownUntilMsec)
            {
                return;
            }

            if (now - OverlappingLocalPlayer.LastDimensionTradeMsec < (ulong)(1.5f * 1000))
            {
                return;
            }

            CooldownUntilMsec = now + (ulong)(1.5f * 1000);

            GetTree().CreateTimer(0.0).Timeout += RequestTrade;
        }

        #endregion

        #region Core - Trade

        private void RequestTrade()
        {
            OverlappingLocalPlayer?.TradeDimensionClientRequest();
        }

        private void OnBodyEntered(Node2D body)
        {
            if (body is not Player player || !player.IsOwner())
            {
                return;
            }

            OverlappingLocalPlayer = player;

            if (PromptLabel != null)
            {
                PromptLabel.Text = "Pressione [E] para viajar";
                PromptLabel.Visible = true;
            }
        }

        private void OnBodyExited(Node2D body)
        {
            if (body is not Player player || player != OverlappingLocalPlayer)
            {
                return;
            }

            if (PromptLabel != null)
            {
                PromptLabel.Visible = false;
            }

            OverlappingLocalPlayer = null;
        }

        #endregion
    }
}
