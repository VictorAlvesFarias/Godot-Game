using Jogo25D.Props;
using System.Collections.Generic;

namespace Jogo25D.Crafting
{
    public static class RecipeDB
    {
        // Na ordem em que aparecem na lista de criacao.
        private static readonly List<RecipeDefinition> _recipes = new()
        {
            new RecipeDefinition
            {
                Id = "crafting_table",
                ResultItemId = "crafting_table",
                Ingredients = new List<(string, int)> { ("item_wood", 10) },
            },
            new RecipeDefinition
            {
                Id = "torch",
                ResultItemId = "torch",
                ResultQuantity = 4,
                Ingredients = new List<(string, int)> { ("item_wood", 1) },
            },
            new RecipeDefinition
            {
                Id = "wall_wood",
                ResultItemId = "wall_wood",
                ResultQuantity = 4,
                Ingredients = new List<(string, int)> { ("item_wood", 1) },
                Station = PropDB.CraftingTableStation,
            },
            new RecipeDefinition
            {
                Id = "pickaxe_starting",
                ResultItemId = "pickaxe_starting",
                Ingredients = new List<(string, int)> { ("item_wood", 8) },
                Station = PropDB.CraftingTableStation,
            },
            new RecipeDefinition
            {
                Id = "sword_basic",
                ResultItemId = "sword_basic",
                Ingredients = new List<(string, int)> { ("item_wood", 12) },
                Station = PropDB.CraftingTableStation,
            },
            new RecipeDefinition
            {
                Id = "portal",
                ResultItemId = "portal",
                Ingredients = new List<(string, int)> { ("item_wood", 20), ("item_leaf", 10) },
                Station = PropDB.CraftingTableStation,
            },
        };

        public static IReadOnlyList<RecipeDefinition> All => _recipes;

        public static RecipeDefinition Get(string id)
        {
            return _recipes.Find(recipe => recipe.Id == id);
        }
    }
}
