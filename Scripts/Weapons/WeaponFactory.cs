using Jogo25D.Items;
using Jogo25D.Weapons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jogo25D.Scripts.Weapons
{
    public static class WeaponFactory
    {
        public static Weapon Use(Item item)
        {
            var weaponInstance = (Weapon)null;

            if (item.Type == ItemType.WeaponRanged)
            {
                var rangedWeapon = new RangedWeapon();

                rangedWeapon.Range = item.AttackRange;
                rangedWeapon.Area = item.AttackArea;
                rangedWeapon.BulletScene = item.ProjectileScene;
                rangedWeapon.BulletSpeed = item.ProjectileSpeed;

                weaponInstance = rangedWeapon;
            }
            else
            {
                var meleeWeapon = new MeleeWeapon();

                meleeWeapon.Range = item.AttackRange;
                weaponInstance = meleeWeapon;
            }

            weaponInstance.WeaponName = item.ItemName;
            weaponInstance.Damage = item.Damage;
            weaponInstance.AttackCooldown = item.AttackCooldown;
            weaponInstance.Icon = item.Icon;
            weaponInstance.MaxCharges = item.MaxCharges;
            weaponInstance.CurrentCharges = item.MaxCharges;
            weaponInstance.ChargeType = item.ChargeType;
            weaponInstance.InfiniteCharges = item.InfiniteCharges;
            weaponInstance.ReloadCooldown = item.ReloadCooldown;

            return weaponInstance;
        }
    }
}
