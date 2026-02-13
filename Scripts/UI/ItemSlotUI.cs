using Godot;
using System;
using Jogo25D.Items;

namespace Jogo25D.UI
{
    /// <summary>
    /// Representa um slot individual na UI do inventário
    /// </summary>
    public partial class ItemSlotUI : Panel
    {
        [Signal]
        public delegate void SlotClickedEventHandler(int slotIndex, InputEvent inputEvent);

        [Export] public int SlotIndex { get; set; } = 0;

        private TextureRect iconRect;
        private Label quantityLabel;
        private Label nameLabel;
        private Panel selectedBorder;
        
        private ItemSlot itemSlot;

        public override void _Ready()
        {
            // Criar layout do slot
            CustomMinimumSize = new Vector2(64, 64);
            
            // Adicionar MarginContainer para padding
            var marginContainer = new MarginContainer();
            marginContainer.AddThemeConstantOverride("margin_left", 4);
            marginContainer.AddThemeConstantOverride("margin_top", 4);
            marginContainer.AddThemeConstantOverride("margin_right", 4);
            marginContainer.AddThemeConstantOverride("margin_bottom", 4);
            AddChild(marginContainer);
            
            // Container centralizado para ícone OU texto
            var centerContainer = new CenterContainer();
            marginContainer.AddChild(centerContainer);

            // Ícone do item
            iconRect = new TextureRect();
            iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            iconRect.CustomMinimumSize = new Vector2(56, 56);
            centerContainer.AddChild(iconRect);
            
            // Label do nome (quando não tem ícone)
            nameLabel = new Label();
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.VerticalAlignment = VerticalAlignment.Center;
            nameLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            nameLabel.AddThemeColorOverride("font_color", Colors.White);
            nameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            nameLabel.AddThemeConstantOverride("outline_size", 1);
            nameLabel.AddThemeFontSizeOverride("font_size", 10);
            nameLabel.Visible = false;
            centerContainer.AddChild(nameLabel);

            // Label de quantidade
            quantityLabel = new Label();
            quantityLabel.HorizontalAlignment = HorizontalAlignment.Right;
            quantityLabel.VerticalAlignment = VerticalAlignment.Bottom;
            quantityLabel.AddThemeColorOverride("font_color", Colors.White);
            quantityLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            quantityLabel.AddThemeConstantOverride("outline_size", 2);
            AddChild(quantityLabel);

            // Borda de seleção
            selectedBorder = new Panel();
            selectedBorder.Visible = false;
            selectedBorder.MouseFilter = MouseFilterEnum.Ignore;
            selectedBorder.SetAnchorsPreset(LayoutPreset.FullRect);
            var styleBox = new StyleBoxFlat();
            styleBox.BorderColor = Colors.Yellow;
            styleBox.BorderWidthLeft = 3;
            styleBox.BorderWidthRight = 3;
            styleBox.BorderWidthTop = 3;
            styleBox.BorderWidthBottom = 3;
            styleBox.BgColor = new Color(0, 0, 0, 0);
            selectedBorder.AddThemeStyleboxOverride("panel", styleBox);
            AddChild(selectedBorder);

            // Configurar estilo do slot
            var slotStyle = new StyleBoxFlat();
            slotStyle.BgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            slotStyle.BorderColor = new Color(0.4f, 0.4f, 0.4f);
            slotStyle.BorderWidthLeft = 2;
            slotStyle.BorderWidthRight = 2;
            slotStyle.BorderWidthTop = 2;
            slotStyle.BorderWidthBottom = 2;
            AddThemeStyleboxOverride("panel", slotStyle);
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
            {
                EmitSignal(SignalName.SlotClicked, SlotIndex, @event);
                AcceptEvent();
            }
        }

        /// <summary>
        /// Atualiza a visualização do slot com os dados do item
        /// </summary>
        public void UpdateSlot(ItemSlot slot)
        {
            itemSlot = slot;

            if (slot == null || slot.IsEmpty)
            {
                iconRect.Visible = false;
                nameLabel.Visible = false;
                quantityLabel.Text = "";
                TooltipText = "";
                
                // Restaurar estilo padrão
                var slotStyle = new StyleBoxFlat();
                slotStyle.BgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                slotStyle.BorderColor = new Color(0.4f, 0.4f, 0.4f);
                slotStyle.BorderWidthLeft = 2;
                slotStyle.BorderWidthRight = 2;
                slotStyle.BorderWidthTop = 2;
                slotStyle.BorderWidthBottom = 2;
                AddThemeStyleboxOverride("panel", slotStyle);
                return;
            }

            // Se não tem ícone, mostrar nome do item
            if (slot.Item.Icon == null)
            {
                iconRect.Visible = false;
                nameLabel.Visible = true;
                nameLabel.Text = slot.Item.ItemName;
                
                // Cor de fundo baseada no tipo de item
                var slotStyle = new StyleBoxFlat();
                switch (slot.Item.Type)
                {
                    case ItemType.Weapon:
                        slotStyle.BgColor = new Color(0.4f, 0.3f, 0.2f, 0.9f); // Marrom
                        break;
                    case ItemType.Consumable:
                        slotStyle.BgColor = new Color(0.2f, 0.4f, 0.2f, 0.9f); // Verde escuro
                        break;
                    case ItemType.Material:
                        slotStyle.BgColor = new Color(0.3f, 0.3f, 0.3f, 0.9f); // Cinza
                        break;
                    default:
                        slotStyle.BgColor = new Color(0.2f, 0.3f, 0.4f, 0.9f); // Azul escuro
                        break;
                }
                slotStyle.BorderColor = new Color(0.6f, 0.6f, 0.6f);
                slotStyle.BorderWidthLeft = 2;
                slotStyle.BorderWidthRight = 2;
                slotStyle.BorderWidthTop = 2;
                slotStyle.BorderWidthBottom = 2;
                AddThemeStyleboxOverride("panel", slotStyle);
            }
            else
            {
                iconRect.Visible = true;
                nameLabel.Visible = false;
                iconRect.Texture = slot.Item.Icon;
                
                // Estilo padrão
                var slotStyle = new StyleBoxFlat();
                slotStyle.BgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                slotStyle.BorderColor = new Color(0.4f, 0.4f, 0.4f);
                slotStyle.BorderWidthLeft = 2;
                slotStyle.BorderWidthRight = 2;
                slotStyle.BorderWidthTop = 2;
                slotStyle.BorderWidthBottom = 2;
                AddThemeStyleboxOverride("panel", slotStyle);
            }
            
            quantityLabel.Text = slot.Quantity > 1 ? slot.Quantity.ToString() : "";
            
            // Tooltip com informações do item
            TooltipText = $"{slot.Item.ItemName}\n{slot.Item.Description}";
        }

        public void SetSelected(bool selected)
        {
            if (selectedBorder != null)
            {
                selectedBorder.Visible = selected;
            }
        }

        public ItemSlot GetItemSlot()
        {
            return itemSlot;
        }
    }
}
