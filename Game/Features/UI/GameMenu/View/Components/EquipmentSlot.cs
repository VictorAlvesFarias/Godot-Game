using Godot;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Items;

namespace Jogo25D.UI
{
    // Slot de equipamento do card Player: icone da peca equipada ou, vazio, o contorno do tipo de slot (exportado na
    // cena). O layout fica na cena; o script so preenche a peca e troca o estilo ao passar o mouse. [Tool] para o
    // contorno aparecer tambem no editor.
    [Tool]
    public partial class EquipmentSlot : Panel
    {
        #region Node children references

        public TextureRect Outline { get; private set; }
        public TextureRect Icon { get; private set; }

        #endregion

        #region Dinamic properties

        private static readonly StringName NormalVariation = "Slot";
        private static readonly StringName HoverVariation = "SlotHover";

        // Posicao no EquipmentData (0 cabeca, 1 peito, 2 pernas, 3 e 4 acessorios).
        [Export]
        public int Index { get; set; }

        private Texture2D _outlineTexture;

        // Contorno mostrado com o slot vazio (capacete, peitoral, calcas ou acessorio).
        [Export]
        public Texture2D OutlineTexture
        {
            get => _outlineTexture;
            set
            {
                _outlineTexture = value;

                if (Outline != null)
                {
                    Outline.Texture = value;
                }
            }
        }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            Outline = GetNode<TextureRect>("MarginContainer/Outline");
            Icon = GetNode<TextureRect>("MarginContainer/Icon");
            Outline.Texture = _outlineTexture;

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
            Outline.Visible = true;
            TooltipText = "";
        }

        public void ShowItem(ItemDefinition definition)
        {
            Icon.Texture = definition.Icon;
            Outline.Visible = false;
            TooltipText = $"{definition.Name}\n{definition.Description}";
        }

        #endregion
    }
}
