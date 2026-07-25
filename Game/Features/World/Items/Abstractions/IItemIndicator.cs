using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items
{
    // Implementacao concreta do indicador visual de um item (ex: linha de
    // mira de uma arma). Uma unica instancia serve TODOS os players que
    // usam aquele tipo de item ao mesmo tempo - por isso nunca guarda
    // estado por-jogador aqui dentro; o node visual de fato mora no Player
    // (ver Player.GetOrCreateIndicator).
    public interface IItemIndicator
    {
        void Update(Player player, ItemDefinition definition, ItemDefinitionData data, float delta);

        // So chamado no OnUnequip - o Update ja so roda enquanto o item
        // esta equipado (ver ItemDefinition.ItemIndicator), entao o unico
        // momento que precisa esconder o indicador de proposito e ao
        // trocar/tirar o item.
        void Hide(Player player);
    }
}
