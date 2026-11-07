using Godot;
using Jogo25D.Characters;
using Jogo25D.Dimensions;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Props;
using Jogo25D.Systems;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Crafting
{
    public static class CraftingSystem
    {
        // Distancia maxima, em tiles, do centro do jogador ao centro de um prop de estacao.
        public const float StationRangeTiles = 4f;

        public static float StationRange => StationRangeTiles * Dimension.TileSize;

        // Estacoes oferecidas pelos props da dimensao do jogador dentro do alcance.
        public static HashSet<string> StationsNear(Player player)
        {
            var stations = new HashSet<string>();

            if (player == null || Dimension.Get(player.GetActiveDimensionId())?.Entities is not Node entities)
            {
                return stations;
            }

            foreach (var prop in entities.GetChildren().OfType<Prop>())
            {
                var station = prop.Definition?.Station;

                if (!string.IsNullOrEmpty(station) && !prop.IsQueuedForDeletion() && prop.GlobalPosition.DistanceTo(player.GlobalPosition) <= StationRange)
                {
                    stations.Add(station);
                }
            }

            return stations;
        }

        // Receitas que o jogador pode ver agora: sem estacao, e as das estacoes proximas, na ordem do cadastro.
        public static List<RecipeDefinition> Available(HashSet<string> stations)
        {
            return RecipeDB.All.Where(recipe => recipe.Station == null || stations.Contains(recipe.Station)).ToList();
        }

        public static bool HasIngredients(InventoryData inventory, RecipeDefinition recipe)
        {
            return recipe.Ingredients.All(ingredient => InventorySystem.CountItem(inventory, ingredient.ItemId) >= ingredient.Quantity);
        }
    }
}
