using Godot;
using Jogo25D.Crafting;
using Jogo25D.Items;
using Jogo25D.Props;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.UI
{
    // Card "Criação": receitas sem estacao e, perto de uma estacao, as dela, com busca por nome. Cada linha mostra o
    // resultado e os ingredientes (tem/precisa); sem ingredientes, a linha fica desabilitada. Clicar cria uma vez.
    public partial class CraftingCard : CardUI
    {
        #region Node children references

        public VBoxContainer RecipeList { get; private set; }
        public LineEdit SearchInput { get; private set; }
        public Label HintLabel { get; private set; }

        #endregion

        #region Dinamic properties

        // Intervalo, em segundos, entre as checagens de estacoes proximas com a tela aberta.
        private const double StationCheckInterval = 0.25;

        private static readonly PackedScene RecipeRowScene = GD.Load<PackedScene>("res://Scenes/Ui/GameMenu/Components/RecipeRow.tscn");

        private HashSet<string> _nearStations = new();
        private double _stationCheckElapsed;

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            RecipeList = GetNode<VBoxContainer>("Box/MarginContainer/Content/Scroll/RecipeList");
            SearchInput = GetNode<LineEdit>("Box/MarginContainer/Content/SearchInput");
            HintLabel = GetNode<Label>("Box/MarginContainer/Content/HintLabel");

            SearchInput.TextChanged += _ => RefreshRecipes();

            // A lista traz uma linha de previa (para o editor); as linhas reais vem das receitas.
            foreach (var preview in RecipeList.GetChildren())
            {
                RecipeList.RemoveChild(preview);
                preview.QueueFree();
            }
        }

        public override void _Process(double delta)
        {
            if (IsOpen)
            {
                CheckNearStations(delta);
            }
        }

        #endregion

        #region Core - Contrato do card

        public override void OnOpened()
        {
            base.OnOpened();

            _nearStations = HasPlayer ? CraftingSystem.StationsNear(LocalPlayer) : new HashSet<string>();
            _stationCheckElapsed = 0;

            RefreshRecipes();
        }

        public override void OnInventoryChanged()
        {
            RefreshRecipes();
        }

        #endregion

        #region Core - Receitas

        // Refaz a lista quando o jogador entra ou sai de perto de uma estacao.
        private void CheckNearStations(double delta)
        {
            _stationCheckElapsed += delta;

            if (_stationCheckElapsed < StationCheckInterval || !HasPlayer)
            {
                return;
            }

            _stationCheckElapsed = 0;

            var stations = CraftingSystem.StationsNear(LocalPlayer);

            if (stations.SetEquals(_nearStations))
            {
                return;
            }

            _nearStations = stations;

            RefreshRecipes();
        }

        public void RefreshRecipes()
        {
            if (RecipeList == null || !HasPlayer || LocalPlayer.Inventory == null)
            {
                return;
            }

            foreach (var child in RecipeList.GetChildren())
            {
                RecipeList.RemoveChild(child);
                child.QueueFree();
            }

            foreach (var recipe in CraftingSystem.Available(_nearStations).Where(Matches))
            {
                var row = RecipeRowScene.Instantiate<RecipeRow>();

                RecipeList.AddChild(row);
                row.ShowRecipe(recipe, LocalPlayer.Inventory);
                row.Pressed += () => LocalPlayer?.CraftRequest(row.RecipeId);
            }

            HintLabel.Visible = !_nearStations.Contains(PropDB.CraftingTableStation);
        }

        // Busca pelo nome do resultado.
        private bool Matches(RecipeDefinition recipe)
        {
            var query = SearchInput.Text.Trim().ToLowerInvariant();

            return string.IsNullOrEmpty(query) || (ItemFactory.Create(recipe.ResultItemId)?.Name ?? recipe.Id).ToLowerInvariant().Contains(query);
        }

        #endregion
    }
}
