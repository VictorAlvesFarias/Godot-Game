using Godot;
using Jogo25D.Characters;
using Jogo25D.Systems;

namespace Jogo25D.Portals
{
    public partial class Portal : Area2D
    {
        private const float CooldownSeconds = 1.5f;
        private const string PromptText = "Pressione [E] para viajar";

        private WorldManager _worldManager;
        private Label _promptLabel;
        private ulong _cooldownUntilMsec;
        private Player _overlappingLocalPlayer;

        public override void _Ready()
        {
            _worldManager = GetTree().Root.GetNodeOrNull<WorldManager>(WorldManager.DEFAULT_NODE_PATH);
            _promptLabel = GetNodeOrNull<Label>("Labels/PromptLabel");

            BodyEntered += OnBodyEntered;
            BodyExited += OnBodyExited;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_overlappingLocalPlayer == null || _overlappingLocalPlayer.Input == null || !_overlappingLocalPlayer.Input.Interact)
            {
                return;
            }

            var now = Time.GetTicksMsec();

            if (now < _cooldownUntilMsec)
            {
                return;
            }

            if (now - _overlappingLocalPlayer.LastDimensionTradeMsec < (ulong)(CooldownSeconds * 1000))
            {
                return;
            }

            _cooldownUntilMsec = now + (ulong)(CooldownSeconds * 1000);

            GetTree().CreateTimer(0.0).Timeout += RequestTrade;
        }

        private void RequestTrade()
        {
            _worldManager?.TradeDimensionClientRequest();
        }

        private void OnBodyEntered(Node2D body)
        {
            if (body is not Player player || !player.IsOwner())
            {
                return;
            }

            _overlappingLocalPlayer = player;

            if (_promptLabel != null)
            {
                _promptLabel.Text = PromptText;
                _promptLabel.Visible = true;
            }
        }

        private void OnBodyExited(Node2D body)
        {
            if (body is not Player player || player != _overlappingLocalPlayer)
            {
                return;
            }

            if (_promptLabel != null)
            {
                _promptLabel.Visible = false;
            }

            _overlappingLocalPlayer = null;
        }
    }
}
