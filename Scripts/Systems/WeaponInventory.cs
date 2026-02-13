using Godot;
using System;
using System.Collections.Generic;
using Jogo25D.Weapons;

namespace Jogo25D.Systems
{
    /// <summary>
    /// Sistema de inventário para gerenciar armas do player
    /// </summary>
    public partial class WeaponInventory : Node2D
    {
        [Export] public int MaxWeapons { get; set; } = 4;
        [Export] public float WeaponOffset { get; set; } = 25.0f; // Distância do centro do player
        
        private List<Weapon> weapons = new List<Weapon>();
        private int currentWeaponIndex = 0;
        private Weapon currentWeapon;
        private Vector2 lastAttackDirection = Vector2.Right;

        [Signal]
        public delegate void WeaponChangedEventHandler(Weapon weapon, int index);

        public Weapon CurrentWeapon => currentWeapon;
        public int CurrentWeaponIndex => currentWeaponIndex;
        public int WeaponCount => weapons.Count;

        public override void _Ready()
        {
            // Coletar todas as armas que já são filhas deste nó
            foreach (Node child in GetChildren())
            {
                if (child is Weapon weapon)
                {
                    weapons.Add(weapon);
                    weapon.OnUnequip();
                }
            }

            // Equipar a primeira arma se houver
            if (weapons.Count > 0)
            {
                EquipWeapon(0);
            }
        }

        public override void _Process(double delta)
        {
            // Troca de armas desabilitada - apenas pelo inventário
            // HandleWeaponSwitch();
            UpdateWeaponPosition();
        }

        /// <summary>
        /// Atualiza a posição e rotação do inventário baseado na direção
        /// </summary>
        private void UpdateWeaponPosition()
        {
            if (lastAttackDirection.LengthSquared() > 0.01f)
            {
                // Rotacionar para a direção do ataque
                Rotation = lastAttackDirection.Angle();
                
                // Inverter posição horizontal se estiver mirando para a esquerda
                if (lastAttackDirection.X < 0)
                {
                    // Lado esquerdo
                    Position = new Vector2(-WeaponOffset, 0);
                    Scale = new Vector2(1, -1); // Flip vertical da arma
                }
                else
                {
                    // Lado direito
                    Position = new Vector2(WeaponOffset, 0);
                    Scale = new Vector2(1, 1);
                }
            }
        }

        /// <summary>
        /// Adiciona uma nova arma ao inventário
        /// </summary>
        public bool AddWeapon(Weapon weapon)
        {
            if (weapons.Count >= MaxWeapons)
            {
                GD.PrintErr("[Inventory] Inventário cheio!");
                return false;
            }

            weapons.Add(weapon);
            AddChild(weapon);
            weapon.OnUnequip();

            GD.Print($"[Inventory] Arma adicionada: {weapon.WeaponName}");

            // Se for a primeira arma, equipar automaticamente
            if (weapons.Count == 1)
            {
                EquipWeapon(0);
            }

            return true;
        }

        /// <summary>
        /// Remove uma arma do inventário
        /// </summary>
        public bool RemoveWeapon(int index)
        {
            if (index < 0 || index >= weapons.Count)
                return false;

            var weapon = weapons[index];
            weapons.RemoveAt(index);
            weapon.QueueFree();

            // Se removeu a arma atual, equipar outra
            if (index == currentWeaponIndex && weapons.Count > 0)
            {
                currentWeaponIndex = Mathf.Clamp(currentWeaponIndex, 0, weapons.Count - 1);
                EquipWeapon(currentWeaponIndex);
            }
            else if (weapons.Count == 0)
            {
                currentWeapon = null;
                currentWeaponIndex = 0;
            }

            return true;
        }

        /// <summary>
        /// Equipa uma arma específica pelo índice
        /// </summary>
        public void EquipWeapon(int index)
        {
            if (index < 0 || index >= weapons.Count)
                return;

            // Desequipar arma atual
            if (currentWeapon != null)
            {
                currentWeapon.OnUnequip();
            }

            // Equipar nova arma
            currentWeaponIndex = index;
            currentWeapon = weapons[index];
            currentWeapon.OnEquip();

            EmitSignal(SignalName.WeaponChanged, currentWeapon, index);
            
            GD.Print($"[Inventory] Equipou: {currentWeapon.WeaponName} (Index: {index})");
        }

        /// <summary>
        /// Troca para a próxima arma
        /// </summary>
        public void NextWeapon()
        {
            if (weapons.Count <= 1)
                return;

            int nextIndex = (currentWeaponIndex + 1) % weapons.Count;
            EquipWeapon(nextIndex);
        }

        /// <summary>
        /// Troca para a arma anterior
        /// </summary>
        public void PreviousWeapon()
        {
            if (weapons.Count <= 1)
                return;

            int prevIndex = (currentWeaponIndex - 1 + weapons.Count) % weapons.Count;
            EquipWeapon(prevIndex);
        }

        /// <summary>
        /// Ataca com a arma atual
        /// </summary>
        public void Attack(Vector2 direction)
        {
            if (currentWeapon != null && currentWeapon.CanAttack)
            {
                lastAttackDirection = direction.Normalized();
                currentWeapon.Attack(direction);
            }
        }

        /// <summary>
        /// Obtém uma arma pelo índice
        /// </summary>
        public Weapon GetWeapon(int index)
        {
            if (index >= 0 && index < weapons.Count)
                return weapons[index];
            return null;
        }

        /// <summary>
        /// Verifica se pode atacar
        /// </summary>
        public bool CanAttack()
        {
            return currentWeapon != null && currentWeapon.CanAttack;
        }

        /// <summary>
        /// Gerencia a troca de armas por input
        /// </summary>
        private void HandleWeaponSwitch()
        {
            // Usar scroll do mouse para trocar armas
            if (Input.IsActionJustPressed("weapon_next"))
            {
                NextWeapon();
            }
            else if (Input.IsActionJustPressed("weapon_prev"))
            {
                PreviousWeapon();
            }

            // Teclas numéricas para seleção direta
            for (int i = 1; i <= Mathf.Min(weapons.Count, 9); i++)
            {
                if (Input.IsActionJustPressed($"weapon_{i}"))
                {
                    EquipWeapon(i - 1);
                }
            }
        }
    }
}
