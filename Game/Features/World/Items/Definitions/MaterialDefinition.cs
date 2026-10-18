using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items
{
    // Material de criacao (minerio, barra): fica no inventario e entra em receitas; usar na mao nao faz nada.
    public class MaterialDefinition : ItemDefinition
    {
        #region Core - Virtuals

        public override void Use(Player player, ItemData instance)
        {
        }

        #endregion
    }
}
