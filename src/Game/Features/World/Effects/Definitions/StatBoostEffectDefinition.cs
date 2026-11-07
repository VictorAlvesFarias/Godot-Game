using Jogo25D.Characters;

namespace Jogo25D.Effects
{
    public class StatBoostEffectDefinition : EffectDefinition
    {
        public override void Apply(Player player, EffectDefinitionData data, float delta)
        {
            if (data.Timer != 0f)
            {
                return;
            }

            data.Timer = 1f;

            foreach (var modifier in Modifiers)
            {
                player.ActiveProperties.Add(modifier);
            }
        }

        public override void OnFinished(Player player, EffectDefinitionData data, float delta)
        {
            foreach (var modifier in Modifiers)
            {
                player.ActiveProperties.Remove(modifier);
            }
        }
    }
}
