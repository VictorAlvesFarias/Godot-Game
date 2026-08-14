using System.Collections.Generic;

namespace Jogo25D.Structures
{
    public static class StructureDB
    {
        private static readonly Dictionary<string, StructureDefinition> _structures = new()
        {
            ["tree"] = new TreeStructureDefinition
            {
                Id = "tree",
                Chance = 0.82f,
            },
        };

        public static StructureDefinition Get(string id)
        {
            return _structures.TryGetValue(id, out var definition) ? definition : null;
        }
    }
}
