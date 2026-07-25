using System;
using System.Collections.Generic;
using Jogo25D.Items.Indicators;

namespace Jogo25D.Items
{
    // So sabe COMO construir um indicador novo pra cada classe de
    // ItemDefinition (ex: WeaponDefinition -> WeaponAimIndicator) - nao
    // guarda nenhuma instancia (diferente de ItemDB, que reusa o mesmo
    // ItemDefinition pra todo mundo). Quem guarda a instancia criada (uma
    // por player, pra nao compartilhar estado entre eles) e o Player, que
    // chama Create() so na primeira vez que precisa de cada tipo.
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
