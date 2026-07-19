using Godot;
using System;
using System.Collections.Generic;
using Jogo25D.Characters;
using Jogo25D.Systems;
using Jogo25D.Items;
using Jogo25D.Properties;

namespace Jogo25D.UI
{
	public partial class InventoryUI : CanvasLayer
	{
		#region Properties

		public const int TotalSlots = 16;
		public const int HotbarSlots = 8;

		public Player LocalPlayer { get; set; }
		public Inventory Inventory => LocalPlayer?.Inventory;
		public PlayerInput PlayerInput => LocalPlayer?.Input;
		public GridContainer GridContainer { get; set; }
		public HBoxContainer HotbarRow { get; set; }
		public Panel DropSlot { get; set; }
		public Panel ContextMenu { get; set; }
		public VBoxContainer ContextMenuContainer { get; set; }
		public Panel[] SlotPanels { get; set; } = new Panel[TotalSlots];
		public TextureRect[] IconRects { get; set; } = new TextureRect[TotalSlots];
		public Label[] QuantityLabels { get; set; } = new Label[TotalSlots];
		public Label[] NameLabels { get; set; } = new Label[TotalSlots];
		public Label[] NumLabels { get; set; } = new Label[TotalSlots];
		public int SelectedSlotIndex { get; set; } = -1;
		public Control MainControl { get; set; }

		public bool IsDragging { get; set; } = false;
		public int DraggedSlotIndex { get; set; } = -1;
		public long DraggedInstanceId { get; set; } = 0;
		public Control DragPreview { get; set; }
		public Vector2 DragOffset { get; set; }

		public const float DragThreshold = 5.0f;

		#endregion

		#region Systems - Character panel

		public AnimatedSprite2D CharacterSprite { get; set; }
		public Label CharacterNameLabel { get; set; }
		public Label CharacterHealthLabel { get; set; }
		public VBoxContainer BuffsListContainer { get; set; }

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
			MainControl = GetNode<Control>("Root");
			ContextMenu = GetNode<Panel>("ContextMenu");
			ContextMenuContainer = GetNode<VBoxContainer>("ContextMenu/MarginContainer/VBoxContainer");

			var mainRow = MainControl.GetNode<HBoxContainer>("Panel/MainPanel/MarginContainer/MainRow");

			DropSlot = MainControl.FindChild("DropSlot", true, false) as Panel;
			HotbarRow = mainRow.GetNode<HBoxContainer>("InventoryColumn/HotbarRow");
			GridContainer = mainRow.GetNode<GridContainer>("InventoryColumn/GridContainer");
			GridContainer.Columns = HotbarSlots;

			CharacterSprite = mainRow.GetNode<AnimatedSprite2D>("StatsColumn/SpriteBox2/VBoxContainer/CenterContainer/CharacterSprite");
			CharacterNameLabel = mainRow.GetNode<Label>("StatsColumn/SpriteBox/MarginContainer/VBoxContainer/CharacterNameLabel");
			CharacterHealthLabel = mainRow.GetNode<Label>("StatsColumn/SpriteBox/MarginContainer/VBoxContainer/CharacterHealthLabel");
			BuffsListContainer = mainRow.GetNode<VBoxContainer>("StatsColumn/SpriteBox/MarginContainer/VBoxContainer/BuffsScroll/BuffsListContainer");

			CharacterSprite.Play("idle");

			CreateDragPreview();

			CallDeferred(nameof(FindLocalPlayerInventorySystem));

			Visible = false;
		}

