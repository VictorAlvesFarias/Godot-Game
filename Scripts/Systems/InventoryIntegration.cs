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
        [Export] public string PlayerGroupName { get; set; } = "players";
        
        private InventorySystem inventorySystem;
        private WeaponInventory weaponInventory;

        public override void _Ready()
        {
            // Conectar os dois sistemas
            if (InventorySystemPath != null)
            {
                inventorySystem = GetNode<InventorySystem>(InventorySystemPath);
                if (inventorySystem != null)
                {
                    inventorySystem.ItemEquipped += OnItemEquipped;
                }
                else
                {
                    GD.PrintErr("[InventoryIntegration] InventorySystem não encontrado no path!");
                }
            }
            else
            {
                GD.PrintErr("[InventoryIntegration] InventorySystemPath é null!");
            }

            // Encontrar o WeaponInventory do player local dinamicamente
            CallDeferred(nameof(FindLocalPlayerWeaponInventory));

            // Aguardar um frame para garantir que tudo está inicializado
            CallDeferred(nameof(InitializeWeaponsInInventory));
        }

        /// <summary>
        /// Encontra o WeaponInventory do player local (com autoridade multiplayer)
        /// </summary>
        private void FindLocalPlayerWeaponInventory()
        {
            var players = GetTree().GetNodesInGroup(PlayerGroupName);
            var localPeerId = 1;
            var hasMultiplayer = false;

            if (Multiplayer != null && Multiplayer.MultiplayerPeer != null &&
                Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected)
            {
                try
                {
                    localPeerId = Multiplayer.GetUniqueId();
                    hasMultiplayer = true;
                }
                catch
                {
                    hasMultiplayer = false;
                }
            }

            foreach (Node node in players)
            {
                if (node is Jogo25D.Characters.Player player)
                {
                    if (!hasMultiplayer || player.GetMultiplayerAuthority() == localPeerId)
                    {
                        // Encontrou o player local, buscar WeaponInventory
                        weaponInventory = player.GetNode<WeaponInventory>("WeaponInventory");
                        if (weaponInventory != null)
                        {
                            GD.Print($"[InventoryIntegration] WeaponInventory encontrado para player local (PeerID: {localPeerId})");
                        }
                        else
                        {
                            GD.PrintErr("[InventoryIntegration] WeaponInventory não encontrado no player local!");
                        }
                        break;
                    }
                }
            }

            if (weaponInventory == null)
            {
                GD.PrintErr("[InventoryIntegration] Nenhum WeaponInventory do player local encontrado!");
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

        /// <summary>
        /// Adiciona as armas iniciais do WeaponInventory ao inventário visual
        /// </summary>
        private void InitializeWeaponsInInventory()
        {
            if (inventorySystem == null || !IsInstanceValid(inventorySystem))
            {
                GD.PrintErr("[InventoryIntegration] InventorySystem não encontrado!");
                return;
            }
            
            if (weaponInventory == null || !IsInstanceValid(weaponInventory))
            {
                GD.PrintErr("[InventoryIntegration] WeaponInventory não encontrado!");
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

        /// <summary>
        /// Chamado quando um item é equipado no inventário
        /// </summary>
        private void OnItemEquipped(InventoryItem item, int slotIndex)
        {
            if (weaponInventory == null || !IsInstanceValid(weaponInventory))
            {
                GD.PrintErr("[InventoryIntegration] WeaponInventory é null ou foi disposed!");
                return;
            }
            
            if (item.Type != ItemType.Weapon)
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
                else
                {
                    GD.PrintErr($"[InventoryIntegration] Arma não encontrada no WeaponInventory: {weapon.WeaponName}");
                }
            }
            else
            {
                GD.PrintErr($"[InventoryIntegration] ItemReference não é uma Weapon! Tipo: {item.ItemReference?.GetType().Name ?? "null"}");
            }
        }

        /// <summary>
        /// Adiciona um novo item ao inventário (para futuros coletáveis)
        /// </summary>
        public void AddItem(InventoryItem item, int quantity = 1)
        {
            if (inventorySystem != null)
            {
                inventorySystem.AddItem(item, quantity);
            }
        }

        /// <summary>
        /// Adiciona uma nova arma e a integra com o WeaponInventory
        /// </summary>
        public void AddWeapon(Weapon weapon)
        {
            if (weaponInventory == null || !IsInstanceValid(weaponInventory) || 
                inventorySystem == null || !IsInstanceValid(inventorySystem)) return;

            // Adicionar ao WeaponInventory
            weaponInventory.AddChild(weapon);
            weapon.OnUnequip(); // Começa desequipada

            // Adicionar ao inventário visual
            var item = InventoryItem.FromWeapon(weapon);
            inventorySystem.AddItem(item, 1);

            GD.Print($"[InventoryIntegration] Nova arma adicionada: {weapon.WeaponName}");
        }
    }
}
