using Jogo25D.Features.World.Properties.Resources;

namespace Jogo25D.Properties
{
    public static class PropertyDescriptionFactory
    {
        public static string Describe(BasePropertyData property)
        {
            return property switch
            {
                DamageResistencePropertyData r => $"Resistência a {r.DamageType}: {r.ResistanceFactor:P0}",
                DamageResistenceMultiplierPropertyData m => $"Mult. resistência a {m.DamageType}: x{m.Multiplier:F2}",
                DamagePropertyData d => $"+{d.DamageAmount} dano {d.DamageType} (x{d.DamageMultiplier:F2})",
                CritPropertyData c => $"Crítico: +{c.CritChance:P0} chance, +{c.CritDamage:P0} dano",
                AttackPropertyData => "Bônus de ataque",
                DashPropertyData => "Bônus de dash",
                MovementPropertyData mv => $"+{mv.Speed:F0} velocidade de movimento",
                HealthPropertyData h => $"+{h.MaxHealth} vida máxima",
                null => "",
                _ => property.GetType().Name
            };
        }
    }
}