		public override void _ExitTree()
		{
			if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
			{
				LocalPlayer.InventoryChanged -= OnInventoryChanged;
				LocalPlayer.BuffsChanged -= UpdateBuffsList;
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
				LocalPlayer.BuffsChanged -= UpdateBuffsList;
			}
			LocalPlayer = null;

			var worldManager = GetTree().Root.GetNodeOrNull<WorldManager>(WorldManager.DEFAULT_NODE_PATH);

			if (worldManager != null)
			{
				LocalPlayer = worldManager.GetLocalPlayer();

				if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
				{
					LocalPlayer.InventoryChanged += OnInventoryChanged;
					LocalPlayer.BuffsChanged += UpdateBuffsList;

					if (SlotPanels[0] == null)
					{
						InitializeSlots();
					}
					else
					{
						OnInventoryChanged();
					}

					UpdateBuffsList();
				}
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

			for (int i = 0; i < TotalSlots; i++)
			{
				SetupSlot(i, i < HotbarSlots ? hotbarTemplate : gridTemplate);
			}

			hotbarTemplate.QueueFree();
			gridTemplate.QueueFree();

			OnInventoryChanged();
		}

		#endregion

		#region Core - Drag and drop

		public int GetSlotAtPosition(Vector2 globalPosition)
		{
			for (int i = 0; i < TotalSlots; i++)
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

			if (index < HotbarSlots)
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

			if (DraggedSlotIndex >= 0 && DraggedSlotIndex < TotalSlots && IconRects[DraggedSlotIndex] != null)
			{
				IconRects[DraggedSlotIndex].Modulate = Colors.White;
			}

			if (targetSlotIndex != DraggedSlotIndex && Inventory != null)
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

			if (DraggedSlotIndex >= 0 && DraggedSlotIndex < TotalSlots && IconRects[DraggedSlotIndex] != null)
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

			var slot = Inventory?.FindItem(LocalPlayer.Data.Inventory, DraggedInstanceId);
			var quantity = slot?.Quantity ?? 0;

			if (DragPreview != null)
			{
				DragPreview.Visible = false;
			}

			if (DraggedSlotIndex >= 0 && DraggedSlotIndex < TotalSlots && IconRects[DraggedSlotIndex] != null)
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
			if (instanceId <= 0 || toIndex < 0 || toIndex >= TotalSlots)
			{
				return;
			}

			LocalPlayer.MoveItemRequest(instanceId, toIndex);
		}

		#endregion

		#region Core - Slots

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

			var definition = slot == null ? null : ItemDB.Get(slot.Id);

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

			if (definition != null)
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

			for (int i = 0; i < TotalSlots; i++)
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
				PlayerInput?.AddBlocker("inventory");

				OnInventoryChanged();
				UpdateBuffsList();
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
			if (LocalPlayer == null || !IsInstanceValid(LocalPlayer) || LocalPlayer.Data == null)
			{
				return;
			}

			CharacterNameLabel.Text = $"Jogador #{LocalPlayer.PeerId}";
			CharacterHealthLabel.Text = $"Vida: {LocalPlayer.Data.CurrentHealth}/{LocalPlayer.Data.MaxHealth}";
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
			CharacterSprite.FlipH = LocalPlayer.Sprite.FlipH;
		}

		public void UpdateBuffsList()
		{
			if (BuffsListContainer == null)
			{
				return;
			}

			foreach (Node child in BuffsListContainer.GetChildren())
			{
				BuffsListContainer.RemoveChild(child);
				child.QueueFree();
			}

			var buffs = LocalPlayer?.Data?.Buffs;

			if (buffs == null || buffs.Count == 0)
			{
				var empty = new Label();

				empty.Text = "Nenhum";
				empty.AddThemeFontSizeOverride("font_size", 11);
				empty.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f, 1f));

				BuffsListContainer.AddChild(empty);

				return;
			}

			foreach (var buff in buffs)
			{
				var row = new HBoxContainer();

				var label = new Label();
				label.Text = DescribeBuff(buff);
				label.AddThemeFontSizeOverride("font_size", 11);
				label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				label.AutowrapMode = TextServer.AutowrapMode.Word;
				row.AddChild(label);

				var removeButton = new Button();
				removeButton.Text = "x";
				removeButton.TooltipText = "Remover buff";
				removeButton.CustomMinimumSize = new Vector2(22, 0);
				var buffInstanceId = buff.InstanceId;
				removeButton.Pressed += () => LocalPlayer?.RemoveBuffRequest(buffInstanceId);
				row.AddChild(removeButton);

				BuffsListContainer.AddChild(row);
			}
		}

		public string DescribeBuff(BasePropertyData buff)
		{
			return buff switch
			{
				DamageResistencePropertyData r => $"Resistência a {r.DamageType}: {r.ResistanceFactor:P0}",
				DamageResistenceMultiplierPropertyData m => $"Mult. resistência a {m.DamageType}: x{m.Multiplier:F2}",
				DamagePropertyData d => $"+{d.DamageAmount} dano {d.DamageType} (x{d.DamageMultiplier:F2})",
				CritPropertyData c => $"Crítico: +{c.CritChance:P0} chance, +{c.CritDamage:P0} dano",
				AttackPropertyData => "Bônus de ataque",
				DashPropertyData => "Bônus de dash",
				null => "",
				_ => buff.GetType().Name
			};
		}

		#endregion
	}
}
