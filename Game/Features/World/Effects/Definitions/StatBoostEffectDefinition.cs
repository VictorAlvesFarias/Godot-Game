using Jogo25D.Characters;

namespace Jogo25D.Effects
{
    // Efeito generico que so insere/remove Modifiers em player.Properties
    // enquanto ativo - reutilizavel por qualquer buff/debuff de atributo (ex:
    // velocidade), sem logica propria alem disso. Data.Timer e reaproveitado
    // como flag "ja inseriu" (0 = nao, 1 = sim), ja que Apply roda todo tick
    // mas a insercao so deve acontecer uma vez.
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
                player.Properties.Add(modifier);
            }
        }

        public override void OnFinished(Player player, EffectDefinitionData data, float delta)
        {
            foreach (var modifier in Modifiers)
            {
                player.Properties.Remove(modifier);
            }
        }
    }
}
