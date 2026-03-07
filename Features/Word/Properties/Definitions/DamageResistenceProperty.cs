using Jogo25D.Items;

namespace Jogo25D.Properties
{
    public class DamageResistenceProperty : BaseProperty
    {
        public DamageType DamageType { get; set; }
        public float ResistanceFactor { get; set; }
    }
}