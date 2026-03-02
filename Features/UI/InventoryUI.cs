using Godot;
using System;
using System.Collections.Generic;
using Jogo25D.Systems;
using Jogo25D.Items;
using Jogo25D.Constants;

namespace Jogo25D.UI
{
	public partial class InventoryUI : CanvasLayer
	{
		private Inventory inventory;
		private GridContainer gridContainer;
		private Panel contextMenu;
		private VBoxContainer contextMenuContainer;
		private Panel[] slots = new Panel[16];
		private Panel[] selectedBorders = new Panel[16];
		private TextureRect[] iconRects = new TextureRect[16];
		private Label[] quantityLabels = new Label[16];
		private Label[] nameLabels = new Label[16];
		private int selectedSlotIndex = -1;
		private Control mainControl;

		private bool isDragging = false;
		private int draggedSlotIndex = -1;
		private Control dragPreview;
		private Vector2 dragOffset;
		private const float DragThreshold = 5.0f;

		public override void _UnhandledInput(InputEvent @event)
		{
			if (contextMenu == null)
			{
				return;
			}

			if (contextMenu.Visible &&
				@event is InputEventMouseButton mouseEvent &&
				mouseEvent.Pressed &&
				mouseEvent.ButtonIndex == MouseButton.Left)
			{
				var rect = contextMenu.GetGlobalRect();

				if (!rect.HasPoint(mouseEvent.GlobalPosition))
				{
					contextMenu.Visible = false;
				}
			}
		}

		public override void _Ready()
		{
			mainControl = GetNode<Control>(NodePaths.InventoryUI.Root);
			gridContainer = GetNode<GridContainer>(NodePaths.InventoryUI.GridContainer);

			contextMenu = GetNode<Panel>(NodePaths.InventoryUI.ContextMenuPanel);
			contextMenuContainer = GetNode<VBoxContainer>(NodePaths.InventoryUI.ContextMenuVBox);

			CreateDragPreview();

			CallDeferred(nameof(FindLocalPlayerInventorySystem));

			Visible = false;
		}

		private void CreateDragPreview()
		{
			dragPreview = new Panel();
			dragPreview.CustomMinimumSize = new Vector2(64, 64);
			dragPreview.Visible = false;
			dragPreview.ZIndex = 100;
			dragPreview.MouseFilter = Control.MouseFilterEnum.Ignore;

			var styleBox = new StyleBoxFlat();
			styleBox.BgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
			styleBox.BorderColor = Colors.White;
			styleBox.BorderWidthLeft = 2;
			styleBox.BorderWidthRight = 2;
			styleBox.BorderWidthTop = 2;
			styleBox.BorderWidthBottom = 2;
			dragPreview.AddThemeStyleboxOverride("panel", styleBox);

			var iconRect = new TextureRect();
			iconRect.Name = NodePaths.InventoryUI.DragPreviewIcon;
			iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			dragPreview.AddChild(iconRect);

			mainControl.AddChild(dragPreview);
		}

