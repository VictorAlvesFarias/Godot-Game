using Godot;
using Jogo25D.Crafting;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Items;
using Jogo25D.Systems;
using System.Linq;

namespace Jogo25D.UI
{
    // Linha de receita do card "Criação": icone e nome do resultado e os ingredientes (tem/precisa). O layout fica na
    // cena; o script so preenche os dados. Sem ingredientes suficientes, a linha fica desabilitada.
    public partial class RecipeRow : Button
    {
        #region Node children references

        public TextureRect Icon { get; private set; }
        public Label NameLabel { get; private set; }
        public RichTextLabel IngredientsLabel { get; private set; }

        #endregion

        #region Dinamic properties

        private static readonly Color MissingIngredientColor = new(0.93f, 0.62f, 0.66f);

        public string RecipeId { get; private set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            Icon = GetNode<TextureRect>("MarginContainer/Content/Icon");
            NameLabel = GetNode<Label>("MarginContainer/Content/Texts/NameLabel");
            IngredientsLabel = GetNode<RichTextLabel>("MarginContainer/Content/Texts/IngredientsLabel");
        }

        #endregion

        #region Core - Receita

        public void ShowRecipe(RecipeDefinition recipe, InventoryData inventory)
        {
            var result = ItemFactory.Create(recipe.ResultItemId);

            RecipeId = recipe.Id;
            Disabled = !CraftingSystem.HasIngredients(inventory, recipe);
            TooltipText = result?.Description ?? "";
            Icon.Texture = result?.Icon;
            NameLabel.Text = recipe.ResultQuantity > 1 ? $"{result?.Name} x{recipe.ResultQuantity}" : result?.Name;
            IngredientsLabel.Text = string.Join("  ", recipe.Ingredients.Select(ingredient =>
            {
                var have = InventorySystem.CountItem(inventory, ingredient.ItemId);
                var text = $"{ItemFactory.Create(ingredient.ItemId)?.Name} {have}/{ingredient.Quantity}";

                return have >= ingredient.Quantity ? text : $"[color=#{MissingIngredientColor.ToHtml(false)}]{text}[/color]";
            }));
        }

        #endregion
    }
}
