using System;
using System.Collections.Generic;
using Jogo25D.Actions.Indicators;

namespace Jogo25D.Actions
{
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