		private void FindLocalPlayerInventorySystem()
		{
			if (inventory != null && IsInstanceValid(inventory))
			{
				inventory.InventoryChanged -= OnInventoryChanged;
			}
			inventory = null;

			var players = GetTree().GetNodesInGroup("players");
			var localPeerId = 1;
			var hasMultiplayer = false;

			if (
				Multiplayer != null && 
				Multiplayer.MultiplayerPeer != null &&
				Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected
			)
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
						inventory = player.Inventory;
						if (inventory != null)
						{
							inventory.InventoryChanged += OnInventoryChanged;

							if (slots[0] == null)
							{
								InitializeSlots();
							}
							else
							{
								OnInventoryChanged();
							}
						}
						break;
					}
				}
			}
		}

		private void InitializeSlots()
		{
			// Se já existe um slot na cena (para visualização no editor), usá-lo como slot 0
			if (gridContainer.GetChildCount() > 0)
			{
				var existingSlot = gridContainer.GetChild<Panel>(0);
				slots[0] = existingSlot;

				var margin = existingSlot.GetNode<MarginContainer>(NodePaths.InventoryUI.SlotMarginContainer);
				var center = margin.GetNode<CenterContainer>(NodePaths.InventoryUI.SlotCenterContainer);
				iconRects[0] = center.GetNode<TextureRect>(NodePaths.InventoryUI.SlotIcon);
				nameLabels[0] = center.GetNode<Label>(NodePaths.InventoryUI.SlotNameLabel);
				quantityLabels[0] = existingSlot.GetNode<Label>(NodePaths.InventoryUI.SlotQuantityLabel);

				int slotIndex = 0;
				existingSlot.GuiInput += (InputEvent @event) => OnSlotInput(slotIndex, @event);

				for (int i = 1; i < 16; i++)
					SetupSlot(i);
			}
			else
			{
				for (int i = 0; i < 16; i++)
					SetupSlot(i);
			}

			OnInventoryChanged();
		}

		public override void _ExitTree()
		{
			if (inventory != null && IsInstanceValid(inventory))
			{
				inventory.InventoryChanged -= OnInventoryChanged;
			}
		}

		public override void _Process(double delta)
		{
			if (isDragging && dragPreview != null)
			{
				dragPreview.GlobalPosition = GetViewport().GetMousePosition() + dragOffset;
			}
		}

		public override void _Input(InputEvent @event)
		{
			if (isDragging && @event is InputEventMouseButton mouseEvent &&
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

			if (@event.IsActionPressed("ui_cancel") && isDragging)
			{
				CancelDrag();
				GetViewport().SetInputAsHandled();
				return;
			}

			if (InputManager.Instance != null && InputManager.Instance.IsBlocked)
			{
				return;
			}

			if (@event.IsActionPressed("toggle_inventory") && !@event.IsEcho())
			{
				if (isDragging)
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

		private int GetSlotAtPosition(Vector2 globalPosition)
		{
			for (int i = 0; i < 16; i++)
			{
				if (slots[i] != null && slots[i].GetGlobalRect().HasPoint(globalPosition))
				{
					return i;
				}
			}
			return -1;
		}

		private void SetupSlot(int index)
		{
			slots[index] = new Panel();
			slots[index].CustomMinimumSize = new Vector2(64, 64);
			gridContainer.AddChild(slots[index]);

			var marginContainer = new MarginContainer();
			marginContainer.AddThemeConstantOverride("margin_left", 4);
			marginContainer.AddThemeConstantOverride("margin_top", 4);
			marginContainer.AddThemeConstantOverride("margin_right", 4);
			marginContainer.AddThemeConstantOverride("margin_bottom", 4);
			slots[index].AddChild(marginContainer);

			var centerContainer = new CenterContainer();
			marginContainer.AddChild(centerContainer);

			iconRects[index] = new TextureRect();
			iconRects[index].ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRects[index].StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRects[index].CustomMinimumSize = new Vector2(56, 56);
			centerContainer.AddChild(iconRects[index]);

			nameLabels[index] = new Label();
			nameLabels[index].HorizontalAlignment = HorizontalAlignment.Center;
			nameLabels[index].VerticalAlignment = VerticalAlignment.Center;
			nameLabels[index].AutowrapMode = TextServer.AutowrapMode.Word;
			nameLabels[index].Visible = false;
			centerContainer.AddChild(nameLabels[index]);

			quantityLabels[index] = new Label();
			quantityLabels[index].HorizontalAlignment = HorizontalAlignment.Right;
			quantityLabels[index].VerticalAlignment = VerticalAlignment.Bottom;
			slots[index].AddChild(quantityLabels[index]);

			int slotIndex = index;
			slots[index].GuiInput += (InputEvent @event) => OnSlotInput(slotIndex, @event);
		}

		private void OnSlotInput(int slotIndex, InputEvent @event)
		{
			if (inventory == null || !IsInstanceValid(inventory)) return;

			var slot = inventory.GetSlot(slotIndex);

			if (@event is InputEventMouseButton mouseEvent)
			{
				if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed && !slot.IsEmpty())
				{
					StartDrag(slotIndex, mouseEvent.GlobalPosition);
					slots[slotIndex].AcceptEvent();
				}
				else if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed && !slot.IsEmpty())
				{
					ShowContextMenuForSlot(slotIndex, mouseEvent.GlobalPosition);
					slots[slotIndex].AcceptEvent();
				}
			}
		}

		private void StartDrag(int slotIndex, Vector2 mousePos)
		{
			if (inventory == null || !IsInstanceValid(inventory)) return;

			var slot = inventory.GetSlot(slotIndex);
			if (slot.IsEmpty()) return;

			GD.Print($"StartDrag: iniciando arrasto do slot {slotIndex} ({slot.Definition?.Name})");

			isDragging = true;
			draggedSlotIndex = slotIndex;

			if (dragPreview != null)
			{
				var iconRect = dragPreview.GetNode<TextureRect>(NodePaths.InventoryUI.DragPreviewIcon);
				iconRect.Texture = slot.Definition?.Icon;

				dragOffset = new Vector2(-32, -32);
				dragPreview.GlobalPosition = mousePos + dragOffset;
				dragPreview.Visible = true;

				if (iconRects[slotIndex] != null)
				{
					iconRects[slotIndex].Modulate = new Color(1, 1, 1, 0.5f);
				}
			}
		}

		private void EndDrag(int targetSlotIndex)
		{
			if (!isDragging || draggedSlotIndex < 0) return;

			GD.Print($"EndDrag: arrastado slot {draggedSlotIndex} para slot {targetSlotIndex}");

			if (dragPreview != null)
			{
				dragPreview.Visible = false;
			}

			if (draggedSlotIndex >= 0 && draggedSlotIndex < 16 && iconRects[draggedSlotIndex] != null)
			{
				iconRects[draggedSlotIndex].Modulate = Colors.White;
			}

			if (targetSlotIndex != draggedSlotIndex && inventory != null)
			{
				SwapItems(draggedSlotIndex, targetSlotIndex);
			}

			isDragging = false;
			draggedSlotIndex = -1;
		}

		private void CancelDrag()
		{
			if (!isDragging) return;

			if (dragPreview != null)
			{
				dragPreview.Visible = false;
			}

			if (draggedSlotIndex >= 0 && draggedSlotIndex < 16 && iconRects[draggedSlotIndex] != null)
			{
				iconRects[draggedSlotIndex].Modulate = Colors.White;
			}

			isDragging = false;
			draggedSlotIndex = -1;
		}

		private void SwapItems(int fromIndex, int toIndex)
		{
			if (inventory == null || !IsInstanceValid(inventory)) return;
			if (fromIndex < 0 || fromIndex >= 16 || toIndex < 0 || toIndex >= 16) return;

			inventory.SwapSlots(fromIndex, toIndex);
		}

		private void UpdateSlot(int index)
		{
			if (inventory == null || !IsInstanceValid(inventory))
				return;

			if (iconRects == null || nameLabels == null || quantityLabels == null)
				return;

			if (index < 0
				|| index >= iconRects.Length
				|| index >= nameLabels.Length
				|| index >= quantityLabels.Length)
				return;

			var slot = inventory.GetSlot(index);
			if (slot == null)
				return;

			if (slot.IsEmpty() || slot.Definition == null)
			{
				iconRects[index].Texture = null;
				nameLabels[index].Visible = false;
				quantityLabels[index].Text = "";
				return;
			}

			if (slot.Definition.Icon != null)
			{
				iconRects[index].Texture = slot.Definition.Icon;
				nameLabels[index].Visible = false;
			}
			else
			{
				iconRects[index].Texture = null;
				nameLabels[index].Text = slot.Definition.Name;
				nameLabels[index].Visible = true;
			}

			if (slot.Definition.Stackable && slot.Quantity > 1)
				quantityLabels[index].Text = slot.Quantity.ToString();
			else
				quantityLabels[index].Text = "";
		}

		private void SelectSlot(int index)
		{
			if (selectedSlotIndex >= 0 && selectedSlotIndex < 16)
			{
				selectedBorders[selectedSlotIndex].Visible = false;
			}

			selectedSlotIndex = index;

			if (selectedSlotIndex >= 0 && selectedSlotIndex < 16)
			{
				selectedBorders[selectedSlotIndex].Visible = true;
			}
		}

		private void ShowContextMenuForSlot(int slotIndex, Vector2 position)
		{
			if (inventory == null || !IsInstanceValid(inventory)) return;

			var slot = inventory.GetSlot(slotIndex);
			if (slot.IsEmpty()) return;

			selectedSlotIndex = slotIndex;

			foreach (Node child in contextMenuContainer.GetChildren())
			{
				contextMenuContainer.RemoveChild(child);
				child.QueueFree();
			}

			if (slot.Definition != null && slot.Definition.IsEquippable)
			{
				var button = new Button();
				button.Text = "Equipar";
				button.CustomMinimumSize = new Vector2(120, 30);
				button.Alignment = HorizontalAlignment.Center;
				button.MouseFilter = Control.MouseFilterEnum.Stop;
				button.Pressed += () => OnContextMenuOption("Equipar");
				contextMenuContainer.AddChild(button);
			}

			// Redimensiona o painel ao conteúdo (não ao tamanho do inventário)
			var minSize = contextMenuContainer.GetCombinedMinimumSize();
			contextMenu.CustomMinimumSize = new Vector2(Mathf.Max(120, (float)minSize.X), (float)minSize.Y);
			contextMenu.Size = contextMenu.CustomMinimumSize;

			contextMenu.GlobalPosition = position;
			contextMenu.Visible = true;
			contextMenu.MoveToFront();
		}

		private void OnContextMenuOption(string option)
		{
			if (selectedSlotIndex < 0 || inventory == null || !IsInstanceValid(inventory)) return;

			var slot = inventory.GetSlot(selectedSlotIndex);
			if (slot.IsEmpty()) return;

			if (option == "Equipar")
			{
				inventory.Rpc(nameof(Inventory.EquipItem), selectedSlotIndex);
			}

			contextMenu.Visible = false;
		}

		private void OnInventoryChanged()
		{
			if (!IsInstanceValid(this)) return;

			for (int i = 0; i < 16; i++)
			{
				UpdateSlot(i);
			}
		}

		public void ToggleInventory()
		{
			if (inventory == null || !IsInstanceValid(inventory))
			{
				FindLocalPlayerInventorySystem();

				if (inventory == null || !IsInstanceValid(inventory))
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

		public void AddItemToInventory(ItemDefinition definition, int quantity = 1)
		{
			if (inventory != null && IsInstanceValid(inventory))
			{
				inventory.AddItem(definition, quantity);
			}
		}
	}
}
