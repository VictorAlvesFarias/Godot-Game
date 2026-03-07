namespace Jogo25D.Items
{
    public enum DamageType
    {
        Physical,
        Fire,
        Ice,
        Poison,
        Electric,
        True
    }

    public struct DamageInfo
    {
        public int Amount;
        public DamageType Type;
        public int SourcePeerId;
        public float CritChance;
        public float CritDamage;
    }
}