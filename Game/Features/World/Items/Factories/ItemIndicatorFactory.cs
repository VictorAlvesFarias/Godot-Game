using System;
using System.Collections.Generic;
using Jogo25D.Items.Indicators;

namespace Jogo25D.Items
{
    public static class ItemIndicatorFactory
    {
        private static readonly Dictionary<Type, Func<ItemDefinition, IItemIndicator>> _factories = new()
        {
            { typeof(WeaponDefinition), def => new WeaponAimIndicator() },
            { typeof(ToolDefinition), def => new MiningIndicator((ToolDefinition)def) },
            { typeof(BlockItemDefinition), def => new PlacementIndicator((BlockItemDefinition)def) },
        };

        public static IItemIndicator Create(ItemDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            return _factories.TryGetValue(definition.GetType(), out var factory) ? factory(definition) : null;
        }
    }
}
