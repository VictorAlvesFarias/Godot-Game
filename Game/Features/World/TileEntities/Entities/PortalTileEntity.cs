using Godot;
using Jogo25D.Characters;
using Jogo25D.Systems;

namespace Jogo25D.TileEntities
{
    // Substitui o antigo WorldPortal.cs/WorldPortal.tscn - mesma logica
    // (cooldown, periodo de graca pos-troca via Player.LastDimensionTradeMsec,
    // deferir a chamada da RPC pra fora do frame de deteccao), so que o
    // gatilho agora vem do TileEntityManager (celula marcada no TileMap),
    // nao de uma Area2D avulsa. Teleporta so quando o player aperta E
    // (OnPlayerInteract), nao so por pisar na celula (OnPlayerEnter).
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
            // O visual do portal e o proprio tile pintado no TileMap (o que
            // quem marcou a celula escolheu) - nao criamos nenhum sprite
            // extra por cima. So resolve a referencia do WorldManager, que
            // vai ser usada quando o player entrar na celula.
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

            // O portal do mundo de destino cai na mesma celula relativa do
            // portal de origem, entao o player chega pousando bem em cima
            // dele - sem isso ele voltaria na hora.
            if (now - player.LastDimensionTradeMsec < (ulong)(CooldownSeconds * 1000))
            {
                return;
            }

            _cooldownUntilMsec = now + (ulong)(CooldownSeconds * 1000);

            // TradeDimension (RPC) reparenta o player, o que nao pode
            // acontecer durante o flush de queries de fisica que originou
            // esta deteccao. Um SceneTreeTimer de 0s empurra a chamada pro
            // proximo frame ocioso - equivalente ao CallDeferred usado no
            // WorldPortal antigo, mas funciona a partir de uma classe que
            // nao e Node (TileEntity nao herda Node de proposito).
            World.GetTree().CreateTimer(0.0).Timeout += RequestTrade;
        }

        private void RequestTrade()
        {
            _worldManager?.TradeDimensionClientRequest();
        }
    }
}
