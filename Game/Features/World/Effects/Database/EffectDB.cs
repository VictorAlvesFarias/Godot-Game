using Godot;
using Jogo25D.Constants;
using Jogo25D.Features.World.Properties.Resources;
using Jogo25D.Features.World.Resolver.Singletons;
using Jogo25D.Properties;
using System.Collections.Generic;

namespace Jogo25D.Effects
{
    public static class EffectDB
    {
        #region Properties

        public static Dictionary<string, EffectDefinition> Effects { get; set; }
        public static bool Initialized { get; set; }

        #endregion

        #region Core - Setup

        public static void Initialize()
        {
            if (Initialized)
            {
                return;
            }

            Effects = new Dictionary<string, EffectDefinition>();

            var poisonDamage = new DamageEffectDefinition
            {
                Id = "poison_damage",
                Name = "Veneno",
                Description = "Causa dano ao longo do tempo",
                Icon = GD.Load<Texture2D>(Textures.Items.EFFECT_POISON_DAMAGE_ICON),
                Duration = 5f,
                Type = EffectTriggerType.OnUse,
                ApplyTo = EffectApply.ToOwner,
                Damages = new Godot.Collections.Array<Items.DamageInfo>
                {
                    new Items.DamageInfo
                    {
                        Type = Items.DamageType.Physical,
                        Amount = 5,
                        SourcePeerId = -1,
                        CritChance = 0.2f,
                        CritDamage = 0.5f
                    }
                }
            };

            var fireDamage = new DamageEffectDefinition
            {
                Id = "fire_damage",
                Name = "Queimadura",
                Description = "Causa dano de fogo ao longo do tempo",
                Icon = GD.Load<Texture2D>(Textures.Items.FIRE_DAMAGE_POTION_ICON),
                Duration = 4f,
                Type = EffectTriggerType.OnUse,
                ApplyTo = EffectApply.ToOwner,
                Damages = new Godot.Collections.Array<Items.DamageInfo>
                {
                    new Items.DamageInfo
                    {
                        Type = Items.DamageType.Fire,
                        Amount = 8,
                        SourcePeerId = -1,
                        CritChance = 0f,
                        CritDamage = 0f
                    }
                }
            };

            var healthRegen = new DamageEffectDefinition
            {
                Id = "health_regen",
                Name = "Regeneração",
                Description = "Recupera vida ao longo do tempo",
                Icon = GD.Load<Texture2D>(Textures.Items.HEALTH_REGEN_POTION_ICON),
                Duration = 5f,
                Type = EffectTriggerType.OnUse,
                ApplyTo = EffectApply.ToOwner,
                Damages = new Godot.Collections.Array<Items.DamageInfo>
                {
                    new Items.DamageInfo
                    {
                        Type = Items.DamageType.Physical,
                        Amount = -6,
                        SourcePeerId = -1,
                        CritChance = 0f,
                        CritDamage = 0f
                    }
                }
            };

            var instantHeal = new InstantHealEffectDefinition
            {
                Id = "instant_heal",
                Name = "Cura Instantânea",
                Description = "Recupera vida na hora",
                Icon = GD.Load<Texture2D>(Textures.Items.INSTANT_HEAL_POTION_ICON),
                Duration = 0f,
                Type = EffectTriggerType.OnUse,
                ApplyTo = EffectApply.ToOwner,
                HealAmount = 30
            };

            var speedBoost = new StatBoostEffectDefinition
            {
                Id = "speed_boost",
                Name = "Velocidade",
                Description = "Aumenta a velocidade de movimento temporariamente",
                Icon = GD.Load<Texture2D>(Textures.Items.SPEED_POTION_ICON),
                Duration = 6f,
                Type = EffectTriggerType.OnUse,
                ApplyTo = EffectApply.ToOwner,
                Modifiers = new Godot.Collections.Array<BasePropertyData>
                {
                    new MovementPropertyData { Speed = 120f }
                }
            };

            Register(poisonDamage);
            Register(fireDamage);
            Register(healthRegen);
            Register(instantHeal);
            Register(speedBoost);

            Initialized = true;
        }

        public static void Register(EffectDefinition definition)
        {
            Effects ??= new Dictionary<string, EffectDefinition>();
            Effects[definition.Id] = definition;
        }

        #endregion

        #region Core - Query

        public static EffectDefinition Get(string id)
        {
            if (!Initialized)
            {
                Initialize();
            }

            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            Effects.TryGetValue(id, out var def);

            return def;
        }

        public static bool TryGet(string id, out EffectDefinition definition)
        {
            if (!Initialized)
            {
                Initialize();
            }

            return Effects.TryGetValue(id, out definition);
        }

        public static IEnumerable<string> GetAllIds()
        {
            if (!Initialized)
            {
                Initialize();
            }

            return Effects.Keys;
        }

        #endregion

        #region Core - Instancing

        private static long _nextInstanceId = System.BitConverter.ToInt64(System.Guid.NewGuid().ToByteArray(), 0) & 0x7FFFFFFFFFFFFFFL;

        public static long NextInstanceId()
        {
            return ++_nextInstanceId;
        }

        public static EffectDefinitionData CreateInstance(string id)
        {
            if (!Initialized)
            {
                Initialize();
            }

            var def = Get(id) ?? throw new System.Exception($"[EffectDB] Efeito '{id}' nao encontrado.");
            var instance = new EffectDefinitionData(id);

            instance.InstanceId = NextInstanceId();
            instance.Duration = def.Duration;
            instance.Infinite = def.Infinite;
            instance.Type = def.Type;
            instance.ApplyTo = def.ApplyTo;

            return instance;
        }

        #endregion
    }
}
