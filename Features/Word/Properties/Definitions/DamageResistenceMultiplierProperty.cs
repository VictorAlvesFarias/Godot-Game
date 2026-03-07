using Jogo25D.Items;

namespace Jogo25D.Properties
{
    public class DamageResistenceMultiplierProperty : BaseProperty
    {
        public DamageType DamageType { get; set; }
        public float Multiplier { get; set; }
    }
}