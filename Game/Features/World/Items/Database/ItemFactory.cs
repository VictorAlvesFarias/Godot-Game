using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Effects;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Features.World.Properties.Resources;
using Jogo25D.Properties;
using System;
using System.Collections.Generic;

namespace Jogo25D.Items
{
    public static class ItemFactory
    {
        #region Properties

        private static Dictionary<string, Func<ItemDefinition>> Recipes { get; set; }
        public static bool Initialized { get; set; }

        #endregion

        #region Core - Setup

        public static void Initialize()
        {
            if (Initialized)
            {
                return;
            }

            Recipes = new Dictionary<string, Func<ItemDefinition>>
            {
                ["sword_starting"] = () => new WeaponDefinition
                {
                    Id = "sword_starting",
                    Name = "Sword",
                    Type = ItemType.WeaponMelee,
                    Description = "Uma espada bÃ¡sica para combate corpo a corpo",
                    Cooldown = 0.5f,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Swords.ICON_SWORD_17),
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Swords/SwordStarting.tscn"),
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamagePropertyData { DamageAmount = 15, DamageType = DamageType.Physical },
                        new AttackPropertyData { AttackRange = 80f, KnockbackForce = 200f },
                        new ChargesPropertyData { InfiniteCharges = true }
                    }
                },

