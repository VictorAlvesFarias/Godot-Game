using Godot;
using Jogo25D.Actions;
using Jogo25D.Effects;
using Jogo25D.Features.Word.Items.Resources;
using Jogo25D.Properties;
using Jogo25D.Systems;
using Jogo25D.Utils.GodotDictionaryParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jogo25D.Features.Word.Characters.Resources
{
    public partial class PlayerData : Resource
    {
        [Export, GodotDictionaryField]
        public float Speed { get; set; } = 300.0f;

        [Export, GodotDictionaryField]
        public float JumpVelocity { get; set; } = -750.0f;

        [Export, GodotDictionaryField]
        public int MaxHealth { get; set; } = 50;

        [Export, GodotDictionaryField]
        public int CurrentHealth { get; set; } = 50;

        [Export, GodotDictionaryField]
        public bool CanUpdateMovement { get; set; } = true;

        [Export, GodotDictionaryField]
        public bool ReloadPending { get; set; } = true;

        [Export, GodotDictionaryField]
        public int EquippedSlotIndex { get; set; } = -1;

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<BasePropertyData> Buffs { get; set; } = new();

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<EffectDefinition> Effects { get; set; } = new();

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<ActionDefinitionData> UnlockedAbilities { get; set; } = new Godot.Collections.Array<ActionDefinitionData>();

        [Export, GodotDictionaryField]
        public InventoryData Inventory { get; set; } = new InventoryData();
    }
}
