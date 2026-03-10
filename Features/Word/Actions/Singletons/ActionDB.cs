using Godot;
using System.Collections.Generic;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Items;
using Jogo25D.Properties;

namespace Jogo25D.Actions
{
    public static class ActionDB
    {
        public static Dictionary<string, ActionDefinition> Actions { get; set; }
        public static bool Initialized { get; set; }

        public static void Initialize()
        {
            if (Initialized)
            {
                return;
            }

            Actions = new Dictionary<string, ActionDefinition>();

            Register(new DashDefinition
            {
                Id = "dash",
                ActionName = "Dash",
                Cooldown = 1f,
                Duration = 0.2f,
                MaxCharges = 2,
                Icon = GD.Load<Texture2D>(Assets.Icons.Spells.ICON_SPELL_10),
                DashSpeed = 800f,
                MovementInfluence = 0.4f,
            });
            Register(new FireballDefinition
            {
                Id = "fireball",
                ActionName = "Fireball",
                Cooldown = 1f,
                Duration = 0.2f,
                MaxCharges = 2,
                Icon = GD.Load<Texture2D>(Assets.Icons.Spells.ICON_SPELL_4),
                Properties = new List<BaseProperty>
                {
                    new DamageProperty { DamageAmount = 15, DamageType = DamageType.Physical } ,
                    new AttackProperty { AttackRange = 1500f, AttackArea = 50f, ProjectileSpeed = 75f },

                },
                HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Fireball.tscn")
            });
            Register(new GroundStrikeDefinition
            {
                Id = "ground_strike",
                ActionName = "Ground Strike",
                Cooldown = 6f,
                Duration = 0.1f,
                MaxCharges = 1,
                Icon = GD.Load<Texture2D>(Assets.Icons.Spells.ICON_SPELL_7),
                Properties = new List<BaseProperty>
                {
                    new DamageProperty { DamageAmount = 20, DamageType = DamageType.Physical },
                    new AttackProperty  { AttackArea = 20f, AttackRange = 1000f },
                },
                HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/GroundStrike.tscn")
            });

            Initialized = true;
        }

        public static void Register(ActionDefinition definition)
        {
            Actions ??= new Dictionary<string, ActionDefinition>();
            Actions[definition.Id] = definition;
        }

        public static ActionDefinition Get(string id)
        {
            if (!Initialized)
            {
                Initialize();
            }
            Actions.TryGetValue(id, out var def);
            return def;
        }

        public static bool TryGet(string id, out ActionDefinition definition)
        {
            if (!Initialized)
            {
                Initialize();
            }
            return Actions.TryGetValue(id, out definition);
        }

        public static ActionInstance CreateInstance(string id, Player player)
        {
            if (!Initialized)
            {
                Initialize();
            }

            var def = Get(id) ?? throw new System.Exception($"[ActionDB] AÃ§Ã£o '{id}' nÃ£o encontrada.");
            var instance = new ActionInstance(def, player);

            def.OnCreate(player, instance);
            
            return instance;
        }

        public static IEnumerable<string> GetAllIds()
        {
            if (!Initialized)
            {
                Initialize();
            }
            
            return Actions.Keys;
        }
    }
}