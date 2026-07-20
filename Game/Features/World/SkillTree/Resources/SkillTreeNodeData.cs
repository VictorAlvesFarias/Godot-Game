using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.SkillTree
{
    public partial class SkillTreeNodeData : Resource
    {
        [Export, GodotDictionaryField]
        public string NodeId { get; set; } = "";

        [Export, GodotDictionaryField]
        public int CurrentLevel { get; set; } = 0;
    }
}
