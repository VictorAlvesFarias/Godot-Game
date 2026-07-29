using Jogo25D.Characters;

namespace Jogo25D.Actions
{
    public interface IActionIndicator
    {
        void Update(Player player, ActionDefinition definition, ActionDefinitionData instance, float delta);

        void Destroy();
    }
}
