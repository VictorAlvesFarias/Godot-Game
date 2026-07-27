using Godot;
using Jogo25D.Characters;
using Jogo25D.Systems;

namespace Jogo25D.TileEntities
{
    public class PortalTileEntity : TileEntity
    {
        private const float CooldownSeconds = 1.5f;

        private WorldManager _worldManager;
        private ulong _cooldownUntilMsec;

        public PortalTileEntity(Vector2I cell, Node2D world, Vector2 cellPosition)
            : base("portal", cell, world, cellPosition)
        {
        }

        public override string InteractPrompt => "Pressione [E] para viajar";

        public override void OnReady()
        {
            _worldManager = World.GetTree().Root.GetNodeOrNull<WorldManager>(WorldManager.DEFAULT_NODE_PATH);
        }

        public override void OnPlayerInteract(Player player)
        {
            if (!player.IsOwner())
            {
                return;
            }

            var now = Time.GetTicksMsec();

            if (now < _cooldownUntilMsec)
            {
                return;
            }

            if (now - player.LastDimensionTradeMsec < (ulong)(CooldownSeconds * 1000))
            {
                return;
            }

            _cooldownUntilMsec = now + (ulong)(CooldownSeconds * 1000);

            World.GetTree().CreateTimer(0.0).Timeout += RequestTrade;
        }

        private void RequestTrade()
        {
            _worldManager?.TradeDimensionClientRequest();
        }
    }
}
