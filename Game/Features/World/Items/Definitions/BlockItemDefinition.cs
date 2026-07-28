using Godot;
using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items
{
    // Item de bloco colocavel - "Use" (segurar o botao de atacar) mira a
    // celula sob o mouse e pede pro servidor colocar o bloco ali (throtled
    // pelo Cooldown normal de ItemDefinition, igual uma arma). BlockId
    // referencia BlockDB pra saber qual tile pintar e o que consumir.
    public class BlockItemDefinition : ItemDefinition
    {
        public string BlockId { get; init; }
        public float Reach { get; init; } = 120f;

        public override void Use(Player player, ItemDefinitionData instance)
        {
            if (instance == null || instance.Quantity <= 0 || !CanUse(instance))
            {
                return;
            }

            if (!player.IsOwner())
            {
                return;
            }

            var layer = player.GetActiveTileLayer();

            if (layer == null)
            {
                return;
            }

            // Colocar e sempre livre - so limitado pelo Reach, sem se
            // importar com o que esta no meio do caminho (diferente de
            // quebrar, que pode ser restrito via "toggle_mining_mode").
            var targetCell = player.ResolveCellInRange(layer, Reach);

            TriggerCooldownTimer(instance);

            player.PlaceBlockRequest(targetCell, instance.InstanceId);
        }
    }
}
