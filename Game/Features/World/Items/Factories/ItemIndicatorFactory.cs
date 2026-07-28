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
            { typeof(ToolDefinition), () => new MiningIndicator() },
            { typeof(BlockItemDefinition), () => new PlacementIndicator() },
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
