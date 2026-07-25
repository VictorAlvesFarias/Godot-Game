using Godot;
using Jogo25D.Actions;
using Jogo25D.Effects;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Features.World.Properties.Resources;
using Jogo25D.Properties;
using Jogo25D.SkillTree;
using Jogo25D.Systems;
using Jogo25D.Utils.GodotDictionaryParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jogo25D.Features.World.Characters.Resources
{
    public partial class  PlayerData : Resource
    {
        #region Properties

        [Export, GodotDictionaryField]
        public int CurrentHealth { get; set; } = 50;

        [Export, GodotDictionaryField]
        public bool CanUpdateMovement { get; set; } = true;

        [Export, GodotDictionaryField]
        public bool ReloadPending { get; set; } = true;

        [Export, GodotDictionaryField]
        public long EquippedItemId { get; set; } = 0;

        // PvE por padrao - so causa/recebe dano de outro player e colide
        // fisicamente com ele se AMBOS tiverem isso ligado (ver
        // Player.UpdatePvpCollisionExceptions e BaseHitbox.IsPvpBlocked).
        [Export, GodotDictionaryField]
        public bool PvpEnabled { get; set; } = false;

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<BasePropertyData> Properties { get; set; } = new()
        {
            new MovementPropertyData { Speed = 300f, JumpVelocity = -750f },
            new HealthPropertyData()
        };

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<EffectDefinitionData> CurrentEffects { get; set; } = new();

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<ActionDefinitionData> UnlockedAbilities { get; set; } = new Godot.Collections.Array<ActionDefinitionData>();

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<SkillTreeNodeData> SkillTree { get; set; } = new();

        [Export, GodotDictionaryField]
        public InventoryData Inventory { get; set; } = new InventoryData();

        #endregion
    }
}
