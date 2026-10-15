using Godot;
using Jogo25D.Characters;
using Jogo25D.Items;
using Jogo25D.Systems;
using System.Linq;

namespace Jogo25D.UI
{
    // Card "Inventário": hotbar e grade de slots (InventorySlot, ja na cena), arrastar entre slots ou para "SOLTAR",
    // menu de contexto, Q sobre um slot e a opcao de coleta automatica.
    public partial class InventoryCard : CardUI
    {
        #region Node children references

        public InventorySlot[] Slots { get; private set; }
        public CheckButton AutoPickupToggle { get; private set; }
        public Panel DropSlot { get; private set; }
        public Panel DragPreview { get; private set; }
        public TextureRect DragPreviewIcon { get; private set; }
        public Control ContextMenu { get; private set; }
        public Button EquipButton { get; private set; }

        #endregion

        #region Dinamic properties

        private static readonly StringName NormalVariation = "Slot";
        private static readonly StringName HoverVariation = "SlotHover";
        private static readonly Vector2 DragOffset = new(-32, -32);

        public int SlotCount => Mathf.Min(LocalPlayer?.Inventory?.Size ?? 0, Slots.Length);

        public bool IsDragging { get; private set; }
        public int DraggedSlotIndex { get; private set; } = -1;
        public long DraggedInstanceId { get; private set; }
        public int SelectedSlotIndex { get; private set; } = -1;

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            var hotbar = GetNode<Node>("Box/MarginContainer/Content/HotbarRow");
            var grid = GetNode<Node>("Box/MarginContainer/Content/GridScroll/GridContainer");

            Slots = hotbar.GetChildren().Concat(grid.GetChildren()).OfType<InventorySlot>().OrderBy(slot => slot.Index).ToArray();
            AutoPickupToggle = GetNode<CheckButton>("Box/MarginContainer/Content/AutoPickup");
            DropSlot = GetNode<Panel>("Box/MarginContainer/Content/DropSlot");
            DragPreview = GetNode<Panel>("DragPreview");
            DragPreviewIcon = GetNode<TextureRect>("DragPreview/Icon");
            ContextMenu = GetNode<Control>("ContextMenu");
            EquipButton = GetNode<Button>("ContextMenu/MarginContainer/VBoxContainer/EquipButton");

            foreach (var slot in Slots)
            {
                var index = slot.Index;

                slot.GuiInput += @event => OnSlotInput(index, @event);
            }

            AutoPickupToggle.Toggled += pressed =>
            {
                if (HasPlayer)
                {
                    LocalPlayer.AutoPickup = pressed;
                }
            };

            DropSlot.MouseEntered += () => DropSlot.ThemeTypeVariation = HoverVariation;
            DropSlot.MouseExited += () => DropSlot.ThemeTypeVariation = NormalVariation;

            EquipButton.Pressed += OnEquipPressed;
        }

        public override void _Process(double delta)
        {
            if (IsDragging)
            {
                DragPreview.GlobalPosition = GetViewport().GetMousePosition() + DragOffset;
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (IsDragging && @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release)
            {
                var targetSlot = GetSlotAtPosition(release.GlobalPosition);

                if (targetSlot >= 0)
                {
                    EndDrag(targetSlot);
                }
                else if (DropSlot.GetGlobalRect().HasPoint(release.GlobalPosition))
                {
                    DropDraggedItem();
                }
                else
                {
                    CancelDrag();
                }

                GetViewport().SetInputAsHandled();

                return;
            }

            if (@event.IsActionPressed("ui_cancel") && IsDragging)
            {
                CancelDrag();
                GetViewport().SetInputAsHandled();
            }
        }

        // Q depois da interface: com um campo de texto em foco, a tecla vira texto e nao solta item.
        public override void _UnhandledInput(InputEvent @event)
        {
            if (IsOpen && !IsDragging && @event.IsActionPressed("drop_item") && !@event.IsEcho())
            {
                DropHoveredItem();
                GetViewport().SetInputAsHandled();

                return;
            }

            if (ContextMenu.Visible && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click &&
                !ContextMenu.GetGlobalRect().HasPoint(click.GlobalPosition))
            {
                ContextMenu.Visible = false;
            }
        }

        #endregion

        #region Core - Contrato do card

        public override void OnOpened()
        {
            base.OnOpened();

            if (HasPlayer)
            {
                AutoPickupToggle.SetPressedNoSignal(LocalPlayer.AutoPickup);
            }

            OnInventoryChanged();
        }

        public override void OnClosed()
        {
            base.OnClosed();

            CancelDrag();
            ContextMenu.Visible = false;
        }

        public override void OnInventoryChanged()
        {
            if (!HasPlayer || LocalPlayer.Inventory == null)
            {
                return;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                var data = InventorySystem.GetSlot(LocalPlayer.Inventory, i);
                var definition = data == null ? null : ItemFactory.Create(data.Id);

                if (definition == null || definition.IsEmpty(data))
                {
                    Slots[i].Clear();
                }
                else
                {
                    Slots[i].ShowItem(definition, data);
                }
            }
        }

        #endregion

        #region Core - Slots

        public int GetSlotAtPosition(Vector2 globalPosition)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (Slots[i].IsVisibleInTree() && Slots[i].GetGlobalRect().HasPoint(globalPosition))
                {
                    return i;
                }
            }

