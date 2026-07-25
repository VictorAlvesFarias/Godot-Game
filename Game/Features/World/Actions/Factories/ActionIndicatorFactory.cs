using System;
using System.Collections.Generic;
using Jogo25D.Actions.Indicators;

namespace Jogo25D.Actions
{
    // So sabe COMO construir um indicador novo pra cada classe de
    // ActionDefinition (ex: GroundStrikeDefinition -> GroundStrikeIndicator)
    // - nao guarda nenhuma instancia (diferente de ActionDB, que reusa o
    // mesmo ActionDefinition pra todo mundo). Quem guarda a instancia
    // criada (uma por player, pra nao compartilhar estado entre eles) e o
    // Player, que chama Create() so na primeira vez que precisa de cada
    // tipo.
    public static class ActionIndicatorFactory
    {
        private static readonly Dictionary<Type, Func<IActionIndicator>> _factories = new()
        {
            { typeof(GroundStrikeDefinition), () => new GroundStrikeIndicator() },
        };

        public static IActionIndicator Create(ActionDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            return _factories.TryGetValue(definition.GetType(), out var factory) ? factory() : null;
        }
    }
}
