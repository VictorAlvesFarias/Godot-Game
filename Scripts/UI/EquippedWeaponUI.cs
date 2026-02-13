using Godot;
using System;
using Jogo25D.Systems;
using Jogo25D.Weapons;

namespace Jogo25D.UI
{
    /// <summary>
    /// HUD que mostra a arma atualmente equipada (abaixo da vida)
    /// </summary>
    public partial class EquippedWeaponUI : Label
    {
        [Export] public NodePath WeaponInventoryPath { get; set; }
        
        private WeaponInventory weaponInventory;

        public override void _Ready()
        {
            // Conectar ao WeaponInventory
            if (WeaponInventoryPath != null)
            {
                weaponInventory = GetNode<WeaponInventory>(WeaponInventoryPath);
                if (weaponInventory != null)
                {
                    weaponInventory.WeaponChanged += OnWeaponChanged;
                    UpdateDisplay();
                }
            }
        }

        private void OnWeaponChanged(Weapon weapon, int index)
        {
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (weaponInventory == null || weaponInventory.CurrentWeapon == null)
            {
                Text = "Arma: Nenhuma";
                return;
            }

            var weapon = weaponInventory.CurrentWeapon;
            Text = $"🗡 {weapon.WeaponName}";
        }
    }
}
