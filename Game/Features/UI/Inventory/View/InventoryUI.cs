using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
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
	public partial class InventoryUI : CanvasLayer
	{
		#region Properties

		public Player LocalPlayer { get; set; }
		public Inventory Inventory => LocalPlayer?.Inventory;
		public PlayerInput PlayerInput => LocalPlayer?.Input;
		public GridContainer GridContainer { get; set; }
		public HBoxContainer HotbarRow { get; set; }
		public Panel DropSlot { get; set; }
		public Panel ContextMenu { get; set; }
		public VBoxContainer ContextMenuContainer { get; set; }
		public Panel[] SlotPanels { get; set; } = new Panel[128];
		public TextureRect[] IconRects { get; set; } = new TextureRect[128];
		public Label[] QuantityLabels { get; set; } = new Label[128];
		public Label[] NameLabels { get; set; } = new Label[128];
		public Label[] NumLabels { get; set; } = new Label[128];
		public int SelectedSlotIndex { get; set; } = -1;
		public Control MainControl { get; set; }

		public bool IsDragging { get; set; } = false;
		public int DraggedSlotIndex { get; set; } = -1;
		public long DraggedInstanceId { get; set; } = 0;
		public Control DragPreview { get; set; }
		public Vector2 DragOffset { get; set; }

		#endregion

		#region Node children references

		public AnimatedSprite2D CharacterSprite { get; set; }
		public Label CharacterNameLabel { get; set; }
		public Label CharacterHealthLabel { get; set; }
		public VBoxContainer BuffsListContainer { get; set; }
		public Button EquiparButtonTemplate { get; set; }
		public Label EmptyPropertyLabelTemplate { get; set; }
		public Label PropertyLabelTemplate { get; set; }

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
			GridContainer = mainRow.GetNode<GridContainer>("InventoryColumn/GridScroll/GridContainer");
			GridContainer.Columns = 8;
			CharacterSprite = mainRow.GetNode<AnimatedSprite2D>("StatsColumn/SpriteBox2/VBoxContainer/CenterContainer/CharacterSprite");
			CharacterNameLabel = mainRow.GetNode<Label>("StatsColumn/SpriteBox/MarginContainer/VBoxContainer/CharacterNameLabel");
			CharacterHealthLabel = mainRow.GetNode<Label>("StatsColumn/SpriteBox/MarginContainer/VBoxContainer/CharacterHealthLabel");
			BuffsListContainer = mainRow.GetNode<VBoxContainer>("StatsColumn/SpriteBox/MarginContainer/VBoxContainer/BuffsScroll/BuffsListContainer");
			EquiparButtonTemplate = ContextMenuContainer.GetNodeOrNull<Button>("EquiparButtonTemplate");
			EmptyPropertyLabelTemplate = BuffsListContainer.GetNodeOrNull<Label>("EmptyPropertyLabelTemplate");
			PropertyLabelTemplate = BuffsListContainer.GetNodeOrNull<Label>("PropertyLabelTemplate");

			if (EquiparButtonTemplate == null)
			{
				GD.PushError("InventoryUI: EquiparButtonTemplate não encontrado em ContextMenuContainer.");
			}
			else
			{
				EquiparButtonTemplate.Visible = false;
			}

			if (EmptyPropertyLabelTemplate == null)
			{
				GD.PushError("InventoryUI: EmptyPropertyLabelTemplate não encontrado em BuffsListContainer.");
			}
			else
			{
				EmptyPropertyLabelTemplate.Visible = false;
			}

			if (PropertyLabelTemplate == null)
			{
				GD.PushError("InventoryUI: PropertyLabelTemplate não encontrado em BuffsListContainer.");
			}
			else
			{
				PropertyLabelTemplate.Visible = false;
			}

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
			var template = MainControl.GetNodeOrNull<Panel>("DragPreviewTemplate");

			if (template == null)
			{
				GD.PushError("InventoryUI: DragPreviewTemplate não encontrado em Root.");
				return;
			}

			template.Visible = false;

			DragPreview = (Panel)template.Duplicate();
			DragPreview.Visible = false;

			MainControl.AddChild(DragPreview);
		}

		public void FindLocalPlayerInventorySystem()
		{
			if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
			{
				LocalPlayer.InventoryChanged -= OnInventoryChanged;
			}
			LocalPlayer = null;

			var worldManager = GetTree().Root.GetNodeOrNull<WorldManager>(StaticNodePathsConstants.WorldManager);

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
			if (Inventory == null)
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
			if (Inventory == null)
			{
				return;
			}

			var slot = Inventory.GetSlot(LocalPlayer.Data.Inventory, slotIndex);

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

			var slot = Inventory?.FindItem(LocalPlayer.Data.Inventory, DraggedInstanceId);
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
			if (Inventory == null)
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
			if (Inventory == null)
			{
				return;
			}

			var slot = Inventory.GetSlot(LocalPlayer.Data.Inventory, slotIndex);

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
			if (SelectedSlotIndex < 0 || Inventory == null)
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

			for (int i = 0; i < 128; i++)
			{
				UpdateSlot(i);
			}
		}

		public void ToggleInventory()
		{
			if (Inventory == null)
			{
				FindLocalPlayerInventorySystem();

				if (Inventory == null)
				{
					return;
				}
			}

			Visible = !Visible;

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
			if (LocalPlayer == null || !IsInstanceValid(LocalPlayer) || LocalPlayer.Data == null)
			{
				return;
			}

			CharacterNameLabel.Text = $"Jogador #{LocalPlayer.PeerId}";
			CharacterHealthLabel.Text = $"Vida: {LocalPlayer.Data.CurrentHealth}/{LocalPlayer.GetMaxHealth()}";
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

				foreach (var property in LocalPlayer.Data.Properties)
				{
					properties.Add(property);
				}

				foreach (var property in LocalPlayer.Properties)
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
				if (EmptyPropertyLabelTemplate == null)
				{
					GD.PushError("InventoryUI: EmptyPropertyLabelTemplate não encontrado, não é possível mostrar a lista de propriedades.");

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
					GD.PushError("InventoryUI: PropertyLabelTemplate não encontrado, não é possível mostrar a lista de propriedades.");

					continue;
				}

				var label = (Label)PropertyLabelTemplate.Duplicate();

				label.Text = text;
				label.Visible = true;

				BuffsListContainer.AddChild(label);
			}
		}

		public string DescribeProperty(BasePropertyData property)
		{
			return PropertyDescriptionFactory.Describe(property);
		}

		#endregion
	}
}
