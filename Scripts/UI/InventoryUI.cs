using Godot;
using System;
using Jogo25D.Systems;
using Jogo25D.Items;

namespace Jogo25D.UI
{
    /// <summary>
    /// Interface principal do inventário com 16 slots
    /// </summary>
    public partial class InventoryUI : Control
    {
        [Export] public NodePath InventorySystemPath { get; set; }
        
        private InventorySystem inventorySystem;
        private GridContainer gridContainer;
        private ContextMenuUI contextMenu;
        private ItemSlotUI[] slotUIs = new ItemSlotUI[16];
        private int selectedSlotIndex = -1;

        public override void _Ready()
        {
            // Obter referência ao sistema de inventário
            if (InventorySystemPath != null)
            {
                inventorySystem = GetNode<InventorySystem>(InventorySystemPath);
                inventorySystem.InventoryChanged += OnInventoryChanged;
            }

            // Criar UI
            CreateInventoryUI();
            
            // Menu de contexto
            contextMenu = new ContextMenuUI();
            contextMenu.OptionSelected += OnContextMenuOption;
            AddChild(contextMenu);

            // Iniciar oculto
            Visible = false;
        }

        private void CreateInventoryUI()
        {
            // Container principal com fundo semi-transparente - CENTRALIZADO igual ao PauseMenu
            var panel = new Panel();
            panel.SetAnchorsPreset(Control.LayoutPreset.Center);
            panel.GrowHorizontal = Control.GrowDirection.Both;
            panel.GrowVertical = Control.GrowDirection.Both;
            
            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            panelStyle.BorderColor = Colors.White;
            panelStyle.BorderWidthLeft = 3;
            panelStyle.BorderWidthRight = 3;
            panelStyle.BorderWidthTop = 3;
            panelStyle.BorderWidthBottom = 3;
            panel.AddThemeStyleboxOverride("panel", panelStyle);
            AddChild(panel);

            var marginContainer = new MarginContainer();
            marginContainer.AddThemeConstantOverride("margin_left", 15);
            marginContainer.AddThemeConstantOverride("margin_top", 15);
            marginContainer.AddThemeConstantOverride("margin_right", 15);
            marginContainer.AddThemeConstantOverride("margin_bottom", 15);
            panel.AddChild(marginContainer);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 10);
            marginContainer.AddChild(vbox);

            // Título
            var title = new Label();
            title.Text = "INVENTÁRIO";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 24);
            vbox.AddChild(title);

            vbox.AddChild(new HSeparator());

            // Grid de 16 slots (4x4) - tamanho automático
            gridContainer = new GridContainer();
            gridContainer.Columns = 4;
            gridContainer.AddThemeConstantOverride("h_separation", 8);
            gridContainer.AddThemeConstantOverride("v_separation", 8);
            vbox.AddChild(gridContainer);

            // Criar os 16 slots
            for (int i = 0; i < 16; i++)
            {
                var slotUI = new ItemSlotUI();
                slotUI.SlotIndex = i;
                slotUI.SlotClicked += OnSlotClicked;
                gridContainer.AddChild(slotUI);
                slotUIs[i] = slotUI;
            }
            
            // Deixar o painel se ajustar ao conteúdo
            CallDeferred(nameof(AdjustPanelSize), panel);
        }
        
        private void AdjustPanelSize(Panel panel)
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

        private void OnSlotClicked(int slotIndex, InputEvent inputEvent)
        {


            if (inventorySystem == null) return;

            var slot = inventorySystem.GetSlot(slotIndex);
            
            if (inputEvent is InputEventMouseButton mouseEvent)
            {
                // Botão esquerdo - selecionar
                if (mouseEvent.ButtonIndex == MouseButton.Left)
                {
                    SelectSlot(slotIndex);
                }
                // Botão direito - menu de contexto
                else if (mouseEvent.ButtonIndex == MouseButton.Right && !slot.IsEmpty)
                {
                    ShowContextMenuForSlot(slotIndex, mouseEvent.GlobalPosition);
                }
            }
        }

        private void SelectSlot(int index)
        {
            Console.WriteLine("SelectSlot");

            // Desselecionar anterior
            if (selectedSlotIndex >= 0 && selectedSlotIndex < 16)
            {
                slotUIs[selectedSlotIndex].SetSelected(false);
            }

            selectedSlotIndex = index;
            
            if (selectedSlotIndex >= 0 && selectedSlotIndex < 16)
            {
                slotUIs[selectedSlotIndex].SetSelected(true);
            }
        }

        private void ShowContextMenuForSlot(int slotIndex, Vector2 position)
        {
            Console.WriteLine("ShowContextMenuForSlot");

            var slot = inventorySystem.GetSlot(slotIndex);
            if (slot.IsEmpty) return;

            selectedSlotIndex = slotIndex;

            // Apenas opção de equipar para itens equipáveis
            if (slot.Item.IsEquippable)
            {
                contextMenu.ShowMenu(position, new string[] { "Equipar" });
            }
        }

        private void OnContextMenuOption(string option)
        {
            Console.WriteLine("OnContextMenuOption");


            if (selectedSlotIndex < 0 || inventorySystem == null) return;

            var slot = inventorySystem.GetSlot(selectedSlotIndex);
            if (slot.IsEmpty) return;

            if (option == "Equipar")
            {
                inventorySystem.EquipItem(selectedSlotIndex);
            }
        }

        private void OnInventoryChanged()
        {
            // Atualizar todos os slots
            for (int i = 0; i < 16; i++)
            {
                var slot = inventorySystem.GetSlot(i);
                slotUIs[i].UpdateSlot(slot);
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

        public void ToggleInventory()
        {
            Visible = !Visible;
            
            if (Visible)
            {
                // Atualizar ao abrir
                OnInventoryChanged();
            }
        }

        public void AddItemToInventory(InventoryItem item, int quantity = 1)
        {
            if (inventorySystem != null)
            {
                inventorySystem.AddItem(item, quantity);
            }
        }
    }
}