                ["bow_starting"] = () => new WeaponDefinition
                {
                    Id = "bow_starting",
                    Name = "Arco",
                    Type = ItemType.WeaponRanged,
                    Description = "Um arco para ataques Ã  distÃ¢ncia",
                    Cooldown = 0.8f,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Bows.ICON_BOW_10),
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Projectile.tscn"),
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamagePropertyData { DamageAmount = 10, DamageType = DamageType.Physical },
                        new AttackPropertyData { AttackRange = 1500f, AttackArea = 50f, ProjectileSpeed = 750f },
                        new ChargesPropertyData { InfiniteCharges = false, MaxCharges = 10, ChargeItemId = "arrow", ReloadCooldown = 1.0f }
                    }
                },

                ["bow_starting2"] = () => new WeaponDefinition
                {
                    Id = "bow_starting2",
                    Name = "Arco2",
                    Type = ItemType.WeaponRanged,
                    Description = "Um arco melhorado para ataques Ã  distÃ¢ncia",
                    Cooldown = 1f,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Bows.ICON_BOW_11),
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Projectile.tscn"),
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamagePropertyData { DamageAmount = 5, DamageType = DamageType.Fire },
                        new AttackPropertyData { AttackRange = 1000f, AttackArea = 15f, ProjectileSpeed = 1000f },
                        new ChargesPropertyData { InfiniteCharges = true, MaxCharges = 1, ReloadCooldown = 1.5f }
                    }
                },

                ["pickaxe_starting"] = () => new ToolDefinition
                {
                    Id = "pickaxe_starting",
                    Name = "Picareta",
                    Type = ItemType.Tool,
                    Description = "Usada para quebrar blocos do mundo",
                    Cooldown = 0.35f,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Pickaxes.ICON_PICKAXE_1),
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Tools/PickaxeStarting.tscn"),
                },

                ["block_grass"] = () => new BlockItemDefinition
                {
                    Id = "block_grass",
                    Name = "Bloco de Grama",
                    Type = ItemType.Block,
                    Description = "Um bloco de terra com grama, pode ser colocado no mundo",
                    BlockId = "grass",
                    Stackable = true,
                    MaxStackSize = 999,
                    Cooldown = 0.25f,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Blocks.ICON_GRASS_BLOCK),
                },

                ["arrow"] = () => new ConsumableDefinition
                {
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
                },

                ["sword_basic"] = () => new WeaponDefinition
                {
                    Id = "sword_basic",
                    Name = "Espada BÃ¡sica",
                    Type = ItemType.WeaponMelee,
                    Cooldown = 0.6f,
                    Icon = ResourceLoader.Exists(Assets.Icons.Swords.ICON_SWORD_1) ? GD.Load<Texture2D>(Assets.Icons.Swords.ICON_SWORD_1) : null,
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Swords/SwordBasic.tscn"),
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamagePropertyData { DamageAmount = 15, DamageType = DamageType.Physical },
                        new AttackPropertyData { AttackRange = 80f, AttackArea = 30f, KnockbackForce = 250f },
                        new CritPropertyData { CritChance = 0.1f, CritDamage = 0.5f },
                        new ChargesPropertyData { InfiniteCharges = true }
                    }
                },

                ["fire_slash_sword"] = () => new WeaponDefinition
                {
                    Id = "fire_slash_sword",
                    Name = "Espada Flamejante",
                    Type = ItemType.WeaponMelee,
                    Description = "Uma espada envolta em chamas",
                    Cooldown = 0.5f,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Swords.ICON_SWORD_19),
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Swords/FireSword.tscn"),
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamagePropertyData { DamageAmount = 16, DamageType = DamageType.Fire },
                        new AttackPropertyData { AttackRange = 85f, AttackArea = 30f, KnockbackForce = 200f },
                        new ChargesPropertyData { InfiniteCharges = true }
                    }
                },

                ["blue_fire_sword"] = () => new WeaponDefinition
                {
                    Id = "blue_fire_sword",
                    Name = "Espada Congelante",
                    Type = ItemType.WeaponMelee,
                    Description = "Uma espada envolta em chamas geladas",
                    Cooldown = 0.5f,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Swords.ICON_SWORD_43),
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Swords/IceSword.tscn"),
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamagePropertyData { DamageAmount = 14, DamageType = DamageType.Ice },
                        new AttackPropertyData { AttackRange = 85f, AttackArea = 30f, KnockbackForce = 180f },
                        new ChargesPropertyData { InfiniteCharges = true }
                    }
                },

                ["blue_fire_sword_alt"] = () => new WeaponDefinition
                {
                    Id = "blue_fire_sword_alt",
                    Name = "Espada Congelante II",
                    Type = ItemType.WeaponMelee,
                    Description = "Variação da espada congelante",
                    Cooldown = 0.5f,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Swords.ICON_SWORD_47),
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Swords/IceSwordAlt.tscn"),
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamagePropertyData { DamageAmount = 14, DamageType = DamageType.Ice },
                        new AttackPropertyData { AttackRange = 85f, AttackArea = 30f, KnockbackForce = 180f },
                        new ChargesPropertyData { InfiniteCharges = true }
                    }
                },

                ["dark_slash_sword"] = () => new WeaponDefinition
                {
                    Id = "dark_slash_sword",
                    Name = "Espada Sombria",
                    Type = ItemType.WeaponMelee,
                    Description = "Uma espada envolta em energia sombria - o dano ignora resistências",
                    Cooldown = 0.65f,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Swords.ICON_SWORD_25),
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Swords/DarkSword.tscn"),
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamagePropertyData { DamageAmount = 20, DamageType = DamageType.True },
                        new AttackPropertyData { AttackRange = 90f, AttackArea = 35f, KnockbackForce = 220f },
                        new ChargesPropertyData { InfiniteCharges = true }
                    }
                },

                ["whip"] = () => new WeaponDefinition
                {
                    Id = "whip",
                    Name = "Chicote",
                    Type = ItemType.WeaponMelee,
                    Description = "Alcance maior, dano menor",
                    Cooldown = 0.4f,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Swords.ICON_SWORD_56),
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Swords/WhipSword.tscn"),
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamagePropertyData { DamageAmount = 10, DamageType = DamageType.Physical },
                        new AttackPropertyData { AttackRange = 140f, AttackArea = 25f, KnockbackForce = 100f },
                        new ChargesPropertyData { InfiniteCharges = true }
                    }
                },

                ["real_sword"] = () => new WeaponDefinition
                {
                    Id = "real_sword",
                    Name = "Espada Real",
                    Type = ItemType.WeaponMelee,
                    Description = "Uma espada de verdade",
                    Cooldown = 0.5f,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Swords.ICON_SWORD_2),
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Swords/RealSword.tscn"),
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamagePropertyData { DamageAmount = 15, DamageType = DamageType.Physical },
                        new AttackPropertyData { AttackRange = 80f, AttackArea = 30f, KnockbackForce = 200f },
                        new ChargesPropertyData { InfiniteCharges = true }
                    }
                },

                ["green_blow_sword"] = () => new WeaponDefinition
                {
                    Id = "green_blow_sword",
                    Name = "Lâmina Venenosa",
                    Type = ItemType.WeaponMelee,
                    Description = "Uma lâmina impregnada de veneno",
                    Cooldown = 0.5f,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Swords.ICON_SWORD_21),
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Swords/PoisonSword.tscn"),
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamagePropertyData { DamageAmount = 10, DamageType = DamageType.Poison },
                        new AttackPropertyData { AttackRange = 85f, AttackArea = 30f, KnockbackForce = 150f },
                        new ChargesPropertyData { InfiniteCharges = true }
                    }
                },

                ["gold_sword"] = () => new WeaponDefinition
                {
                    Id = "gold_sword",
                    Name = "Espada Dourada",
                    Type = ItemType.WeaponMelee,
                    Description = "Uma espada de ouro",
                    Cooldown = 0.5f,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Swords.ICON_SWORD_10),
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Swords/GoldSword.tscn"),
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamagePropertyData { DamageAmount = 15, DamageType = DamageType.Physical },
                        new AttackPropertyData { AttackRange = 80f, AttackArea = 30f, KnockbackForce = 200f },
                        new ChargesPropertyData { InfiniteCharges = true }
                    }
                },

                ["silver_sword"] = () => new WeaponDefinition
                {
                    Id = "silver_sword",
                    Name = "Espada de Prata",
                    Type = ItemType.WeaponMelee,
                    Description = "Uma espada de prata",
                    Cooldown = 0.5f,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Swords.ICON_SWORD_14),
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Swords/SilverSword.tscn"),
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamagePropertyData { DamageAmount = 15, DamageType = DamageType.Physical },
                        new AttackPropertyData { AttackRange = 80f, AttackArea = 30f, KnockbackForce = 200f },
                        new ChargesPropertyData { InfiniteCharges = true }
                    }
                },

                ["bow_basic"] = () => new WeaponDefinition
                {
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
                },

                ["poison_flask"] = () => new ConsumableDefinition
                {
                    Id = "poison_flask",
                    Name = "Frasco de Veneno",
                    Type = ItemType.Consumable,
                    Stackable = true,
                    MaxStackSize = 10,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Potions.ICON_POTION_19),
                    Effects = new Godot.Collections.Array<string> { "poison_damage" }
                },

                ["fire_damage_potion"] = () => new ConsumableDefinition
                {
                    Id = "fire_damage_potion",
                    Name = "Poção de Dano de Fogo",
                    Type = ItemType.Consumable,
                    Stackable = true,
                    MaxStackSize = 10,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Potions.ICON_POTION_22),
                    Effects = new Godot.Collections.Array<string> { "fire_damage" }
                },

                ["health_regen_potion"] = () => new ConsumableDefinition
                {
                    Id = "health_regen_potion",
                    Name = "Poção de Regeneração",
                    Type = ItemType.Consumable,
                    Stackable = true,
                    MaxStackSize = 10,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Potions.ICON_POTION_44),
                    Effects = new Godot.Collections.Array<string> { "health_regen" }
                },

                ["speed_potion"] = () => new ConsumableDefinition
                {
                    Id = "speed_potion",
                    Name = "Poção de Velocidade",
                    Type = ItemType.Consumable,
                    Stackable = true,
                    MaxStackSize = 10,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Potions.ICON_POTION_15),
                    Effects = new Godot.Collections.Array<string> { "speed_boost" }
                },

                ["instant_heal_potion"] = () => new ConsumableDefinition
                {
                    Id = "instant_heal_potion",
                    Name = "Poção de Cura Instantânea",
                    Type = ItemType.Consumable,
                    Stackable = true,
                    MaxStackSize = 10,
                    Icon = GD.Load<Texture2D>(Assets.Icons.Potions.ICON_POTION_13),
                    Effects = new Godot.Collections.Array<string> { "instant_heal" }
                },
            };

            Initialized = true;
        }

        #endregion

        #region Core - Query

        public static ItemDefinition Create(string id)
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

        #endregion

        #region Core - Instancing

        private static long _nextInstanceId = System.BitConverter.ToInt64(System.Guid.NewGuid().ToByteArray(), 0) & 0x7FFFFFFFFFFFFFFL;

        public static long NextInstanceId()
        {
            return ++_nextInstanceId;
        }

        public static ItemData CreateInstance(string id)
        {
            var def = Create(id) ?? throw new System.Exception($"[ItemFactory] Item '{id}' nao encontrado.");
            var instance = new ItemData(id)
            {
                InstanceId = NextInstanceId(),
                Quantity = 1,
            };

            foreach (var effectId in def.Effects)
            {
                instance.Effects.Add(EffectDB.CreateInstance(effectId));
            }

            return instance;
        }

        #endregion
    }
}
