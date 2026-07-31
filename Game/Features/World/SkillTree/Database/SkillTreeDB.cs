using Godot;
using Jogo25D.Constants;
using Jogo25D.Features.World.Properties.Resources;
using Jogo25D.Items;
using Jogo25D.Properties;
using System.Collections.Generic;

namespace Jogo25D.SkillTree
{
    public static class SkillTreeDB
    {
        #region Properties

        public static Dictionary<string, SkillTreeNode> Nodes { get; set; }
        public static bool Initialized { get; set; }

        #endregion

        #region Core - Setup

        public static void Initialize()
        {
            if (Initialized)
            {
                return;
            }

            Nodes = new Dictionary<string, SkillTreeNode>();

            var vitality1 = new SkillTreeNode
            {
                Id = "vitality_1",
                TreeId = "main",
                Name = "Vitalidade I",
                Description = "Aumenta a vida maxima.",
                Icon = GD.Load<Texture2D>(Textures.Skills.VITALITY_1_ICON),
                MaxLevel = 3,
                Properties = new Godot.Collections.Array<BasePropertyData>
                {
                    new HealthPropertyData { MaxHealth = 10 }
                }
            };

            var vitality2 = new SkillTreeNode
            {
                Id = "vitality_2",
                TreeId = "main",
                Name = "Vitalidade II",
                Description = "Aumenta ainda mais a vida maxima. Requer Vitalidade I nivel 2.",
                Icon = GD.Load<Texture2D>(Textures.Skills.VITALITY_2_ICON),
                MaxLevel = 2,
                Dependencies = new List<SkillTreeDependency>
                {
                    new SkillTreeDependency { NodeId = "vitality_1", MinLevel = 2 }
                },
                Properties = new Godot.Collections.Array<BasePropertyData>
                {
                    new HealthPropertyData { MaxHealth = 20 }
                }
            };

            var swiftSteps = new SkillTreeNode
            {
                Id = "swift_steps",
                TreeId = "main",
                Name = "Passos Ligeiros",
                Description = "Aumenta a velocidade de movimento.",
                Icon = GD.Load<Texture2D>(Textures.Skills.SWIFT_STEPS_ICON),
                MaxLevel = 3,
                Properties = new Godot.Collections.Array<BasePropertyData>
                {
                    new MovementPropertyData { Speed = 15f }
                }
            };

            var power1 = new SkillTreeNode
            {
                Id = "power_1",
                TreeId = "main",
                Name = "Poder I",
                Description = "Aumenta o dano fisico causado.",
                Icon = GD.Load<Texture2D>(Textures.Skills.POWER_1_ICON),
                MaxLevel = 3,
                Properties = new Godot.Collections.Array<BasePropertyData>
                {
                    new DamagePropertyData { DamageType = DamageType.Physical, DamageAmount = 5 }
                }
            };

            var arcaneDash = new SkillTreeNode
            {
                Id = "arcane_dash",
                TreeId = "main",
                Name = "Dash Arcano",
                Description = "Desbloqueia a habilidade de Dash. Requer Passos Ligeiros nivel 2 e nivel total 4 na arvore principal.",
                Icon = GD.Load<Texture2D>(Textures.Skills.ARCANE_DASH_ICON),
                MaxLevel = 1,
                RequiredTreeLevel = 4,
                Dependencies = new List<SkillTreeDependency>
                {
                    new SkillTreeDependency { NodeId = "swift_steps", MinLevel = 2 }
                },
                UnlockedAbilities = new Godot.Collections.Array<string>
                {
                    "dash"
                }
            };

            Register(vitality1);
            Register(vitality2);
            Register(swiftSteps);
            Register(power1);
            Register(arcaneDash);

            Initialized = true;
        }

        public static void Register(SkillTreeNode node)
        {
            Nodes ??= new Dictionary<string, SkillTreeNode>();
            Nodes[node.Id] = node;
        }

        #endregion

        #region Core - Query

        public static SkillTreeNode Get(string id)
        {
            if (!Initialized)
            {
                Initialize();
            }

            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            Nodes.TryGetValue(id, out var node);

            return node;
        }

        public static bool TryGet(string id, out SkillTreeNode node)
        {
            if (!Initialized)
            {
                Initialize();
            }

            return Nodes.TryGetValue(id, out node);
        }

        public static IEnumerable<string> GetAllIds()
        {
            if (!Initialized)
            {
                Initialize();
            }

            return Nodes.Keys;
        }

        #endregion

        #region Core - Progress lookup

        public static int GetNodeLevel(Godot.Collections.Array<SkillTreeNodeData> progress, string nodeId)
        {
            foreach (var entry in progress)
            {
                if (entry != null && entry.NodeId == nodeId)
                {
                    return entry.CurrentLevel;
                }
            }

            return 0;
        }

        public static int GetTreeLevel(Godot.Collections.Array<SkillTreeNodeData> progress, string treeId)
        {
            if (!Initialized)
            {
                Initialize();
            }

            var total = 0;

            foreach (var entry in progress)
            {
                if (entry == null)
                {
                    continue;
                }

                var node = Get(entry.NodeId);

                if (node != null && node.TreeId == treeId)
                {
                    total += entry.CurrentLevel;
                }
            }

            return total;
        }

        #endregion

        #region Core - Validation

        public static bool CanLevelUp(Godot.Collections.Array<SkillTreeNodeData> progress, string nodeId)
        {
            var node = Get(nodeId);

            if (node == null)
            {
                return false;
            }

            var currentLevel = GetNodeLevel(progress, nodeId);

            if (currentLevel >= node.MaxLevel)
            {
                return false;
            }

            var totalTreeLevel = GetTreeLevel(progress, node.TreeId);

            if (totalTreeLevel < node.RequiredTreeLevel)
            {
                return false;
            }

            foreach (var dependency in node.Dependencies)
            {
                if (GetNodeLevel(progress, dependency.NodeId) < dependency.MinLevel)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
