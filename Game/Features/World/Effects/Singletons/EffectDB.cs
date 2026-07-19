using Godot;
using Jogo25D.Constants;
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
                Icon = GD.Load<Texture2D>(Assets.Icons.Effects.ICON_EFFECT_1),
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

            Register(poisonDamage);

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
