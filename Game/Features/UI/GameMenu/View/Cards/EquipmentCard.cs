using Godot;
using System.Linq;

namespace Jogo25D.UI
{
    // Coluna de equipamento ao lado do card Player: um EquipmentSlot por slot do EquipmentData (clique direito desequipa).
    public partial class EquipmentCard : CardUI
    {
        #region Node children references

        public EquipmentSlot[] Slots { get; private set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            Slots = GetNode("Margin/Slots").GetChildren().OfType<EquipmentSlot>().OrderBy(slot => slot.Index).ToArray();

            foreach (var slot in Slots)
            {
                var index = slot.Index;

                slot.GuiInput += @event => OnSlotInput(index, @event);
            }
        }

        #endregion

        #region Core - Contrato do card

        public override void OnOpened()
        {
            base.OnOpened();

            OnInventoryChanged();
        }

        // Equipar e desequipar tambem mexem no inventario, entao o InventoryChanged cobre os dois.
        public override void OnInventoryChanged()
        {
            if (!HasPlayer || LocalPlayer.Equipment == null)
            {
                return;
            }

            foreach (var slot in Slots)
            {
                var definition = LocalPlayer.EquipmentDefinitionOf(LocalPlayer.Equipment.Slots[slot.Index]);

                if (definition == null)
                {
                    slot.Clear();
                }
                else
                {
                    slot.ShowItem(definition);
                }
            }
        }

        private void OnSlotInput(int index, InputEvent @event)
        {
            if (HasPlayer && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
            {
                LocalPlayer.UnequipArmorRequest(index);
                AcceptEvent();
            }
        }

        #endregion
    }
}
