using Godot;
using Jogo25D.Items;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.World.Items.Resources
{
    // Pecas equipadas do personagem, uma por EquipmentSlot (nulo = vazio). Salvo no estado do personagem.
    // Um campo por slot (e nao um array): o parser descarta os nulos de arrays, o que deslocaria as pecas ao recarregar.
    public partial class EquipmentData : Resource
    {
        #region Dinamic properties

        public const int SlotCount = 5;

        [Export, GodotDictionaryField]
        public ItemData Head { get; set; }

        [Export, GodotDictionaryField]
        public ItemData Chest { get; set; }

        [Export, GodotDictionaryField]
        public ItemData Legs { get; set; }

        [Export, GodotDictionaryField]
        public ItemData Accessory1 { get; set; }

        [Export, GodotDictionaryField]
        public ItemData Accessory2 { get; set; }

        // As pecas na ordem do EquipmentSlot (nulo = slot vazio).
        public ItemData[] Slots => [Head, Chest, Legs, Accessory1, Accessory2];

        #endregion

        #region Core - Slots

        public ItemData Get(EquipmentSlot slot)
        {
            return Slots[(int)slot];
        }

        public void Set(EquipmentSlot slot, ItemData item)
        {
            switch (slot)
            {
                case EquipmentSlot.Head:
                    Head = item;
                    break;

                case EquipmentSlot.Chest:
                    Chest = item;
                    break;

                case EquipmentSlot.Legs:
                    Legs = item;
                    break;

                case EquipmentSlot.Accessory1:
                    Accessory1 = item;
                    break;

                case EquipmentSlot.Accessory2:
                    Accessory2 = item;
                    break;
            }
        }

        #endregion
    }
}
