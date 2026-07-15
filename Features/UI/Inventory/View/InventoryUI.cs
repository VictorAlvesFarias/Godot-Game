using Godot;
using System;
using System.Collections.Generic;
using Jogo25D.Characters;
using Jogo25D.Systems;
using Jogo25D.Items;

namespace Jogo25D.UI
{
	public partial class InventoryUI : CanvasLayer
	{
		public Player LocalPlayer { get; set; }
		public Inventory Inventory => LocalPlayer?.Inventory;
		public PlayerInput PlayerInput => LocalPlayer?.Input;
		public GridContainer GridContainer { get; set; }
		public Panel ContextMenu { get; set; }
		public VBoxContainer ContextMenuContainer { get; set; }
		public Panel[] SlotPanels { get; set; } = new Panel[16];
		public Panel[] SelectedBorders { get; set; } = new Panel[16];
		public TextureRect[] IconRects { get; set; } = new TextureRect[16];
		public Label[] QuantityLabels { get; set; } = new Label[16];
		public Label[] NameLabels { get; set; } = new Label[16];
		public Label[] NumLabels { get; set; } = new Label[16];
		public int SelectedSlotIndex { get; set; } = -1;
		public Control MainControl { get; set; }

		public bool IsDragging { get; set; } = false;
		public int DraggedSlotIndex { get; set; } = -1;
		public Control DragPreview { get; set; }
		public Vector2 DragOffset { get; set; }

		public const float DragThreshold = 5.0f;

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
			MainControl = GetNode<Control>("CenterContainer");
			GridContainer = GetNode<GridContainer>("CenterContainer/MainPanel/MarginContainer/VBoxContainer/GridContainer");

			ContextMenu = GetNode<Panel>("ContextMenu");
			ContextMenuContainer = GetNode<VBoxContainer>("ContextMenu/MarginContainer/VBoxContainer");

			CreateDragPreview();

			CallDeferred(nameof(FindLocalPlayerInventorySystem));

			Visible = false;
		}

		public void CreateDragPreview()
		{
			DragPreview = new Panel();
			DragPreview.CustomMinimumSize = new Vector2(64, 64);
			DragPreview.Visible = false;
			DragPreview.ZIndex = 100;
			DragPreview.MouseFilter = Control.MouseFilterEnum.Ignore;

			var styleBox = new StyleBoxFlat();
			styleBox.BgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
			styleBox.BorderColor = Colors.White;
			styleBox.BorderWidthLeft = 2;
			styleBox.BorderWidthRight = 2;
			styleBox.BorderWidthTop = 2;
			styleBox.BorderWidthBottom = 2;
			DragPreview.AddThemeStyleboxOverride("panel", styleBox);

			var iconRect = new TextureRect();
			iconRect.Name = "Icon";
			iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            iconRect.TextureFilter = Control.TextureFilterEnum.Nearest;
            iconRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			DragPreview.AddChild(iconRect);

			MainControl.AddChild(DragPreview);
		}

