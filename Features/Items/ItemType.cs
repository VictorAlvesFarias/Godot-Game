namespace Jogo25D.Items
{
    public enum ItemType
    {
        WeaponMelee,
        WeaponRanged,
        Consumable,
        Material,
        Misc
    }

    public static class ItemTypeExtensions
    {
        public static bool IsWeapon(this ItemType type)
        {
            return type == ItemType.WeaponMelee || type == ItemType.WeaponRanged;
        }
    }
}
