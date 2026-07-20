using Godot;
using Jogo25D.Actions;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Effects;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Features.World.Properties.Resources;
using Jogo25D.Features.World.Resolver.Singletons;
using Jogo25D.Properties;
using System.Collections.Generic;

namespace Jogo25D.Items
{
    public static class ItemDB
    {
        #region Properties

        public static Dictionary<string, ItemDefinition> Items { get; set; }
        public static bool Initialized { get; set; }

        #endregion

        #region Core - Setup

        public static void Initialize()
        {
            if (Initialized)
            {
                return;
            }

            Items = new Dictionary<string, ItemDefinition>();

            var swordStarting = new WeaponDefinition {
                Id = "sword_starting",
                Name = "Sword",
                Type = ItemType.WeaponMelee,
                Description = "Uma espada bÃ¡sica para combate corpo a corpo",
                Cooldown = 0.5f,
                Icon = GD.Load<Texture2D>(Assets.Icons.Swords.ICON_SWORD_17),
                HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Sword.tscn"),
                Properties = new Godot.Collections.Array<BasePropertyData>
                {   
                    new DamagePropertyData { DamageAmount = 15, DamageType = DamageType.Physical },
                    new AttackPropertyData { AttackRange = 80f, KnockbackForce = 200f },
                    new ChargesPropertyData { InfiniteCharges = true }
                }
            };

            var bowStarting = new WeaponDefinition {
                Id = "bow_starting",
                Name = "Arco",
                Type = ItemType.WeaponRanged,
                Description = "Um arco para ataques Ã  distÃ¢ncia",
                Cooldown = 0.8f,
                Icon = GD.Load<Texture2D>(Assets.Icons.Bows.ICON_BOW_10) ,
                HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Projectile.tscn"),
                Properties = new Godot.Collections.Array<BasePropertyData>
                {
                    new DamagePropertyData { DamageAmount = 10, DamageType = DamageType.Physical },
                    new AttackPropertyData { AttackRange = 1500f, AttackArea = 50f, ProjectileSpeed = 750f },
                    new ChargesPropertyData { InfiniteCharges = false, MaxCharges = 10, ChargeItemId = "arrow", ReloadCooldown = 1.0f }
                }
            };

            var fireballStarting = new WeaponDefinition {
                Id = "bow_starting2",
                Name = "Arco2",
                Type = ItemType.WeaponRanged,
                Description = "Um arco melhorado para ataques Ã  distÃ¢ncia",
                Cooldown = 1f,
                Icon = GD.Load<Texture2D>(Assets.Icons.Bows.ICON_BOW_11),
                HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Projectile.tscn"),
                Properties = new Godot.Collections.Array<BasePropertyData>
                {
                    new DamagePropertyData { DamageAmount = 5, DamageType = DamageType.Fire },
                    new AttackPropertyData { AttackRange = 1000f, AttackArea = 15f, ProjectileSpeed = 1000f },
                    new ChargesPropertyData { InfiniteCharges = true, MaxCharges = 1, ReloadCooldown = 1.5f }
                }
            };

            var arrow = new ConsumableDefinition {
                Id = "arrow",
                Name = "Flecha",
                Type = ItemType.Consumable,
                Description = "MuniÃ§Ã£o para arcos",
                Stackable = true,
                MaxStackSize = 9999,
                Icon = GD.Load<Texture2D>(Assets.Icons.Bows.ICON_BOW_40),
                Properties = new Godot.Collections.Array<BasePropertyData>
                {
                    new ChargesPropertyData { ChargeItemId = "arrow" }
                }
            };

            var swordBasic = new WeaponDefinition {
                Id = "sword_basic",
                Name = "Espada BÃ¡sica",
                Type = ItemType.WeaponMelee,
                Cooldown = 0.6f,
                Icon = ResourceLoader.Exists(Assets.Icons.Swords.ICON_SWORD_1) ? GD.Load<Texture2D>(Assets.Icons.Swords.ICON_SWORD_1) : null,
                HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Sword.tscn"),
                Properties = new Godot.Collections.Array<BasePropertyData>
                {
                    new DamagePropertyData { DamageAmount = 15, DamageType = DamageType.Physical },
                    new AttackPropertyData { AttackRange = 80f, AttackArea = 30f, KnockbackForce = 250f },
                    new CritPropertyData { CritChance = 0.1f, CritDamage = 0.5f },
                    new ChargesPropertyData { InfiniteCharges = true }
                }
            };

            var bowBasic = new WeaponDefinition {
                Id = "bow_basic",
                Name = "Arco BÃ¡sico",
                Type = ItemType.WeaponRanged,
                Cooldown = 0.8f,
                Icon = GD.Load<Texture2D>(Assets.Icons.Bows.ICON_BOW_1),
                HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Projectile.tscn"),
                Properties = new Godot.Collections.Array<BasePropertyData>
                {
                    new DamagePropertyData { DamageAmount = 10, DamageType = DamageType.Physical },
                    new AttackPropertyData { AttackRange = 1500f, AttackArea = 150f, ProjectileSpeed = 70f },
                    new ChargesPropertyData { InfiniteCharges = false, MaxCharges = 10, ChargeItemId = "arrow", ReloadCooldown = 1.0f }
                }
            };

            var poisonFlask = new ConsumableDefinition {
                Id = "poison_flask",
                Name = "Frasco de Veneno",
                Type = ItemType.Consumable,
                Stackable = true,
                MaxStackSize = 10,
                Icon = GD.Load<Texture2D>(Assets.Icons.Potions.ICON_POTION_1),
                Effects = new Godot.Collections.Array<string>
                {
                    "poison_damage"
                }
            };

            Register(swordStarting);
            Register(bowStarting);
            Register(fireballStarting);
            Register(arrow);
            Register(swordBasic);
            Register(bowBasic);
            Register(poisonFlask);

            Initialized = true;
        }

        public static void Register(ItemDefinition definition)
        {
            if (Items == null)
            {
                Items = new Dictionary<string, ItemDefinition>();
            }
            Items[definition.Id] = definition;
        }

        #endregion

        #region Core - Query

        public static ItemDefinition Get(string id)
        {
            if (!Initialized)
            {
                Initialize();
            }

            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            Items.TryGetValue(id, out var def);

            return def;
        }

        public static bool TryGet(string id, out ItemDefinition definition)
        {
            if (!Initialized)
            {
                Initialize();
            }

            return Items.TryGetValue(id, out definition);
        }

        public static IEnumerable<string> GetAllIds()
        {
            if (!Initialized)
            {
                Initialize();
            }

            return Items.Keys;
        }

        #endregion

        #region Core - Instancing

        private static long _nextInstanceId = System.BitConverter.ToInt64(System.Guid.NewGuid().ToByteArray(), 0) & 0x7FFFFFFFFFFFFFFL;

        public static long NextInstanceId()
        {
            return ++_nextInstanceId;
        }

        public static ItemDefinitionData CreateInstance(string id)
        {
            if (!Initialized)
            {
                Initialize();
            }

            var def = Get(id) ?? throw new System.Exception($"[ItemDB] Item '{id}' nao encontrado.");
            var instance = new ItemDefinitionData(id);

            instance.InstanceId = NextInstanceId();
            instance.Quantity = 1;
            instance.Properties = Resolver.CloneProperties(def.Properties);

            foreach (var effectId in def.Effects)
            {
                instance.Effects.Add(EffectDB.CreateInstance(effectId));
            }

            return instance;
        }

        #endregion
    }
}
