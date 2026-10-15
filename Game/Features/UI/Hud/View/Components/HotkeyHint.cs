using Godot;

namespace Jogo25D.UI
{
    // Dica de atalho do HUD: icone ao centro e a tecla no canto. O layout fica na cena; icone e tecla sao definidos em
    // cada instancia. [Tool] para aparecerem tambem no editor.
    [Tool]
    public partial class HotkeyHint : Panel
    {
        #region Node children references

        public TextureRect IconRect { get; private set; }
        public Label KeyLabel { get; private set; }

        #endregion

        #region Dinamic properties

        private static readonly StringName NormalVariation = "SlotRound";
        private static readonly StringName HoverVariation = "SlotRoundHover";

        private Texture2D _icon;
        private string _key = "";

        [Export]
        public Texture2D Icon
        {
            get => _icon;
            set
            {
                _icon = value;

                if (IconRect != null)
                {
                    IconRect.Texture = value;
                }
            }
        }

        [Export]
        public string Key
        {
            get => _key;
            set
            {
                _key = value;

                if (KeyLabel != null)
                {
                    KeyLabel.Text = value;
                }
            }
        }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            IconRect = GetNode<TextureRect>("MarginContainer/CenterContainer/IconRect");
            KeyLabel = GetNode<Label>("KeyLabel");
            IconRect.Texture = _icon;
            KeyLabel.Text = _key;

            if (Engine.IsEditorHint())
            {
                return;
            }

            MouseEntered += () => ThemeTypeVariation = HoverVariation;
            MouseExited += () => ThemeTypeVariation = NormalVariation;
        }

        #endregion
    }
}
