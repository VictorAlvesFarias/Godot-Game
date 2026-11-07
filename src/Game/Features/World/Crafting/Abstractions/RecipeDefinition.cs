using System.Collections.Generic;

namespace Jogo25D.Crafting
{
    public class RecipeDefinition
    {
        #region Dinamic properties

        public string Id { get; init; }
        public string ResultItemId { get; init; }
        public int ResultQuantity { get; init; } = 1;
        public IReadOnlyList<(string ItemId, int Quantity)> Ingredients { get; init; } = new List<(string, int)>();

        // Estacao exigida (PropDefinition.Station), ou null para criar em qualquer lugar.
        public string Station { get; init; }

        #endregion
    }
}
