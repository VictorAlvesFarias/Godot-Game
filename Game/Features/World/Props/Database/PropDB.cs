using System.Collections.Generic;

namespace Jogo25D.Props
{
    public static class PropDB
    {
        public const string CraftingTableStation = "crafting_table";

        private static readonly Dictionary<string, PropDefinition> _props = new()
        {
            ["portal"] = new PropDefinition
            {
                Id = "portal",
                ScenePath = "res://Scenes/World/Props/Portal.tscn",
                ItemId = "portal",
            },
            ["crafting_table"] = new PropDefinition
            {
                Id = "crafting_table",
                ScenePath = "res://Scenes/World/Props/CraftingTable.tscn",
                ItemId = "crafting_table",
                Station = CraftingTableStation,
            },
        };

        public static IEnumerable<PropDefinition> All => _props.Values;

        public static PropDefinition Get(string id)
        {
            return _props.TryGetValue(id, out var definition) ? definition : null;
        }
    }
}