		public void FindLocalPlayerInventorySystem()
		{
			if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
			{
				LocalPlayer.InventoryChanged -= OnInventoryChanged;
			}
			LocalPlayer = null;

			var worldManager = GetTree().Root.GetNodeOrNull<WorldManager>(WorldManager.DEFAULT_NODE_PATH);

			if (worldManager != null)
			{
				LocalPlayer = worldManager.GetLocalPlayer();

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
				}
			}
		}

		public void InitializeSlots()
		{
			if (GridContainer.GetChildCount() > 0)
			{
				var existingSlot = GridContainer.GetChild<Panel>(0);
				SlotPanels[0] = existingSlot;

				var margin = existingSlot.GetNode<MarginContainer>("MarginContainer");
				var center = margin.GetNode<CenterContainer>("CenterContainer");
				IconRects[0] = center.GetNode<TextureRect>("IconRect");
				NameLabels[0] = center.GetNode<Label>("NameLabel");
				QuantityLabels[0] = existingSlot.GetNode<Label>("QuantityLabel");
				NumLabels[0] = existingSlot.GetNode<Label>("NumLabel");
				NumLabels[0].Text = "1";

				int slotIndex = 0;
				existingSlot.GuiInput += (InputEvent @event) => OnSlotInput(slotIndex, @event);

				for (int i = 1; i < 16; i++)
				{
					SetupSlot(i);
				}
			}
			else
			{
				for (int i = 0; i < 16; i++)
				{
					SetupSlot(i);
				}
			}

			OnInventoryChanged();
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

			if (PlayerInput != null && PlayerInput.IsBlocked())
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

		public int GetSlotAtPosition(Vector2 globalPosition)
		{
			for (int i = 0; i < 16; i++)
			{
				if (SlotPanels[i] != null && SlotPanels[i].GetGlobalRect().HasPoint(globalPosition))
				{
					return i;
				}
			}
			return -1;
		}

		public void SetupSlot(int index)
		{
			SlotPanels[index] = new Panel();
			SlotPanels[index].CustomMinimumSize = new Vector2(64, 64);
			GridContainer.AddChild(SlotPanels[index]);

			var marginContainer = new MarginContainer();
			marginContainer.AddThemeConstantOverride("margin_left", 4);
			marginContainer.AddThemeConstantOverride("margin_top", 4);
			marginContainer.AddThemeConstantOverride("margin_right", 4);
			marginContainer.AddThemeConstantOverride("margin_bottom", 4);
			SlotPanels[index].AddChild(marginContainer);

			var centerContainer = new CenterContainer();
			marginContainer.AddChild(centerContainer);

			IconRects[index] = new TextureRect();
			IconRects[index].ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			IconRects[index].StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			IconRects[index].CustomMinimumSize = new Vector2(56, 56);
			centerContainer.AddChild(IconRects[index]);

			NameLabels[index] = new Label();
			NameLabels[index].HorizontalAlignment = HorizontalAlignment.Center;
			NameLabels[index].VerticalAlignment = VerticalAlignment.Center;
			NameLabels[index].AutowrapMode = TextServer.AutowrapMode.Word;
			NameLabels[index].Visible = false;
			centerContainer.AddChild(NameLabels[index]);

			QuantityLabels[index] = new Label();
			QuantityLabels[index].LayoutMode = 1;
			QuantityLabels[index].AnchorLeft = 1;
			QuantityLabels[index].AnchorTop = 1;
			QuantityLabels[index].AnchorRight = 1;
			QuantityLabels[index].AnchorBottom = 1;
			QuantityLabels[index].OffsetLeft = -33;
			QuantityLabels[index].OffsetTop = -16;
			QuantityLabels[index].OffsetRight = -3;
			QuantityLabels[index].OffsetBottom = -3;
			QuantityLabels[index].GrowHorizontal = Control.GrowDirection.Begin;
			QuantityLabels[index].GrowVertical = Control.GrowDirection.Begin;
			QuantityLabels[index].HorizontalAlignment = HorizontalAlignment.Right;
			QuantityLabels[index].VerticalAlignment = VerticalAlignment.Bottom;
			QuantityLabels[index].AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.3f, 1f));
			QuantityLabels[index].AddThemeColorOverride("font_outline_color", Colors.Black);
			QuantityLabels[index].AddThemeConstantOverride("outline_size", 2);
			QuantityLabels[index].AddThemeFontSizeOverride("font_size", 10);
			SlotPanels[index].AddChild(QuantityLabels[index]);

			NumLabels[index] = new Label();
			NumLabels[index].Text = $"{index + 1}";
			NumLabels[index].LayoutMode = 1;
			NumLabels[index].OffsetLeft = 3;
			NumLabels[index].OffsetTop = 2;
			NumLabels[index].OffsetRight = 23;
			NumLabels[index].OffsetBottom = 16;
			NumLabels[index].AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f, 1f));
			NumLabels[index].AddThemeFontSizeOverride("font_size", 9);
			SlotPanels[index].AddChild(NumLabels[index]);

			int slotIndex = index;
			SlotPanels[index].GuiInput += (InputEvent @event) => OnSlotInput(slotIndex, @event);
		}

		public void OnSlotInput(int slotIndex, InputEvent @event)
		{
			if (Inventory == null || !IsInstanceValid(Inventory))
			{
			    return;
			}

			var slot = Inventory.GetSlot(LocalPlayer.Data.Inventory, slotIndex);

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
			if (Inventory == null || !IsInstanceValid(Inventory))
			{
			    return;
			}

            var slot = Inventory.GetSlot(LocalPlayer.Data.Inventory, slotIndex);

			if (slot == null)
			{
			    return;
			}

			var def = ItemDB.Get(slot.Id);

            GD.Print($"StartDrag: iniciando arrasto do slot {slotIndex} ({def?.Name})");

			IsDragging = true;
			DraggedSlotIndex = slotIndex;

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

			if (DraggedSlotIndex >= 0 && DraggedSlotIndex < 16 && IconRects[DraggedSlotIndex] != null)
			{
				IconRects[DraggedSlotIndex].Modulate = Colors.White;
			}

			if (targetSlotIndex != DraggedSlotIndex && Inventory != null)
			{
				SwapItems(DraggedSlotIndex, targetSlotIndex);
			}

			IsDragging = false;
			DraggedSlotIndex = -1;
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

			if (DraggedSlotIndex >= 0 && DraggedSlotIndex < 16 && IconRects[DraggedSlotIndex] != null)
			{
				IconRects[DraggedSlotIndex].Modulate = Colors.White;
			}

			IsDragging = false;
			DraggedSlotIndex = -1;
		}

		public void SwapItems(int fromIndex, int toIndex)
		{
			if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
			{
			    return;
			}
			if (fromIndex < 0 || fromIndex >= 16 || toIndex < 0 || toIndex >= 16)
			{
			    return;
			}

			LocalPlayer.SwapSlotsRequest(fromIndex, toIndex);
		}

		public void UpdateSlot(int index)
		{
			if (Inventory == null || !IsInstanceValid(Inventory))
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

			var slot = Inventory.GetSlot(LocalPlayer.Data.Inventory, index);

			if (slot == null)
			{
				return;
			}

			var definition = ItemDB.Get(slot.Id);

			if (definition == null || definition.IsEmpty(slot))
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

		public void SelectSlot(int index)
		{
			if (SelectedSlotIndex >= 0 && SelectedSlotIndex < 16)
			{
				SelectedBorders[SelectedSlotIndex].Visible = false;
			}

			SelectedSlotIndex = index;

			if (SelectedSlotIndex >= 0 && SelectedSlotIndex < 16)
			{
				SelectedBorders[SelectedSlotIndex].Visible = true;
			}
		}

		public void ShowContextMenuForSlot(int slotIndex, Vector2 position)
		{
			if (Inventory == null || !IsInstanceValid(Inventory))
			{
			    return;
			}

            var slot = Inventory.GetSlot(LocalPlayer.Data.Inventory, slotIndex);

            if (slot == null)
            {
                return;
            }

            var definition = ItemDB.Get(slot.Id);

            if (definition == null || definition.IsEmpty(slot))
			{
			    return;
			}

			SelectedSlotIndex = slotIndex;

			foreach (Node child in ContextMenuContainer.GetChildren())
			{
				ContextMenuContainer.RemoveChild(child);
				child.QueueFree();
			}

			if (definition != null && definition.IsEquippable)
			{
				var button = new Button();
				button.Text = "Equipar";
				button.CustomMinimumSize = new Vector2(120, 30);
				button.Alignment = HorizontalAlignment.Center;
				button.MouseFilter = Control.MouseFilterEnum.Stop;
				button.Pressed += () => OnContextMenuOption("Equipar");
				ContextMenuContainer.AddChild(button);
			}

			var minSize = ContextMenuContainer.GetCombinedMinimumSize();
			ContextMenu.CustomMinimumSize = new Vector2(Mathf.Max(120, (float)minSize.X), (float)minSize.Y);
			ContextMenu.Size = ContextMenu.CustomMinimumSize;

			ContextMenu.GlobalPosition = position;
			ContextMenu.Visible = true;
			ContextMenu.MoveToFront();
		}

		public void OnContextMenuOption(string option)
		{
			if (SelectedSlotIndex < 0 || Inventory == null || !IsInstanceValid(Inventory))
			{
			    return;
			}

			var slot = Inventory.GetSlot(LocalPlayer.Data.Inventory, SelectedSlotIndex);

			if (slot == null)
			{
			    return;
			}

			if (option == "Equipar")
			{
				LocalPlayer.EquipItemRequest(SelectedSlotIndex);
			}

			ContextMenu.Visible = false;
		}

		public void OnInventoryChanged()
		{
			if (!IsInstanceValid(this))
			{
			    return;
			}

			for (int i = 0; i < 16; i++)
			{
				UpdateSlot(i);
			}
		}

		public void ToggleInventory()
		{
			if (Inventory == null || !IsInstanceValid(Inventory))
			{
				FindLocalPlayerInventorySystem();

				if (Inventory == null || !IsInstanceValid(Inventory))
				{
					return;
				}
			}

			Visible = !Visible;

			if (Visible)
			{
				OnInventoryChanged();
			}
		}
	}
}