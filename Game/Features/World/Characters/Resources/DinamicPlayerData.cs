using Godot;
using Jogo25D.Actions;
using Jogo25D.Effects;
using Jogo25D.Features.World.Properties.Resources;
using Jogo25D.Properties;
using Jogo25D.Utils.GodotDictionaryParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jogo25D.Features.World.Characters.Resources
{
    public partial class DinamicPlayerData : Resource
    {
        [Export, GodotDictionaryField]
        public Godot.Collections.Array<BasePropertyData> Properties { get; set; } = new() { new MovementPropertyData(), new HealthPropertyData() };

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<BasePropertyData> Buffs { get; set; } = new();

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<EffectDefinitionData> CurrentEffects { get; set; } = new();

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<ActionDefinitionData> UnlockedAbilities { get; set; } = new Godot.Collections.Array<ActionDefinitionData>();
    }
}
