using System;
using System.Collections.Generic;
using Jogo25D.Items.Indicators;

namespace Jogo25D.Items
{
    public static class ItemIndicatorFactory
    {
        private static readonly Dictionary<Type, Func<IItemIndicator>> _factories = new()
        {
            { typeof(WeaponDefinition), () => new WeaponAimIndicator() },
        };

        public static IItemIndicator Create(ItemDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            return _factories.TryGetValue(definition.GetType(), out var factory) ? factory() : null;
        }
    }
}
