using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items
{
    public interface IItemIndicator
    {
        void Update(Player player, ItemDefinitionData data, float delta);

        void Hide(Player player);

        void Destroy();
    }
}
