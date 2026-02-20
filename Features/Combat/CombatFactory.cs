using Godot;
using Jogo25D.Characters;
using Jogo25D.Items;
using Jogo25D.Weapons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jogo25D.Scripts.Weapons
{
    public static class CombatFactory
    {
        public static Combat Use(Item item, Player player)
        {
            var weaponInstance = (Combat)null;

            if (item.Type == ItemType.WeaponRanged)
            {
                var rangedWeapon = new RangedCombat(player, item.ProjectileScene);

                rangedWeapon.Range = item.AttackRange;
                rangedWeapon.Area = item.AttackArea;
                rangedWeapon.BulletSpeed = item.ProjectileSpeed;

                weaponInstance = rangedWeapon;
            }
            else
            {
                var meleeWeapon = new MeleeCombat(player);

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
