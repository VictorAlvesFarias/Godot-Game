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
		#region Properties

		public Player LocalPlayer { get; set; }
		public PlayerInput PlayerInput => LocalPlayer?.Input;
		public Panel[] SlotPanels { get; set; } = new Panel[128];
		public TextureRect[] IconRects { get; set; } = new TextureRect[128];
		public Label[] QuantityLabels { get; set; } = new Label[128];
		public Label[] NameLabels { get; set; } = new Label[128];
		public Label[] NumLabels { get; set; } = new Label[128];
		public int SelectedSlotIndex { get; set; } = -1;

		public bool IsDragging { get; set; } = false;
		public int DraggedSlotIndex { get; set; } = -1;
		public long DraggedInstanceId { get; set; } = 0;
		public Control DragPreview { get; set; }
		public Vector2 DragOffset { get; set; }

		#endregion

		#region Node children references


		#endregion

		#region Godot implementation

		public override void _UnhandledInput(InputEvent @event)
		{
			if (Game.Ui.InventoryUI.ContextMenu.Node == null)
			{
				return;
			}

			if (Game.Ui.InventoryUI.ContextMenu.Node.Visible &&
				@event is InputEventMouseButton mouseEvent &&
				mouseEvent.Pressed &&
				mouseEvent.ButtonIndex == MouseButton.Left)
			{
				var rect = Game.Ui.InventoryUI.ContextMenu.Node.GetGlobalRect();

				if (!rect.HasPoint(mouseEvent.GlobalPosition))
				{
					Game.Ui.InventoryUI.ContextMenu.Node.Visible = false;
				}
			}
		}

		public override bool IsOverlay => true;

		public override void _Ready()
		{

			Game.WhenReady(Initialize);
		}

		#endregion

		#region Core - Setup

		private void Initialize()
		{
			Game.Ui.InventoryUI.GridContainer.Node.Columns = 8;

			Game.Ui.InventoryUI.EquiparButtonTemplate.Node.Visible = false;
			Game.Ui.InventoryUI.EmptyPropertyLabelTemplate.Node.Visible = false;
			Game.Ui.InventoryUI.PropertyLabelTemplate.Node.Visible = false;

			Game.Ui.InventoryUI.CharacterSprite.Node.Play("idle");

			CreateDragPreview();

			FindLocalPlayerInventorySystem();
		}

		#endregion

		#region Godot implementation

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
				else if (Game.Ui.InventoryUI.DropSlot.Node != null && Game.Ui.InventoryUI.DropSlot.Node.GetGlobalRect().HasPoint(mouseEvent.GlobalPosition))
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
			var template = Game.Ui.InventoryUI.DragPreviewTemplate.Node;

			if (template == null)
			{
				GD.PushError("InventoryUI: DragPreviewTemplate não encontrado em Root.");
				return;
			}

			template.Visible = false;

			DragPreview = (Panel)template.Duplicate();
			DragPreview.Visible = false;

			Game.Ui.InventoryUI.MainControl.Node.AddChild(DragPreview);
		}

		public void FindLocalPlayerInventorySystem()
		{
			if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
			{
				LocalPlayer.InventoryChanged -= OnInventoryChanged;
			}
			LocalPlayer = null;

			var worldManager = Game.Managers.WorldManager.Node;

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

					UpdatePropertiesList();
				}
			}
		}

		public void InitializeSlots()
		{
			var hotbarTemplate = (Panel)Game.Ui.InventoryUI.HotbarRow.Node.GetChild(0).Duplicate();
			var gridTemplate = (Panel)Game.Ui.InventoryUI.GridContainer.Node.GetChild(0).Duplicate();

			foreach (Node child in Game.Ui.InventoryUI.HotbarRow.Node.GetChildren())
			{
				Game.Ui.InventoryUI.HotbarRow.Node.RemoveChild(child);
				child.QueueFree();
			}

			foreach (Node child in Game.Ui.InventoryUI.GridContainer.Node.GetChildren())
			{
				Game.Ui.InventoryUI.GridContainer.Node.RemoveChild(child);
				child.QueueFree();
			}

			for (int i = 0; i < 128; i++)
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
			for (int i = 0; i < 128; i++)
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
				Game.Ui.InventoryUI.HotbarRow.Node.AddChild(SlotPanels[index]);
			}
			else
			{
				Game.Ui.InventoryUI.GridContainer.Node.AddChild(SlotPanels[index]);
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

			if (DraggedSlotIndex >= 0 && DraggedSlotIndex < 128 && IconRects[DraggedSlotIndex] != null)
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

			if (DraggedSlotIndex >= 0 && DraggedSlotIndex < 128 && IconRects[DraggedSlotIndex] != null)
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

			if (DraggedSlotIndex >= 0 && DraggedSlotIndex < 128 && IconRects[DraggedSlotIndex] != null)
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
			if (instanceId <= 0 || toIndex < 0 || toIndex >= 128)
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

			foreach (Node child in Game.Ui.InventoryUI.ContextMenuContainer.Node.GetChildren())
			{
				if (child == Game.Ui.InventoryUI.EquiparButtonTemplate.Node)
				{
					continue;
				}

				Game.Ui.InventoryUI.ContextMenuContainer.Node.RemoveChild(child);
				child.QueueFree();
			}

			if (definition != null)
			{
				if (Game.Ui.InventoryUI.EquiparButtonTemplate.Node == null)
				{
					GD.PushError("InventoryUI: Game.Ui.InventoryUI.EquiparButtonTemplate.Node não encontrado, não é possível montar o menu de contexto.");
				}
				else
				{
					var button = (Button)Game.Ui.InventoryUI.EquiparButtonTemplate.Node.Duplicate();
					button.Visible = true;
					button.Pressed += () => OnContextMenuOption("Equipar");

					Game.Ui.InventoryUI.ContextMenuContainer.Node.AddChild(button);
				}
			}

			var minSize = Game.Ui.InventoryUI.ContextMenuContainer.Node.GetCombinedMinimumSize();
			Game.Ui.InventoryUI.ContextMenu.Node.CustomMinimumSize = new Vector2(Mathf.Max(120f, (float)minSize.X), (float)minSize.Y);
			Game.Ui.InventoryUI.ContextMenu.Node.Size = Game.Ui.InventoryUI.ContextMenu.Node.CustomMinimumSize;

			Game.Ui.InventoryUI.ContextMenu.Node.GlobalPosition = position;
			Game.Ui.InventoryUI.ContextMenu.Node.Visible = true;
			Game.Ui.InventoryUI.ContextMenu.Node.MoveToFront();
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

			Game.Ui.InventoryUI.ContextMenu.Node.Visible = false;
		}

		#endregion

		#region Core - State

		public void OnInventoryChanged()
		{
			if (!IsInstanceValid(this))
			{
				return;
			}

			for (int i = 0; i < 128; i++)
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
				Game.Managers.RouterManager.Node.Close(this);
			}
			else
			{
				Game.Managers.RouterManager.Node.Open(this);
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

			Game.Ui.InventoryUI.CharacterNameLabel.Node.Text = $"Jogador #{LocalPlayer.PeerId}";
			Game.Ui.InventoryUI.CharacterHealthLabel.Node.Text = $"Vida: {LocalPlayer.CurrentHealth}/{LocalPlayer.GetMaxHealth()}";
		}

		public void UpdateCharacterSprite()
		{
			if (LocalPlayer == null || !IsInstanceValid(LocalPlayer) || LocalPlayer.Sprite == null)
			{
				return;
			}

			var playerAnimation = LocalPlayer.Sprite.Animation;

			if (Game.Ui.InventoryUI.CharacterSprite.Node.Animation != playerAnimation || !Game.Ui.InventoryUI.CharacterSprite.Node.IsPlaying())
			{
				Game.Ui.InventoryUI.CharacterSprite.Node.Play(playerAnimation);
			}

			Game.Ui.InventoryUI.CharacterSprite.Node.Frame = LocalPlayer.Sprite.Frame;
			Game.Ui.InventoryUI.CharacterSprite.Node.FlipH = LocalPlayer.FacingLeft();
		}

		public void UpdatePropertiesList()
		{
			if (Game.Ui.InventoryUI.BuffsListContainer.Node == null)
			{
				return;
			}

			foreach (Node child in Game.Ui.InventoryUI.BuffsListContainer.Node.GetChildren())
			{
				if (child == Game.Ui.InventoryUI.EmptyPropertyLabelTemplate.Node || child == Game.Ui.InventoryUI.PropertyLabelTemplate.Node)
				{
					continue;
				}

				Game.Ui.InventoryUI.BuffsListContainer.Node.RemoveChild(child);

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
					lines.Add(DescribeProperty(damage));
				}

				foreach (var resistance in Resolver.Resolve(properties.OfType<DamageResistencePropertyData>().ToList()))
				{
					lines.Add(DescribeProperty(resistance));
				}

				foreach (var multiplier in Resolver.Resolve(properties.OfType<DamageResistenceMultiplierPropertyData>().ToList()))
				{
					lines.Add(DescribeProperty(multiplier));
				}

				var critList = properties.OfType<CritPropertyData>().ToList();

				if (critList.Count > 0)
				{
					lines.Add(DescribeProperty(Resolver.Resolve(critList)));
				}

				var movementList = properties.OfType<MovementPropertyData>().ToList();

				if (movementList.Count > 0)
				{
					lines.Add(DescribeProperty(Resolver.Resolve(movementList)));
				}

				var healthList = properties.OfType<HealthPropertyData>().ToList();

				if (healthList.Count > 0)
				{
					lines.Add(DescribeProperty(Resolver.Resolve(healthList)));
				}

				var attackList = properties.OfType<AttackPropertyData>().ToList();

				if (attackList.Count > 0)
				{
					lines.Add(DescribeProperty(Resolver.Resolve(attackList)));
				}

				var dashList = properties.OfType<DashPropertyData>().ToList();

				if (dashList.Count > 0)
				{
					lines.Add(DescribeProperty(Resolver.Resolve(dashList)));
				}
			}

			lines.RemoveAll(string.IsNullOrEmpty);

			if (lines.Count == 0)
			{
				if (Game.Ui.InventoryUI.EmptyPropertyLabelTemplate.Node == null)
				{
					GD.PushError("InventoryUI: Game.Ui.InventoryUI.EmptyPropertyLabelTemplate.Node não encontrado, não é possível mostrar a lista de propriedades.");

					return;
				}

				var empty = (Label)Game.Ui.InventoryUI.EmptyPropertyLabelTemplate.Node.Duplicate();
				empty.Visible = true;

				Game.Ui.InventoryUI.BuffsListContainer.Node.AddChild(empty);

				return;
			}

			foreach (var text in lines)
			{
				if (Game.Ui.InventoryUI.PropertyLabelTemplate.Node == null)
				{
					GD.PushError("InventoryUI: Game.Ui.InventoryUI.PropertyLabelTemplate.Node não encontrado, não é possível mostrar a lista de propriedades.");

					continue;
				}

				var label = (Label)Game.Ui.InventoryUI.PropertyLabelTemplate.Node.Duplicate();

				label.Text = text;
				label.Visible = true;

				Game.Ui.InventoryUI.BuffsListContainer.Node.AddChild(label);
			}
		}

		public string DescribeProperty(BasePropertyData property)
		{
			return PropertyDescriptionFactory.Describe(property);
		}

		#endregion
	}
}
