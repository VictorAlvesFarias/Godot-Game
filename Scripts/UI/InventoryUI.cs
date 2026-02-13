using Godot;
using System;
using System.Collections.Generic;
using Jogo25D.Systems;
using Jogo25D.Items;

namespace Jogo25D.UI
{
	/// <summary>
	/// Interface principal do inventário com 16 slots integrados
	/// </summary>
	public partial class InventoryUI : CanvasLayer
	{
		private InventorySystem inventorySystem;
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
		private Panel panel;

		public override void _UnhandledInput(InputEvent @event)
		{
			// Fechar context menu ao clicar fora
			if (contextMenu.Visible && @event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
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
			// Obter referências dos nós da cena
			mainControl = GetNode<Control>("MainControl");
			panel = GetNode<Panel>("MainControl/Panel");
			gridContainer = GetNode<GridContainer>("MainControl/Panel/MarginContainer/VBoxContainer/GridContainer");
			contextMenu = GetNode<Panel>("MainControl/ContextMenu");
			contextMenuContainer = GetNode<VBoxContainer>("MainControl/ContextMenu/Container");
			
			// Buscar o InventorySystem do player local
			CallDeferred(nameof(FindLocalPlayerInventorySystem));

			// Ajustar tamanho do painel
			CallDeferred(nameof(AdjustPanelSize));

			// Iniciar oculto
			Visible = false;
		}

		/// <summary>
		/// Encontra o InventorySystem do player local (com autoridade multiplayer)
		/// </summary>
		private void FindLocalPlayerInventorySystem()
		{
			// Desconectar do inventorySystem anterior se existir
			if (inventorySystem != null && IsInstanceValid(inventorySystem))
			{
				inventorySystem.InventoryChanged -= OnInventoryChanged;
			}
			inventorySystem = null;
			
			var players = GetTree().GetNodesInGroup("players");
			var localPeerId = 1;
			var hasMultiplayer = false;

			if (Multiplayer != null && Multiplayer.MultiplayerPeer != null &&
				Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected)
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
						// Encontrou o player local, buscar InventorySystem
						inventorySystem = player.GetNodeOrNull<InventorySystem>("InventorySystem");
						if (inventorySystem != null)
						{
							inventorySystem.InventoryChanged += OnInventoryChanged;

							
							// Verificar se slots já foram inicializados (slots[0] não é null)
							if (slots[0] == null)
							{
								// Inicializar UI com dados do inventário
								InitializeSlots();
							}
							else
							{
								// Apenas atualizar dados se os slots já existem
								OnInventoryChanged();
							}
						}


		private void InitializeSlots()
		{
			// Configurar todos os 16 slots
			for (int i = 0; i < 16; i++)
			{
				SetupSlot(i);
			}

			// Atualizar dados iniciais do inventário
			OnInventoryChanged();
		}

		public override void _ExitTree()
		{
			// Desconectar sinais para evitar erros ao destruir a cena
			if (inventorySystem != null && IsInstanceValid(inventorySystem))
			{
				inventorySystem.InventoryChanged -= OnInventoryChanged;
			}
		}

        public override void _Input(InputEvent @event)
        {
            // Toggle inventário com I ou TAB
            if (Input.IsActionJustPressed("toggle_inventory"))
            {
                ToggleInventory();
                GetViewport().SetInputAsHandled();
            }
            // ESC fecha o inventário se estiver aberto
            else if (@event.IsActionPressed("ui_cancel") && Visible)
            {
                ToggleInventory();
                GetViewport().SetInputAsHandled();
            }
        }

        private void SetupSlot(int index)
		{
			slots[index] = GetNode<Panel>($"MainControl/Panel/MarginContainer/VBoxContainer/GridContainer/Slot{index}");
			
			// Criar elementos internos do slot
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
			nameLabels[index].AddThemeColorOverride("font_color", Colors.White);
			nameLabels[index].AddThemeColorOverride("font_outline_color", Colors.Black);
			nameLabels[index].AddThemeConstantOverride("outline_size", 1);
			nameLabels[index].AddThemeFontSizeOverride("font_size", 10);
			nameLabels[index].Visible = false;
			centerContainer.AddChild(nameLabels[index]);

			quantityLabels[index] = new Label();
			quantityLabels[index].HorizontalAlignment = HorizontalAlignment.Right;
			quantityLabels[index].VerticalAlignment = VerticalAlignment.Bottom;
			quantityLabels[index].AddThemeColorOverride("font_color", Colors.White);
			quantityLabels[index].AddThemeColorOverride("font_outline_color", Colors.Black);
			quantityLabels[index].AddThemeConstantOverride("outline_size", 2);
			slots[index].AddChild(quantityLabels[index]);

			selectedBorders[index] = new Panel();
			selectedBorders[index].Visible = false;
			selectedBorders[index].MouseFilter = Control.MouseFilterEnum.Ignore;
			selectedBorders[index].SetAnchorsPreset(Control.LayoutPreset.FullRect);
			var styleBox = new StyleBoxFlat();
			styleBox.BorderColor = Colors.Yellow;
			styleBox.BorderWidthLeft = 3;
			styleBox.BorderWidthRight = 3;
			styleBox.BorderWidthTop = 3;
			styleBox.BorderWidthBottom = 3;
			styleBox.BgColor = new Color(0, 0, 0, 0);
			selectedBorders[index].AddThemeStyleboxOverride("panel", styleBox);
			slots[index].AddChild(selectedBorders[index]);

			// Conectar evento de input do slot
			int slotIndex = index;
			slots[index].GuiInput += (InputEvent @event) => OnSlotInput(slotIndex, @event);
		}

		private void OnSlotInput(int slotIndex, InputEvent @event)
		{
			if (inventorySystem == null || !IsInstanceValid(inventorySystem)) return;

			var slot = inventorySystem.GetSlot(slotIndex);
			
			if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
			{
				// Botão esquerdo - selecionar
				if (mouseEvent.ButtonIndex == MouseButton.Left)
				{
					SelectSlot(slotIndex);
					slots[slotIndex].AcceptEvent();
				}
				// Botão direito - menu de contexto
				else if (mouseEvent.ButtonIndex == MouseButton.Right && !slot.IsEmpty)
				{
					ShowContextMenuForSlot(slotIndex, mouseEvent.GlobalPosition);
					slots[slotIndex].AcceptEvent();
				}
			}
		}

		private void UpdateSlot(int index)
		{
			if (inventorySystem == null || !IsInstanceValid(inventorySystem)) return;
			
			var slot = inventorySystem.GetSlot(index);
			
			if (slot.IsEmpty || slot.Item == null)
			{
				iconRects[index].Texture = null;
				nameLabels[index].Visible = false;
				quantityLabels[index].Text = "";
				return;
			}

			if (slot.Item.Icon != null)
			{
				iconRects[index].Texture = slot.Item.Icon;
				nameLabels[index].Visible = false;
			}
			else
			{
				iconRects[index].Texture = null;
				nameLabels[index].Text = slot.Item.ItemName;
				nameLabels[index].Visible = true;
			}

			if (slot.Item.IsStackable && slot.Quantity > 1)
			{
				quantityLabels[index].Text = slot.Quantity.ToString();
			}
			else
			{
				quantityLabels[index].Text = "";
			}
		}

		private void SelectSlot(int index)
		{
			// Desselecionar anterior
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
			if (inventorySystem == null || !IsInstanceValid(inventorySystem)) return;
			
			var slot = inventorySystem.GetSlot(slotIndex);
			if (slot.IsEmpty) return;

			selectedSlotIndex = slotIndex;

			// Limpar opções antigas
			foreach (Node child in contextMenuContainer.GetChildren())
			{
				contextMenuContainer.RemoveChild(child);
				child.QueueFree();
			}

			// Apenas opção de equipar para itens equipáveis
			if (slot.Item.IsEquippable)
			{
				var button = new Button();
				button.Text = "Equipar";
				button.CustomMinimumSize = new Vector2(120, 30);
				button.Alignment = HorizontalAlignment.Center;
				button.MouseFilter = Control.MouseFilterEnum.Stop;
				button.Pressed += () => OnContextMenuOption("Equipar");
				contextMenuContainer.AddChild(button);
			}

			contextMenu.GlobalPosition = position;
			contextMenu.Visible = true;
			contextMenu.MoveToFront();
		}

		private void OnContextMenuOption(string option)
		{
			if (selectedSlotIndex < 0 || inventorySystem == null || !IsInstanceValid(inventorySystem)) return;

			var slot = inventorySystem.GetSlot(selectedSlotIndex);
			if (slot.IsEmpty) return;

			if (option == "Equipar")
			{
				inventorySystem.EquipItem(selectedSlotIndex);
			}

			contextMenu.Visible = false;
		}

		private void AdjustPanelSize()
		{
			// Forçar atualização de layout para calcular o tamanho real
			panel.ResetSize();
			
			// Centralizar usando offsets (igual ao PauseMenu)
			var panelSize = panel.Size;
			var halfWidth = panelSize.X / 2;
			var halfHeight = panelSize.Y / 2;
			
			panel.OffsetLeft = -halfWidth;
			panel.OffsetTop = -halfHeight;
			panel.OffsetRight = halfWidth;
			panel.OffsetBottom = halfHeight;
		}

		private void OnInventoryChanged()
		{
			if (!IsInstanceValid(this)) return;
			
			// Atualizar todos os slots
			for (int i = 0; i < 16; i++)
			{
				UpdateSlot(i);
			}
		}

		public void ToggleInventory()
		{
			// Verificar se o inventorySystem é válido antes de abrir
			if (inventorySystem == null || !IsInstanceValid(inventorySystem))
			{
			FindLocalPlayerInventorySystem();
			
			// Se ainda não encontrou, não abrir o inventário
			if (inventorySystem == null || !IsInstanceValid(inventorySystem))
			{
			
			if (Visible)
			{
				// Atualizar ao abrir
				OnInventoryChanged();
			}
		}

		public void AddItemToInventory(InventoryItem item, int quantity = 1)
		{
			if (inventorySystem != null && IsInstanceValid(inventorySystem))
			{
				inventorySystem.AddItem(item, quantity);
			}
		}
	}
}
