using Godot;
using Jogo25D.SkillTree;

namespace Jogo25D.UI
{
    // No da arvore de habilidades: icone, nome e nivel atual/maximo. O layout fica na cena; o script so preenche os
    // dados. No nivel maximo, usa a variacao de tema SkillSlotMaxed (desabilitado com o estilo de selecionado).
    public partial class SkillNodeButton : Button
    {
        #region Node children references

        public TextureRect Icon { get; private set; }
        public Label NameLabel { get; private set; }
        public Label LevelLabel { get; private set; }

        #endregion

        #region Dinamic properties

        private static readonly StringName NormalVariation = "SkillSlot";
        private static readonly StringName MaxedVariation = "SkillSlotMaxed";

        public SkillTreeNode Node { get; private set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            Icon = GetNode<TextureRect>("MarginContainer/Box/CenterBox/Icon");
            NameLabel = GetNode<Label>("MarginContainer/Box/CenterBox/NameLabel");
            LevelLabel = GetNode<Label>("MarginContainer/Box/LevelLabel");
        }

        #endregion

        #region Core - No

        public void ShowNode(SkillTreeNode node)
        {
            Node = node;
            TooltipText = BuildTooltip(node);
            Icon.Texture = node.Icon;
            Icon.Visible = node.Icon != null;
            NameLabel.Text = node.Name;
        }

        public void ShowLevel(int currentLevel, bool canLevelUp)
        {
            var maxed = currentLevel >= Node.MaxLevel;

            LevelLabel.Text = $"{currentLevel}/{Node.MaxLevel}";
            Disabled = maxed || !canLevelUp;
            ThemeTypeVariation = maxed ? MaxedVariation : NormalVariation;
        }

        private static string BuildTooltip(SkillTreeNode node)
        {
            var text = node.Description;

            if (node.RequiredTreeLevel > 0)
            {
                text += $"\nRequer nivel total {node.RequiredTreeLevel} na arvore principal.";
            }

            foreach (var dependency in node.Dependencies)
            {
                var dependencyNode = SkillTreeDB.Get(dependency.NodeId);

                text += $"\nRequer {dependencyNode?.Name ?? dependency.NodeId} nivel {dependency.MinLevel}.";
            }

            return text;
        }

        #endregion
    }
}
