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
            new RecipeDefinition
            {
                Id = "furnace",
                ResultItemId = "furnace",
                Ingredients = new List<(string, int)> { ("block_dirt", 20), ("item_wood", 4) },
                Station = PropDB.CraftingTableStation,
            },
            new RecipeDefinition
            {
                Id = "copper_bar",
                ResultItemId = "copper_bar",
                Ingredients = new List<(string, int)> { ("copper_ore", 3) },
                Station = PropDB.FurnaceStation,
            },
            new RecipeDefinition
            {
                Id = "iron_bar",
                ResultItemId = "iron_bar",
                Ingredients = new List<(string, int)> { ("iron_ore", 3) },
                Station = PropDB.FurnaceStation,
            },
            new RecipeDefinition
            {
                Id = "gold_bar",
                ResultItemId = "gold_bar",
                Ingredients = new List<(string, int)> { ("gold_ore", 3) },
                Station = PropDB.FurnaceStation,
            },
            new RecipeDefinition
            {
                Id = "copper_pickaxe",
                ResultItemId = "copper_pickaxe",
                Ingredients = new List<(string, int)> { ("copper_bar", 8), ("item_wood", 3) },
                Station = PropDB.CraftingTableStation,
            },
            new RecipeDefinition
            {
                Id = "iron_pickaxe",
                ResultItemId = "iron_pickaxe",
                Ingredients = new List<(string, int)> { ("iron_bar", 10), ("item_wood", 3) },
                Station = PropDB.CraftingTableStation,
            },
            new RecipeDefinition
            {
                Id = "anvil",
                ResultItemId = "anvil",
                Ingredients = new List<(string, int)> { ("iron_bar", 10) },
                Station = PropDB.CraftingTableStation,
            },
            new RecipeDefinition
            {
                Id = "copper_helmet",
                ResultItemId = "copper_helmet",
                Ingredients = new List<(string, int)> { ("copper_bar", 8) },
                Station = PropDB.AnvilStation,
            },
            new RecipeDefinition
            {
                Id = "copper_chestplate",
                ResultItemId = "copper_chestplate",
                Ingredients = new List<(string, int)> { ("copper_bar", 12) },
                Station = PropDB.AnvilStation,
            },
            new RecipeDefinition
            {
                Id = "copper_leggings",
                ResultItemId = "copper_leggings",
                Ingredients = new List<(string, int)> { ("copper_bar", 10) },
                Station = PropDB.AnvilStation,
            },
            new RecipeDefinition
            {
                Id = "iron_helmet",
                ResultItemId = "iron_helmet",
                Ingredients = new List<(string, int)> { ("iron_bar", 10) },
                Station = PropDB.AnvilStation,
            },
            new RecipeDefinition
            {
                Id = "iron_chestplate",
                ResultItemId = "iron_chestplate",
                Ingredients = new List<(string, int)> { ("iron_bar", 16) },
                Station = PropDB.AnvilStation,
            },
            new RecipeDefinition
            {
                Id = "iron_leggings",
                ResultItemId = "iron_leggings",
                Ingredients = new List<(string, int)> { ("iron_bar", 12) },
                Station = PropDB.AnvilStation,
            },
            new RecipeDefinition
            {
                Id = "gold_helmet",
                ResultItemId = "gold_helmet",
                Ingredients = new List<(string, int)> { ("gold_bar", 12) },
                Station = PropDB.AnvilStation,
            },
            new RecipeDefinition
            {
                Id = "gold_chestplate",
                ResultItemId = "gold_chestplate",
                Ingredients = new List<(string, int)> { ("gold_bar", 20) },
                Station = PropDB.AnvilStation,
            },
            new RecipeDefinition
            {
                Id = "gold_leggings",
                ResultItemId = "gold_leggings",
                Ingredients = new List<(string, int)> { ("gold_bar", 15) },
                Station = PropDB.AnvilStation,
            },
            new RecipeDefinition
            {
                Id = "swift_boots",
                ResultItemId = "swift_boots",
                Ingredients = new List<(string, int)> { ("iron_bar", 6) },
                Station = PropDB.AnvilStation,
            },
            new RecipeDefinition
            {
                Id = "life_amulet",
                ResultItemId = "life_amulet",
                Ingredients = new List<(string, int)> { ("gold_bar", 4) },
                Station = PropDB.AnvilStation,
            },
        };

        public static IReadOnlyList<RecipeDefinition> All => _recipes;

        public static RecipeDefinition Get(string id)
        {
            return _recipes.Find(recipe => recipe.Id == id);
        }
    }
}
