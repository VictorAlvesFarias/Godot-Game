using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Features.World.Properties.Resources;
using Jogo25D.Features.World.Resolver.Singletons;
using Jogo25D.Items;
using Jogo25D.Properties;
using Jogo25D.Systems;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.UI
{
    public partial class InventoryUI : ScreenUI
    {
        #region Node children references

        public Control MainControl { get; private set; }
        public Panel DragPreviewTemplate { get; private set; }
        public Panel ContextMenu { get; private set; }
        public VBoxContainer ContextMenuContainer { get; private set; }
        public Panel DropSlot { get; private set; }
        public HBoxContainer HotbarRow { get; private set; }
        public GridContainer GridContainer { get; private set; }
        public AnimatedSprite2D CharacterSprite { get; private set; }
        public Label CharacterNameLabel { get; private set; }
        public Label CharacterHealthLabel { get; private set; }
        public VBoxContainer BuffsListContainer { get; private set; }
        public Button EquiparButtonTemplate { get; private set; }
        public Label EmptyPropertyLabelTemplate { get; private set; }
        public Label PropertyLabelTemplate { get; private set; }

        #endregion

        #region Dinamic properties

        public Player LocalPlayer { get; set; }
        public PlayerInput PlayerInput => LocalPlayer?.Input;
        public const int MAX_SLOTS = 128;

        private static readonly StringName VARIACAO_SLOT = "Slot";
        private static readonly StringName VARIACAO_SLOT_HOVER = "SlotHover";
        private static readonly StringName VARIACAO_DROP = "DropSlot";
        private static readonly StringName VARIACAO_DROP_HOVER = "DropSlotHover";

        public int SlotCount => Mathf.Min(LocalPlayer?.Inventory?.Size ?? 0, MAX_SLOTS);

        public Panel[] SlotPanels { get; set; } = new Panel[MAX_SLOTS];
        public TextureRect[] IconRects { get; set; } = new TextureRect[MAX_SLOTS];
        public Label[] QuantityLabels { get; set; } = new Label[MAX_SLOTS];
        public Label[] NameLabels { get; set; } = new Label[MAX_SLOTS];
        public Label[] NumLabels { get; set; } = new Label[MAX_SLOTS];
        public int SelectedSlotIndex { get; set; } = -1;

        public bool IsDragging { get; set; } = false;
        public int DraggedSlotIndex { get; set; } = -1;
        public long DraggedInstanceId { get; set; } = 0;
        public Control DragPreview { get; set; }
        public Vector2 DragOffset { get; set; }

        public override bool IsOverlay => true;

        #endregion

        #region Godot implementation

        public override void _UnhandledInput(InputEvent @event)
        {
            if (ContextMenu == null)
            {
                return;
            }

            if (ContextMenu.Visible &&
                @event is InputEventMouseButton mouseEvent &&
                mouseEvent.Pressed &&
                mouseEvent.ButtonIndex == MouseButton.Left)
            {
                var rect = ContextMenu.GetGlobalRect();

                if (!rect.HasPoint(mouseEvent.GlobalPosition))
                {
                    ContextMenu.Visible = false;
                }
            }
        }

        public override void _Ready()
        {
            ResolveChildren();
            Initialize();
        }

        public override void _ExitTree()
        {
            if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
            {
                LocalPlayer.InventoryChanged -= OnInventoryChanged;
            }
        }

        public override void _Process(double delta)
        {
            if (IsDragging && DragPreview != null)
            {
                DragPreview.GlobalPosition = GetViewport().GetMousePosition() + DragOffset;
            }

            if (Visible)
            {
                UpdateCharacterInfo();
                UpdateCharacterSprite();
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (IsDragging && @event is InputEventMouseButton mouseEvent &&
                mouseEvent.ButtonIndex == MouseButton.Left && !mouseEvent.Pressed)
            {
                int targetSlot = GetSlotAtPosition(mouseEvent.GlobalPosition);

                if (targetSlot >= 0)
                {
                    EndDrag(targetSlot);
                }
                else if (DropSlot != null && DropSlot.GetGlobalRect().HasPoint(mouseEvent.GlobalPosition))
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

                return;
            }

            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                FindLocalPlayerInventorySystem();
            }

            if (PlayerInput != null && PlayerInput.IsBlockedByOther("inventory"))
            {
                return;
            }

            if (@event.IsActionPressed("toggle_inventory") && !@event.IsEcho())
            {
                if (IsDragging)
                {
                    CancelDrag();
                }

                ToggleInventory();
                GetViewport().SetInputAsHandled();
            }
            else if (@event.IsActionPressed("ui_cancel") && Visible)
            {
                ToggleInventory();
                GetViewport().SetInputAsHandled();
            }
        }

        #endregion

        #region Core - Setup

        private void ResolveChildren()
        {
            MainControl = GetNode<Control>("Root");
            DragPreviewTemplate = GetNode<Panel>("Root/DragPreviewTemplate");
            ContextMenu = GetNode<Panel>("ContextMenu");
            ContextMenuContainer = GetNode<VBoxContainer>("ContextMenu/MarginContainer/VBoxContainer");
            DropSlot = GetNode<Panel>("Root/Panel/MainPanel/MarginContainer/MainRow/InventoryColumn/DropSlot");
            HotbarRow = GetNode<HBoxContainer>("Root/Panel/MainPanel/MarginContainer/MainRow/InventoryColumn/HotbarRow");
            GridContainer = GetNode<GridContainer>("Root/Panel/MainPanel/MarginContainer/MainRow/InventoryColumn/GridScroll/GridContainer");
            CharacterSprite = GetNode<AnimatedSprite2D>("Root/Panel/MainPanel/MarginContainer/MainRow/StatsColumn/SpriteBox2/VBoxContainer/CenterContainer/CharacterSprite");
            CharacterNameLabel = GetNode<Label>("Root/Panel/MainPanel/MarginContainer/MainRow/StatsColumn/SpriteBox/MarginContainer/VBoxContainer/CharacterNameLabel");
            CharacterHealthLabel = GetNode<Label>("Root/Panel/MainPanel/MarginContainer/MainRow/StatsColumn/SpriteBox/MarginContainer/VBoxContainer/CharacterHealthLabel");
            BuffsListContainer = GetNode<VBoxContainer>("Root/Panel/MainPanel/MarginContainer/MainRow/StatsColumn/SpriteBox/MarginContainer/VBoxContainer/BuffsScroll/BuffsListContainer");
            EquiparButtonTemplate = GetNode<Button>("ContextMenu/MarginContainer/VBoxContainer/EquiparButtonTemplate");
            EmptyPropertyLabelTemplate = GetNode<Label>("Root/Panel/MainPanel/MarginContainer/MainRow/StatsColumn/SpriteBox/MarginContainer/VBoxContainer/BuffsScroll/BuffsListContainer/EmptyPropertyLabelTemplate");
            PropertyLabelTemplate = GetNode<Label>("Root/Panel/MainPanel/MarginContainer/MainRow/StatsColumn/SpriteBox/MarginContainer/VBoxContainer/BuffsScroll/BuffsListContainer/PropertyLabelTemplate");
        }

        private void Initialize()
        {
            var dropTarget = DropSlot;

            if (dropTarget != null)
            {
                foreach (var child in dropTarget.GetChildren())
                {
                    if (child is Control control)
                    {
                        control.MouseFilter = Control.MouseFilterEnum.Ignore;
                    }
                }

                dropTarget.MouseEntered += () => dropTarget.ThemeTypeVariation = VARIACAO_DROP_HOVER;
                dropTarget.MouseExited += () => dropTarget.ThemeTypeVariation = VARIACAO_DROP;
            }

            GridContainer.Columns = 8;

            EquiparButtonTemplate.Visible = false;
            EmptyPropertyLabelTemplate.Visible = false;
            PropertyLabelTemplate.Visible = false;

            CharacterSprite.Play("idle");

            CreateDragPreview();

            FindLocalPlayerInventorySystem();
        }

        public void CreateDragPreview()
        {
            var template = DragPreviewTemplate;

            if (template == null)
            {
                GD.PushError("InventoryUI: DragPreviewTemplate não encontrado em Root.");

                return;
            }

            template.Visible = false;

            DragPreview = (Panel)template.Duplicate();
            DragPreview.Visible = false;

            DragPreview.MouseFilter = Control.MouseFilterEnum.Ignore;

            foreach (var child in DragPreview.GetChildren())
            {
                if (child is Control control)
                {
                    control.MouseFilter = Control.MouseFilterEnum.Ignore;
                }
            }

            MainControl.AddChild(DragPreview);
        }

        public void FindLocalPlayerInventorySystem()
        {
            if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
            {
                LocalPlayer.InventoryChanged -= OnInventoryChanged;
            }

            LocalPlayer = null;

            LocalPlayer = Players.GetLocal();

            if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
            {
                LocalPlayer.InventoryChanged += OnInventoryChanged;

                if (SlotPanels[0] == null)
                {
                    InitializeSlots();
                }
                else
                {
                    OnInventoryChanged();
                }

                UpdatePropertiesList();
            }
        }

        public void InitializeSlots()
        {
            var hotbarTemplate = (Panel)HotbarRow.GetChild(0).Duplicate();
            var gridTemplate = (Panel)GridContainer.GetChild(0).Duplicate();

            foreach (Node child in HotbarRow.GetChildren())
            {
                HotbarRow.RemoveChild(child);
                child.QueueFree();
            }

            foreach (Node child in GridContainer.GetChildren())
            {
                GridContainer.RemoveChild(child);
                child.QueueFree();
            }

            for (int i = 0; i < MAX_SLOTS; i++)
            {
                SlotPanels[i] = null;
                IconRects[i] = null;
                NameLabels[i] = null;
                QuantityLabels[i] = null;
                NumLabels[i] = null;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                SetupSlot(i, i < 8 ? hotbarTemplate : gridTemplate);
            }

            hotbarTemplate.QueueFree();
            gridTemplate.QueueFree();

            OnInventoryChanged();
        }

        #endregion

        #region Core - Drag and drop

        public int GetSlotAtPosition(Vector2 globalPosition)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (SlotPanels[i] != null && SlotPanels[i].GetGlobalRect().HasPoint(globalPosition))
                {
                    return i;
                }
            }

            return -1;
        }

        public void SetupSlot(int index, Panel template)
        {
            SlotPanels[index] = (Panel)template.Duplicate();

            if (index < 8)
            {
                HotbarRow.AddChild(SlotPanels[index]);
            }
            else
            {
                GridContainer.AddChild(SlotPanels[index]);
            }

            IconRects[index] = SlotPanels[index].GetNode<TextureRect>("MarginContainer/CenterContainer/Icon");
            NameLabels[index] = SlotPanels[index].GetNode<Label>("MarginContainer/CenterContainer/NameLabel");
            QuantityLabels[index] = SlotPanels[index].GetNode<Label>("QuantityLabel");
            NumLabels[index] = SlotPanels[index].GetNode<Label>("NumLabel");

            IconRects[index].Texture = null;
            NameLabels[index].Visible = false;
            QuantityLabels[index].Text = "";
            NumLabels[index].Text = $"{index + 1}";

            int slotIndex = index;
            SlotPanels[index].GuiInput += (InputEvent @event) => OnSlotInput(slotIndex, @event);

            foreach (var child in SlotPanels[index].GetChildren())
            {
                if (child is Control control)
                {
                    control.MouseFilter = Control.MouseFilterEnum.Ignore;
                }
            }

            var panel = SlotPanels[index];

            panel.MouseEntered += () => panel.ThemeTypeVariation = VARIACAO_SLOT_HOVER;
            panel.MouseExited += () => panel.ThemeTypeVariation = VARIACAO_SLOT;
        }

        public void OnSlotInput(int slotIndex, InputEvent @event)
        {
            if (LocalPlayer?.Inventory == null)
            {
                return;
            }

            var slot = InventorySystem.GetSlot(LocalPlayer.Inventory, slotIndex);

            if (@event is InputEventMouseButton mouseEvent)
            {
                if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed && slot != null)
                {
                    StartDrag(slotIndex, mouseEvent.GlobalPosition);
                    SlotPanels[slotIndex].AcceptEvent();
                }
                else if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed && slot != null)
                {
                    ShowContextMenuForSlot(slotIndex, mouseEvent.GlobalPosition);
                    SlotPanels[slotIndex].AcceptEvent();
                }
            }
        }

        public void StartDrag(int slotIndex, Vector2 mousePos)
        {
            if (LocalPlayer?.Inventory == null)
            {
                return;
            }

            var slot = InventorySystem.GetSlot(LocalPlayer.Inventory, slotIndex);

            if (slot == null)
            {
                return;
            }

            var def = ItemFactory.Create(slot.Id);

            GD.Print($"StartDrag: iniciando arrasto do slot {slotIndex} ({def?.Name})");

            IsDragging = true;
            DraggedSlotIndex = slotIndex;
            DraggedInstanceId = slot.InstanceId;

            if (DragPreview != null)
            {
                var iconRect = DragPreview.GetNode<TextureRect>("Icon");

                iconRect.Texture = def?.Icon;

                DragOffset = new Vector2(-32, -32);
                DragPreview.GlobalPosition = mousePos + DragOffset;
                DragPreview.Visible = true;

                if (IconRects[slotIndex] != null)
                {
                    IconRects[slotIndex].Modulate = new Color(1, 1, 1, 0.5f);
                }
            }
        }

        public void EndDrag(int targetSlotIndex)
        {
            if (!IsDragging || DraggedSlotIndex < 0)
            {
                return;
            }

            GD.Print($"EndDrag: arrastado slot {DraggedSlotIndex} para slot {targetSlotIndex}");

            if (DragPreview != null)
            {
                DragPreview.Visible = false;
            }

            if (DraggedSlotIndex >= 0 && DraggedSlotIndex < MAX_SLOTS && IconRects[DraggedSlotIndex] != null)
            {
                IconRects[DraggedSlotIndex].Modulate = Colors.White;
            }

            if (targetSlotIndex != DraggedSlotIndex && LocalPlayer?.Inventory != null)
            {
                SwapItems(DraggedInstanceId, targetSlotIndex);
            }

            IsDragging = false;
            DraggedSlotIndex = -1;
            DraggedInstanceId = 0;
        }

        public void CancelDrag()
        {
            if (!IsDragging)
            {
                return;
            }

            if (DragPreview != null)
            {
                DragPreview.Visible = false;
            }

            if (DraggedSlotIndex >= 0 && DraggedSlotIndex < MAX_SLOTS && IconRects[DraggedSlotIndex] != null)
            {
                IconRects[DraggedSlotIndex].Modulate = Colors.White;
            }

            IsDragging = false;
            DraggedSlotIndex = -1;
            DraggedInstanceId = 0;
        }

        public void DropDraggedItem()
        {
            if (!IsDragging || DraggedInstanceId <= 0 || LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                CancelDrag();

                return;
            }

            var slot = InventorySystem.FindItem(LocalPlayer.Inventory, DraggedInstanceId);
            var quantity = slot?.Quantity ?? 0;

            if (DragPreview != null)
            {
                DragPreview.Visible = false;
            }

            if (DraggedSlotIndex >= 0 && DraggedSlotIndex < MAX_SLOTS && IconRects[DraggedSlotIndex] != null)
            {
                IconRects[DraggedSlotIndex].Modulate = Colors.White;
            }

            if (quantity > 0)
            {
                LocalPlayer.DropItemRequest(DraggedInstanceId, quantity);
            }

            IsDragging = false;
            DraggedSlotIndex = -1;
            DraggedInstanceId = 0;
        }

        public void SwapItems(long instanceId, int toIndex)
        {
            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                return;
            }

            if (instanceId <= 0 || toIndex < 0 || toIndex >= SlotCount)
            {
                return;
            }

            LocalPlayer.MoveItemRequest(instanceId, toIndex);
        }

        #endregion

        #region Core - Slots

        public void UpdateSlot(int index)
        {
            if (LocalPlayer?.Inventory == null)
            {
                return;
            }

            if (IconRects == null || NameLabels == null || QuantityLabels == null)
            {
                return;
            }

            if (index < 0
                || index >= IconRects.Length
                || index >= NameLabels.Length
                || index >= QuantityLabels.Length)
                return;

            var slot = InventorySystem.GetSlot(LocalPlayer.Inventory, index);

            var definition = slot == null ? null : ItemFactory.Create(slot.Id);

            if (slot == null || definition == null || definition.IsEmpty(slot))
            {
                IconRects[index].Texture = null;
                NameLabels[index].Visible = false;
                QuantityLabels[index].Text = "";

                return;
            }

            if (definition.Icon != null)
            {
                IconRects[index].Texture = definition.Icon;
                NameLabels[index].Visible = false;
            }
            else
            {
                IconRects[index].Texture = null;
                NameLabels[index].Text = definition.Name;
                NameLabels[index].Visible = true;
            }

            if (definition.Stackable && slot.Quantity > 1)
            {
                QuantityLabels[index].Text = $"x{slot.Quantity}";
            }
            else
            {
                QuantityLabels[index].Text = "";
            }
        }

        #endregion

        #region Core - Context menu

        public void ShowContextMenuForSlot(int slotIndex, Vector2 position)
        {
            if (LocalPlayer?.Inventory == null)
            {
                return;
            }

            var slot = InventorySystem.GetSlot(LocalPlayer.Inventory, slotIndex);

            if (slot == null)
            {
                return;
            }

            var definition = ItemFactory.Create(slot.Id);

            if (definition == null || definition.IsEmpty(slot))
            {
                return;
            }

            SelectedSlotIndex = slotIndex;

            foreach (Node child in ContextMenuContainer.GetChildren())
            {
                if (child == EquiparButtonTemplate)
                {
                    continue;
                }

                ContextMenuContainer.RemoveChild(child);
                child.QueueFree();
            }

            if (definition != null)
            {
                if (EquiparButtonTemplate == null)
                {
                    GD.PushError("InventoryUI: EquiparButtonTemplate não encontrado, não é possível montar o menu de contexto.");
                }
                else
                {
                    var button = (Button)EquiparButtonTemplate.Duplicate();
                    button.Visible = true;
                    button.Pressed += () => OnContextMenuOption("Equipar");

                    ContextMenuContainer.AddChild(button);
                }
            }

            var minSize = ContextMenuContainer.GetCombinedMinimumSize();
            ContextMenu.CustomMinimumSize = new Vector2(Mathf.Max(120f, (float)minSize.X), (float)minSize.Y);
            ContextMenu.Size = ContextMenu.CustomMinimumSize;

            ContextMenu.GlobalPosition = position;
            ContextMenu.Visible = true;
            ContextMenu.MoveToFront();
        }

        public void OnContextMenuOption(string option)
        {
            if (SelectedSlotIndex < 0 || LocalPlayer?.Inventory == null)
            {
                return;
            }

            var slot = InventorySystem.GetSlot(LocalPlayer.Inventory, SelectedSlotIndex);

            if (slot == null)
            {
                return;
            }

            if (option == "Equipar")
            {
                LocalPlayer.EquipItemRequest(slot.InstanceId);
            }

            ContextMenu.Visible = false;
        }

        #endregion

        #region Core - State

        public void OnInventoryChanged()
        {
            if (!IsInstanceValid(this))
            {
                return;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                UpdateSlot(i);
            }
        }

        public void ToggleInventory()
        {
            if (LocalPlayer?.Inventory == null)
            {
                FindLocalPlayerInventorySystem();

                if (LocalPlayer?.Inventory == null)
                {
                    return;
                }
            }

            if (Visible)
            {
                RouterContext.Close(this);
            }
            else
            {
                RouterContext.Open(this);
            }

            if (Visible)
            {
                PlayerInput?.AddBlocker("inventory");

                OnInventoryChanged();
                UpdatePropertiesList();
            }
            else
            {
                PlayerInput?.RemoveBlocker("inventory");
            }
        }

        #endregion

        #region Core - Character panel

        public void UpdateCharacterInfo()
        {
            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                return;
            }

            CharacterNameLabel.Text = $"Jogador #{LocalPlayer.PeerId}";
            CharacterHealthLabel.Text = $"Vida: {LocalPlayer.CurrentHealth}/{LocalPlayer.GetMaxHealth()}";
        }

        public void UpdateCharacterSprite()
        {
            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer) || LocalPlayer.Sprite == null)
            {
                return;
            }

            var playerAnimation = LocalPlayer.Sprite.Animation;

            if (CharacterSprite.Animation != playerAnimation || !CharacterSprite.IsPlaying())
            {
                CharacterSprite.Play(playerAnimation);
            }

            CharacterSprite.Frame = LocalPlayer.Sprite.Frame;
            CharacterSprite.FlipH = LocalPlayer.FacingLeft();
        }

        public void UpdatePropertiesList()
        {
            if (BuffsListContainer == null)
            {
                return;
            }

            foreach (Node child in BuffsListContainer.GetChildren())
            {
                if (child == EmptyPropertyLabelTemplate || child == PropertyLabelTemplate)
                {
                    continue;
                }

                BuffsListContainer.RemoveChild(child);

                child.QueueFree();
            }

            Godot.Collections.Array<BasePropertyData> properties = null;

            if (LocalPlayer != null)
            {
                properties = new Godot.Collections.Array<BasePropertyData>();

                foreach (var property in LocalPlayer.ActiveProperties)
                {
                    properties.Add(property);
                }

                foreach (var property in LocalPlayer.ActiveProperties)
                {
                    properties.Add(property);
                }

                var equippedInstance = LocalPlayer.EquippedInstance();

                if (equippedInstance != null)
                {
                    foreach (var property in equippedInstance.Properties)
                    {
                        properties.Add(property);
                    }
                }
            }

            var lines = new List<string>();

            if (properties != null)
            {
                foreach (var damage in Resolver.Resolve(properties.OfType<DamagePropertyData>().ToList()))
                {
                    lines.Add(PropertyDescriptionFactory.Describe(damage));
                }

                foreach (var resistance in Resolver.Resolve(properties.OfType<DamageResistencePropertyData>().ToList()))
                {
                    lines.Add(PropertyDescriptionFactory.Describe(resistance));
                }

                foreach (var multiplier in Resolver.Resolve(properties.OfType<DamageResistenceMultiplierPropertyData>().ToList()))
                {
                    lines.Add(PropertyDescriptionFactory.Describe(multiplier));
                }

                var critList = properties.OfType<CritPropertyData>().ToList();

                if (critList.Count > 0)
                {
                    lines.Add(PropertyDescriptionFactory.Describe(Resolver.Resolve(critList)));
                }

                var movementList = properties.OfType<MovementPropertyData>().ToList();

                if (movementList.Count > 0)
                {
                    lines.Add(PropertyDescriptionFactory.Describe(Resolver.Resolve(movementList)));
                }

                var healthList = properties.OfType<HealthPropertyData>().ToList();

                if (healthList.Count > 0)
                {
                    lines.Add(PropertyDescriptionFactory.Describe(Resolver.Resolve(healthList)));
                }

                var attackList = properties.OfType<AttackPropertyData>().ToList();

                if (attackList.Count > 0)
                {
                    lines.Add(PropertyDescriptionFactory.Describe(Resolver.Resolve(attackList)));
                }

                var dashList = properties.OfType<DashPropertyData>().ToList();

                if (dashList.Count > 0)
                {
                    lines.Add(PropertyDescriptionFactory.Describe(Resolver.Resolve(dashList)));
                }
            }

            lines.RemoveAll(string.IsNullOrEmpty);

            if (lines.Count == 0)
            {
                if (EmptyPropertyLabelTemplate == null)
                {
                    GD.PushError("InventoryUI: EmptyPropertyLabelTemplate não encontrado, não é possível mostrar a list de propriedades.");

                    return;
                }

                var empty = (Label)EmptyPropertyLabelTemplate.Duplicate();
                empty.Visible = true;

                BuffsListContainer.AddChild(empty);

                return;
            }

            foreach (var text in lines)
            {
                if (PropertyLabelTemplate == null)
                {
                    GD.PushError("InventoryUI: PropertyLabelTemplate não encontrado, não é possível mostrar a list de propriedades.");

                    continue;
                }

                var label = (Label)PropertyLabelTemplate.Duplicate();

                label.Text = text;
                label.Visible = true;

                BuffsListContainer.AddChild(label);
            }
        }

        #endregion
    }
}
