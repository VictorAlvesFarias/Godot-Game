using Godot;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Items;

namespace Jogo25D.UI
{
    // Slot do inventario: icone (ou nome, sem icone), quantidade e o numero do slot. O layout fica na cena; o script so
    // preenche o item e troca o estilo ao passar o mouse. [Tool] para o numero aparecer certo tambem no editor.
    [Tool]
    public partial class InventorySlot : Panel
    {
        #region Node children references

        public TextureRect Icon { get; private set; }
        public Label NameLabel { get; private set; }
        public Label QuantityLabel { get; private set; }
        public Label NumLabel { get; private set; }

        #endregion

        #region Dinamic properties

        private static readonly StringName NormalVariation = "Slot";
        private static readonly StringName HoverVariation = "SlotHover";

        private int _index;

        // Posicao no inventario (0 = primeiro slot da hotbar); o numero mostrado e Index + 1.
        [Export]
        public int Index
        {
            get => _index;
            set
            {
                _index = value;

                if (NumLabel != null)
                {
                    NumLabel.Text = $"{_index + 1}";
                }
            }
        }

        // Icone esmaecido enquanto o item do slot esta sendo arrastado.
        public bool Dimmed
        {
            set => Icon.Modulate = value ? new Color(1, 1, 1, 0.5f) : Colors.White;
        }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            Icon = GetNode<TextureRect>("MarginContainer/CenterContainer/Icon");
            NameLabel = GetNode<Label>("MarginContainer/CenterContainer/NameLabel");
            QuantityLabel = GetNode<Label>("QuantityLabel");
            NumLabel = GetNode<Label>("NumLabel");
            NumLabel.Text = $"{_index + 1}";

            if (Engine.IsEditorHint())
            {
                return;
            }

            MouseEntered += () => ThemeTypeVariation = HoverVariation;
            MouseExited += () => ThemeTypeVariation = NormalVariation;

            Clear();
        }

        #endregion

        #region Core - Item

        public void Clear()
        {
            Icon.Texture = null;
            NameLabel.Visible = false;
            QuantityLabel.Text = "";
            TooltipText = "";
        }

        public void ShowItem(ItemDefinition definition, ItemData data)
        {
            TooltipText = definition.Name;
            Icon.Texture = definition.Icon;
            NameLabel.Text = definition.Name;
            NameLabel.Visible = definition.Icon == null;
            QuantityLabel.Text = definition.Stackable && data.Quantity > 1 ? $"x{data.Quantity}" : "";
        }

        #endregion
    }
}
