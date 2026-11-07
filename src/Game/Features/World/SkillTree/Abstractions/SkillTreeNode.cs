using Godot;
using Jogo25D.Properties;
using System.Collections.Generic;

namespace Jogo25D.SkillTree
{
    public class SkillTreeNode
    {
        #region Dinamic properties

        public string Id { get; init; } = "";
        public string TreeId { get; init; } = "main";
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public Texture2D Icon { get; set; }
        public int MaxLevel { get; init; } = 1;
        public int RequiredTreeLevel { get; init; } = 0;
        public List<SkillTreeDependency> Dependencies { get; set; } = new();
        public Godot.Collections.Array<BasePropertyData> Properties { get; set; } = new();
        public Godot.Collections.Array<string> Effects { get; set; } = new();
        public Godot.Collections.Array<string> UnlockedAbilities { get; set; } = new();

        #endregion
    }
}
