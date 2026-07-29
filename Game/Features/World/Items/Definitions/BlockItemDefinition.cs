using Godot;
using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items
{
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

            var targetCell = player.ResolveCellInRange(layer, Reach);

            TriggerCooldownTimer(instance);

            player.PlaceBlockRequest(targetCell, instance.InstanceId);
        }
    }
}
