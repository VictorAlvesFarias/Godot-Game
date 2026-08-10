using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items
{
    // Item de material puro (madeira, folha, etc.) - so existe pra empilhar no inventario e ser
    // usado em drops/crafting futuro, sem nenhum comportamento proprio ao "usar".
    public class MaterialItemDefinition : ItemDefinition
    {
        #region Core - Virtuals

        public override void Use(Player player, ItemData instance)
        {
        }

        #endregion
    }
}