            return -1;
        }

        // Q com a tela aberta (o input do jogador fica bloqueado): solta 1 unidade do item do slot sob o mouse.
        public void DropHoveredItem()
        {
            if (!HasPlayer || LocalPlayer.Inventory == null)
            {
                return;
            }

            var index = GetSlotAtPosition(GetViewport().GetMousePosition());
            var data = index < 0 ? null : InventorySystem.GetSlot(LocalPlayer.Inventory, index);

            if (data == null || data.InstanceId <= 0 || data.Quantity <= 0)
            {
                return;
            }

            LocalPlayer.DropItemRequest(data.InstanceId, 1);
        }

        #endregion

        #region Core - Drag and drop

        public void OnSlotInput(int slotIndex, InputEvent @event)
        {
            if (!HasPlayer || LocalPlayer.Inventory == null || @event is not InputEventMouseButton { Pressed: true } press)
            {
                return;
            }

            if (InventorySystem.GetSlot(LocalPlayer.Inventory, slotIndex) == null)
            {
                return;
            }

            if (press.ButtonIndex == MouseButton.Left)
            {
                StartDrag(slotIndex, press.GlobalPosition);
                Slots[slotIndex].AcceptEvent();
            }
            else if (press.ButtonIndex == MouseButton.Right)
            {
                ShowContextMenu(slotIndex, press.GlobalPosition);
                Slots[slotIndex].AcceptEvent();
            }
        }

        public void StartDrag(int slotIndex, Vector2 mousePosition)
        {
            var data = InventorySystem.GetSlot(LocalPlayer.Inventory, slotIndex);

            if (data == null)
            {
                return;
            }

            IsDragging = true;
            DraggedSlotIndex = slotIndex;
            DraggedInstanceId = data.InstanceId;

            DragPreviewIcon.Texture = ItemFactory.Create(data.Id)?.Icon;
            DragPreview.GlobalPosition = mousePosition + DragOffset;
            DragPreview.Visible = true;
            Slots[slotIndex].Dimmed = true;
        }

        public void EndDrag(int targetSlotIndex)
        {
            if (!IsDragging)
            {
                return;
            }

            var instanceId = DraggedInstanceId;
            var fromIndex = DraggedSlotIndex;

            StopDrag();

            if (targetSlotIndex != fromIndex && HasPlayer && instanceId > 0 && targetSlotIndex < SlotCount)
            {
                LocalPlayer.MoveItemRequest(instanceId, targetSlotIndex);
            }
        }

        public void CancelDrag()
        {
            if (IsDragging)
            {
                StopDrag();
            }
        }

        // Arrastar ate "SOLTAR": solta a pilha inteira no chao.
        public void DropDraggedItem()
        {
            var instanceId = DraggedInstanceId;

            StopDrag();

            if (!HasPlayer || instanceId <= 0)
            {
                return;
            }

            var quantity = InventorySystem.FindItem(LocalPlayer.Inventory, instanceId)?.Quantity ?? 0;

            if (quantity > 0)
            {
                LocalPlayer.DropItemRequest(instanceId, quantity);
            }
        }

        private void StopDrag()
        {
            DragPreview.Visible = false;

            if (DraggedSlotIndex >= 0 && DraggedSlotIndex < Slots.Length)
            {
                Slots[DraggedSlotIndex].Dimmed = false;
            }

            IsDragging = false;
            DraggedSlotIndex = -1;
            DraggedInstanceId = 0;
        }

        #endregion

        #region Core - Context menu

        public void ShowContextMenu(int slotIndex, Vector2 position)
        {
            var data = InventorySystem.GetSlot(LocalPlayer.Inventory, slotIndex);
            var definition = data == null ? null : ItemFactory.Create(data.Id);

            if (definition == null || definition.IsEmpty(data))
            {
                return;
            }

            SelectedSlotIndex = slotIndex;
            ContextMenu.GlobalPosition = position;
            ContextMenu.Visible = true;
        }

        private void OnEquipPressed()
        {
            var data = SelectedSlotIndex < 0 || !HasPlayer ? null : InventorySystem.GetSlot(LocalPlayer.Inventory, SelectedSlotIndex);

            if (data != null)
            {
                LocalPlayer.EquipItemRequest(data.InstanceId);
            }

            ContextMenu.Visible = false;
        }

        #endregion
    }
}
