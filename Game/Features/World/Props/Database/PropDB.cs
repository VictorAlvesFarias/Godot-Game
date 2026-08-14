using System.Collections.Generic;

namespace Jogo25D.Props
{
    public static class PropDB
    {
        private static readonly Dictionary<string, PropDefinition> _props = new()
        {
            ["portal"] = new PropDefinition
            {
                Id = "portal",
                ScenePath = "res://Scenes/World/Props/Portal.tscn",
            },
        };

        public static PropDefinition Get(string id)
        {
            return _props.TryGetValue(id, out var definition) ? definition : null;
        }
    }
}
