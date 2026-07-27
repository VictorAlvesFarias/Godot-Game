using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items
{
    public interface IItemIndicator
    {
        void Update(Player player, ItemDefinition definition, ItemDefinitionData data, float delta);

        void Hide(Player player);
    }
}
