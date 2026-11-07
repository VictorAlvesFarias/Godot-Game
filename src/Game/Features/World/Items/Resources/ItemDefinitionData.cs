using Godot;
using Jogo25D.Actions;
using Jogo25D.Effects;
using Jogo25D.Properties;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.World.Items.Resources
{
    public partial class ItemDefinitionData : Resource
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public string Id { get; set; }

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<BasePropertyData> Properties { get; set; } = new();

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<BasePropertyData> Modifiers { get; set; } = new();

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<EffectDefinitionData> Effects { get; set; } = new();

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<ActionDefinitionData> UnlockedAbilities { get; set; } = new Godot.Collections.Array<ActionDefinitionData>();

        #endregion
    }
}