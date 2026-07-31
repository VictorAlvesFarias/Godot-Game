using Godot;
using System;
using System.Collections.Generic;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Effects;
using Jogo25D.Items;
using Jogo25D.Properties;

namespace Jogo25D.Actions
{
    public static class ActionFactory
    {
        private static Dictionary<string, Func<ActionDefinition>> Recipes { get; set; }
        public static bool Initialized { get; set; }

        public static void Initialize()
        {
            if (Initialized)
            {
                return;
            }

            Recipes = new Dictionary<string, Func<ActionDefinition>>
            {
                ["dash"] = () => new DashDefinition
                {
                    Id = "dash",
                    ActionName = "Dash",
                    Cooldown = 1f,
                    Duration = 0.2f,
                    MaxCharges = 2,
                    Icon = GD.Load<Texture2D>(Textures.Items.DASH_ICON),
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DashPropertyData { DashSpeed = 800f, MovementInfluence = 0.4f }
                    }
                },

                ["fireball"] = () => new FireballDefinition
                {
                    Id = "fireball",
                    ActionName = "Fireball",
                    Cooldown = 1f,
                    Duration = 0.2f,
                    MaxCharges = 2,
                    Icon = GD.Load<Texture2D>(Textures.Items.FIREBALL_ICON),
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamagePropertyData { DamageAmount = 15, DamageType = DamageType.Physical },
                        new AttackPropertyData { AttackRange = 1500f, AttackArea = 50f, ProjectileSpeed = 800f },
                    },
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Fireball.tscn")
                },

                ["ground_strike"] = () => new GroundStrikeDefinition
                {
                    Id = "ground_strike",
                    ActionName = "Ground Strike",
                    Cooldown = 6f,
                    Duration = 0.1f,
                    MaxCharges = 1,
                    Icon = GD.Load<Texture2D>(Textures.Items.GROUND_STRIKE_ICON),
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamagePropertyData { DamageAmount = 20, DamageType = DamageType.Physical },
                        new AttackPropertyData { AttackArea = 20f, AttackRange = 1000f },
                    },
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/GroundStrike.tscn")
                },
            };

            Initialized = true;
        }

        public static ActionDefinition Create(string id)
        {
            if (!Initialized)
            {
                Initialize();
            }

            if (string.IsNullOrEmpty(id) || !Recipes.TryGetValue(id, out var recipe))
            {
                return null;
            }

            return recipe();
        }

        public static IEnumerable<string> GetAllIds()
        {
            if (!Initialized)
            {
                Initialize();
            }

            return Recipes.Keys;
        }

        public static ActionDefinitionData CreateInstance(string id)
        {
            var def = Create(id) ?? throw new System.Exception($"[ActionFactory] Acao '{id}' nao encontrada.");
            var instance = new ActionDefinitionData
            {
                Id = id,
                CurrentCharges = def.MaxCharges,
            };

            foreach (var effectId in def.Effects)
            {
                instance.Effects.Add(EffectDB.CreateInstance(effectId));
            }

            return instance;
        }
    }
}
