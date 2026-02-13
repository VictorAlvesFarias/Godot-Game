using Godot;
using System;
using Jogo25D.Items;
using Jogo25D.Weapons;

namespace Jogo25D.Systems
{
    /// <summary>
    /// Integra o sistema de inventário com o WeaponInventory para equipar armas
    /// </summary>
    public partial class InventoryIntegration : Node
    {
        [Export] public NodePath InventorySystemPath { get; set; }
        [Export] public NodePath WeaponInventoryPath { get; set; }
        
        private InventorySystem inventorySystem;
        private WeaponInventory weaponInventory;

        public override void _Ready()
        {
            if (InventorySystemPath != null)
            {
                inventorySystem = GetNode<InventorySystem>(InventorySystemPath);

                if (inventorySystem != null)
                {
                    inventorySystem.ItemEquipped += OnItemEquipped;
                }
            }

            // Conectar ao WeaponInventory (irmão neste Player)
            if (WeaponInventoryPath != null)
            {
                weaponInventory = GetNode<WeaponInventory>(WeaponInventoryPath);
                if (weaponInventory != null)
                {
                    // Inicializar armas no inventário
                    CallDeferred(nameof(InitializeWeaponsInInventory));
                }
            }
        }

        public override void _ExitTree()
        {
            // Desconectar sinais para evitar erros ao destruir a cena
            if (inventorySystem != null && IsInstanceValid(inventorySystem))
            {
                inventorySystem.ItemEquipped -= OnItemEquipped;
            }
        }

        private void InitializeWeaponsInInventory()
        {
            if (inventorySystem == null || !IsInstanceValid(inventorySystem))
            {
                return;
            }
            
            if (weaponInventory == null || !IsInstanceValid(weaponInventory))
            {
                return;
            }

            // Obter todas as armas do WeaponInventory
            var weapons = weaponInventory.GetChildren();
            
            foreach (var child in weapons)
            {
                if (child is Weapon weapon)
                {
                    var item = InventoryItem.FromWeapon(weapon);
                    inventorySystem.AddItem(item, 1);
                }
            }
        }

        private void OnItemEquipped(InventoryItem item, int slotIndex)
        {
            if (weaponInventory == null || !IsInstanceValid(weaponInventory))
            {
                return;
            }

            // Encontrar a arma correspondente no WeaponInventory
            if (item.ItemReference is Weapon weapon)
            {
                // Encontrar o índice da arma no WeaponInventory
                var weapons = weaponInventory.GetChildren();
                int weaponIndex = -1;
                
                for (int i = 0; i < weapons.Count; i++)
                {
                    if (weapons[i] == weapon)
                    {
                        weaponIndex = i;
                        break;
                    }
                }

                if (weaponIndex >= 0)
                {
                    weaponInventory.EquipWeapon(weaponIndex);
                }
            }
        }

        public void AddItem(InventoryItem item, int quantity = 1)
        {
            if (inventorySystem != null)
            {
                inventorySystem.AddItem(item, quantity);
            }
        }

        public void AddWeapon(Weapon weapon)
        {
            if (
                weaponInventory == null || 
                inventorySystem == null || 
                !IsInstanceValid(weaponInventory) ||
                !IsInstanceValid(inventorySystem)
            )
            { 
                return;
            }

            
            weaponInventory.AddChild(weapon);
            
            weapon.OnUnequip(); 

            var item = InventoryItem.FromWeapon(weapon);

            inventorySystem.AddItem(item, 1);
        }
    }
}
