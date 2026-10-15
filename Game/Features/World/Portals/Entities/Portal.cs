using Godot;
using Jogo25D.Characters;
using Jogo25D.Props;
using Jogo25D.Save;

namespace Jogo25D.Portals
{
    [SaveScene("portal", "res://Scenes/World/Props/Portal.tscn")]
    public partial class Portal : Prop
    {
        #region Dinamic properties

        private const ulong TradeCooldownMsec = 1500;

        public ulong CooldownUntilMsec { get; set; }

        public override bool Interactable => true;

        public override string InteractPrompt => "Pressione [E] para viajar";

        #endregion

        #region Core - Trade

        protected override void Interact(Player player)
        {
            var now = Time.GetTicksMsec();

            if (now < CooldownUntilMsec || now - player.LastDimensionTradeMsec < TradeCooldownMsec)
            {
                return;
            }

            CooldownUntilMsec = now + TradeCooldownMsec;

            GetTree().CreateTimer(0.0).Timeout += RequestTrade;
        }

        private void RequestTrade()
        {
            OverlappingLocalPlayer?.TradeDimensionClientRequest();
        }

        #endregion
    }
}
