using Godot;
using Jogo25D.SkillTree;
using System.Collections.Generic;

namespace Jogo25D.UI
{
    // Card "Árvore de habilidades": um SkillNodeButton por no da arvore, com nivel atual/maximo, busca por nome e
    // resetar pontos.
    public partial class SkillTreeCard : CardUI
    {
        #region Node children references

        public Label PointsLabel { get; private set; }
        public LineEdit SearchInput { get; private set; }
        public Button ResetButton { get; private set; }
        public GridContainer GridContainer { get; private set; }

        #endregion

        #region Dinamic properties

        private static readonly PackedScene NodeButtonScene = GD.Load<PackedScene>("res://Scenes/Ui/GameMenu/Components/SkillNodeButton.tscn");

        public Dictionary<string, SkillNodeButton> NodeButtons { get; } = new();

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            PointsLabel = GetNode<Label>("Box/MarginContainer/Content/PointsLabel");
            SearchInput = GetNode<LineEdit>("Box/MarginContainer/Content/Toolbar/SearchInput");
            ResetButton = GetNode<Button>("Box/MarginContainer/Content/Toolbar/ResetButton");
            GridContainer = GetNode<GridContainer>("Box/MarginContainer/Content/Scroll/GridContainer");

            SearchInput.TextChanged += OnSearchTextChanged;
            ResetButton.Pressed += OnResetPressed;

            // A grade traz um no de previa (para o editor); os nos reais vem da arvore.
            foreach (var preview in GridContainer.GetChildren())
            {
                GridContainer.RemoveChild(preview);
                preview.QueueFree();
            }

            foreach (var nodeId in SkillTreeDB.GetAllIds())
            {
                var button = NodeButtonScene.Instantiate<SkillNodeButton>();

                GridContainer.AddChild(button);
                button.ShowNode(SkillTreeDB.Get(nodeId));
                button.Pressed += () => OnNodePressed(nodeId);

                NodeButtons[nodeId] = button;
            }
        }

        public override void _Process(double delta)
        {
            if (IsOpen)
            {
                RefreshGrid();
            }
        }

        #endregion

        #region Core - Arvore

        public void OnNodePressed(string nodeId)
        {
            if (!HasPlayer)
            {
                return;
            }

            LocalPlayer.LevelUpSkillNodeRequest(nodeId);

            RefreshGrid();
        }

        public void OnResetPressed()
        {
            if (!HasPlayer)
            {
                return;
            }

            LocalPlayer.ResetSkillTreeRequest();

            RefreshGrid();
        }

        public void OnSearchTextChanged(string text)
        {
            var query = text.Trim().ToLowerInvariant();

            foreach (var (nodeId, button) in NodeButtons)
            {
                button.Visible = string.IsNullOrEmpty(query) || SkillTreeDB.Get(nodeId).Name.ToLowerInvariant().Contains(query);
            }
        }

        public void RefreshGrid()
        {
            var progress = HasPlayer ? LocalPlayer.SkillTree : null;

            PointsLabel.Text = progress == null
                ? "Pontos disponiveis: ilimitado (temporario)"
                : $"Pontos disponiveis: ilimitado (temporario) | Investido na arvore principal: {SkillTreeDB.GetTreeLevel(progress, "main")}";

            foreach (var (nodeId, button) in NodeButtons)
            {
                var currentLevel = progress == null ? 0 : SkillTreeDB.GetNodeLevel(progress, nodeId);

                button.ShowLevel(currentLevel, progress != null && SkillTreeDB.CanLevelUp(progress, nodeId));
            }
        }

        #endregion
    }
}
