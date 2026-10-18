using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Effects;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Features.World.Properties.Resources;
using Jogo25D.Instances;
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
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Tools/PickaxeStarting.tscn"),
                },

                ["copper_pickaxe"] = () => new ToolDefinition
                {
                    Id = "copper_pickaxe",
                    Name = "Picareta de Cobre",
                    Type = ItemType.Tool,
                    Description = "Quebra blocos mais rápido e minera ferro",
                    Cooldown = 0.35f,
                    BreakTimeSeconds = 0.9f,
                    Tier = 2,
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Tools/PickaxeStarting.tscn"),
                },

                ["iron_pickaxe"] = () => new ToolDefinition
                {
                    Id = "iron_pickaxe",
                    Name = "Picareta de Ferro",
                    Type = ItemType.Tool,
                    Description = "Quebra blocos mais rápido e minera ouro",
                    Cooldown = 0.35f,
                    BreakTimeSeconds = 0.7f,
                    Tier = 3,
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Tools/PickaxeStarting.tscn"),
                },

                ["pickaxe_onehit"] = () => new ToolDefinition
                {
                    Id = "pickaxe_onehit",
                    Name = "Picareta Onehit",
                    Type = ItemType.Tool,
                    Description = "Destroi uma area redonda de blocos em um unico golpe",
                    Cooldown = 0.35f,
                    BreakTimeSeconds = 0.01f,
                    BreakRadius = 0,
                    Tier = 3,
                    HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Tools/PickaxeStarting.tscn"),
                },

                ["wall_wood"] = () => new BlockItemDefinition
                {
                    Id = "wall_wood", Name = "Parede de madeira", Type = ItemType.Block,
                    Description = "Parede de fundo sem colisao", BlockId = "wall_wood", Background = true,
                    Stackable = true, MaxStackSize = 999, Cooldown = 0.25f,
                },
                ["wall_dirt"] = () => new BlockItemDefinition
                {
                    Id = "wall_dirt", Name = "Parede de terra", Type = ItemType.Block,
                    Description = "Parede de fundo sem colisao", BlockId = "wall_dirt", Background = true,
                    Stackable = true, MaxStackSize = 999, Cooldown = 0.25f,
                },
                ["wall_hammer"] = () => new BlockItemDefinition
                {
                    Id = "wall_hammer", Name = "Martelo de paredes", Type = ItemType.Tool,
                    Description = "Remove somente paredes de fundo", Background = true, RemoveBackground = true,
                    Stackable = false, MaxStackSize = 1, Cooldown = 0.25f,
                },

                ["block_dirt"] = () => new BlockItemDefinition
                {
                    Id = "block_dirt",
                    Name = "Bloco de Terra",
                    Type = ItemType.Block,
                    Description = "Um bloco de terra, pode ser colocado no mundo",
                    BlockId = "dirt",
                    Stackable = true,
                    MaxStackSize = 999,
                    Cooldown = 0.25f,
                },

                ["item_wood"] = () => new BlockItemDefinition
                {
                    Id = "item_wood",
                    Name = "Madeira",
                    Type = ItemType.Material,
                    Description = "Madeira retirada do tronco de uma árvore, pode ser colocada no mundo",
                    BlockId = "wood",
                    Stackable = true,
                    MaxStackSize = 999,
                    Cooldown = 0.25f,
                },

                ["item_leaf"] = () => new BlockItemDefinition
                {
                    Id = "item_leaf",
                    Name = "Folha",
                    Type = ItemType.Material,
                    Description = "Folhas retiradas da copa de uma árvore, pode ser colocada no mundo",
                    BlockId = "leaf",
                    Stackable = true,
                    MaxStackSize = 999,
                    Cooldown = 0.25f,
                },

                ["torch"] = () => new BlockItemDefinition
                {
                    Id = "torch",
                    Name = "Tocha",
                    Type = ItemType.Block,
                    Description = "Ilumina em volta. Precisa de uma parede atrás ou de um bloco embaixo ou ao lado",
                    BlockId = "torch",
                    Stackable = true,
                    MaxStackSize = 999,
                    Cooldown = 0.25f,
                    HeldLightColor = new Color(1f, 0.72f, 0.4f),
                },

                ["copper_ore"] = () => new MaterialDefinition
                {
                    Id = "copper_ore",
                    Name = "Minério de Cobre",
                    Type = ItemType.Material,
                    Description = "Funda na fornalha para fazer barras de cobre",
                    Stackable = true,
                    MaxStackSize = 999,
                },

                ["iron_ore"] = () => new MaterialDefinition
                {
                    Id = "iron_ore",
                    Name = "Minério de Ferro",
                    Type = ItemType.Material,
                    Description = "Funda na fornalha para fazer barras de ferro",
                    Stackable = true,
                    MaxStackSize = 999,
                },

                ["gold_ore"] = () => new MaterialDefinition
                {
                    Id = "gold_ore",
                    Name = "Minério de Ouro",
                    Type = ItemType.Material,
                    Description = "Funda na fornalha para fazer barras de ouro",
                    Stackable = true,
                    MaxStackSize = 999,
                },

                ["copper_bar"] = () => new MaterialDefinition
                {
                    Id = "copper_bar",
                    Name = "Barra de Cobre",
                    Type = ItemType.Material,
                    Description = "Usada para fazer ferramentas de cobre",
                    Stackable = true,
                    MaxStackSize = 999,
                },

                ["iron_bar"] = () => new MaterialDefinition
                {
                    Id = "iron_bar",
                    Name = "Barra de Ferro",
                    Type = ItemType.Material,
                    Description = "Usada para fazer ferramentas de ferro",
                    Stackable = true,
                    MaxStackSize = 999,
                },

                ["gold_bar"] = () => new MaterialDefinition
                {
                    Id = "gold_bar",
                    Name = "Barra de Ouro",
                    Type = ItemType.Material,
                    Description = "Metal raro e valioso",
                    Stackable = true,
                    MaxStackSize = 999,
                },

                ["copper_helmet"] = () => new EquipmentDefinition
                {
                    Id = "copper_helmet",
                    Name = "Capacete de Cobre",
                    Type = ItemType.Equipment,
                    Kind = EquipmentKind.Head,
                    Description = "Reduz o dano físico recebido em 3%",
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamageResistencePropertyData { DamageType = DamageType.Physical, ResistanceFactor = 0.03f },
                    },
                },

                ["copper_chestplate"] = () => new EquipmentDefinition
                {
                    Id = "copper_chestplate",
                    Name = "Peitoral de Cobre",
                    Type = ItemType.Equipment,
                    Kind = EquipmentKind.Chest,
                    Description = "Reduz o dano físico recebido em 5%",
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamageResistencePropertyData { DamageType = DamageType.Physical, ResistanceFactor = 0.05f },
                    },
                },

                ["copper_leggings"] = () => new EquipmentDefinition
                {
                    Id = "copper_leggings",
                    Name = "Calças de Cobre",
                    Type = ItemType.Equipment,
                    Kind = EquipmentKind.Legs,
                    Description = "Reduz o dano físico recebido em 4%",
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamageResistencePropertyData { DamageType = DamageType.Physical, ResistanceFactor = 0.04f },
                    },
                },

                ["iron_helmet"] = () => new EquipmentDefinition
                {
                    Id = "iron_helmet",
                    Name = "Capacete de Ferro",
                    Type = ItemType.Equipment,
                    Kind = EquipmentKind.Head,
                    Description = "Reduz o dano físico recebido em 5%",
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamageResistencePropertyData { DamageType = DamageType.Physical, ResistanceFactor = 0.05f },
                    },
                },

                ["iron_chestplate"] = () => new EquipmentDefinition
                {
                    Id = "iron_chestplate",
                    Name = "Peitoral de Ferro",
                    Type = ItemType.Equipment,
                    Kind = EquipmentKind.Chest,
                    Description = "Reduz o dano físico recebido em 8%",
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamageResistencePropertyData { DamageType = DamageType.Physical, ResistanceFactor = 0.08f },
                    },
                },

                ["iron_leggings"] = () => new EquipmentDefinition
                {
                    Id = "iron_leggings",
                    Name = "Calças de Ferro",
                    Type = ItemType.Equipment,
                    Kind = EquipmentKind.Legs,
                    Description = "Reduz o dano físico recebido em 6%",
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamageResistencePropertyData { DamageType = DamageType.Physical, ResistanceFactor = 0.06f },
                    },
                },

                ["gold_helmet"] = () => new EquipmentDefinition
                {
                    Id = "gold_helmet",
                    Name = "Capacete de Ouro",
                    Type = ItemType.Equipment,
                    Kind = EquipmentKind.Head,
                    Description = "Reduz o dano físico recebido em 7%",
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamageResistencePropertyData { DamageType = DamageType.Physical, ResistanceFactor = 0.07f },
                    },
                },

                ["gold_chestplate"] = () => new EquipmentDefinition
                {
                    Id = "gold_chestplate",
                    Name = "Peitoral de Ouro",
                    Type = ItemType.Equipment,
                    Kind = EquipmentKind.Chest,
                    Description = "Reduz o dano físico recebido em 11%",
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamageResistencePropertyData { DamageType = DamageType.Physical, ResistanceFactor = 0.11f },
                    },
                },

                ["gold_leggings"] = () => new EquipmentDefinition
                {
                    Id = "gold_leggings",
                    Name = "Calças de Ouro",
                    Type = ItemType.Equipment,
                    Kind = EquipmentKind.Legs,
                    Description = "Reduz o dano físico recebido em 9%",
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new DamageResistencePropertyData { DamageType = DamageType.Physical, ResistanceFactor = 0.09f },
                    },
                },

                ["swift_boots"] = () => new EquipmentDefinition
                {
                    Id = "swift_boots",
                    Name = "Botas Ligeiras",
                    Type = ItemType.Equipment,
                    Kind = EquipmentKind.Accessory,
                    Description = "Aumenta a velocidade de movimento",
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new MovementPropertyData { Speed = 45f },
                    },
                },

                ["life_amulet"] = () => new EquipmentDefinition
                {
                    Id = "life_amulet",
                    Name = "Amuleto de Vida",
                    Type = ItemType.Equipment,
                    Kind = EquipmentKind.Accessory,
                    Description = "Aumenta a vida máxima em 20",
                    Properties = new Godot.Collections.Array<BasePropertyData>
                    {
                        new HealthPropertyData { MaxHealth = 20 },
                    },
                },

                ["portal"] = () => new PlacePropItemDefinition
                {
                    Id = "portal",
                    PropId = "portal",
                    Name = "Portal",
                    Type = ItemType.Misc,
                    Description = "Cria um portal que troca de dimensão ao interagir",
                    Stackable = true,
                    MaxStackSize = 99,
                    Cooldown = 0.25f,
                },

                ["crafting_table"] = () => new PlacePropItemDefinition
                {
                    Id = "crafting_table",
                    PropId = "crafting_table",
                    Name = "Mesa de criação",
                    Type = ItemType.Misc,
                    Description = "Libera as receitas de mesa para quem está perto dela",
                    Stackable = true,
                    MaxStackSize = 99,
                    Cooldown = 0.25f,
                },

                ["furnace"] = () => new PlacePropItemDefinition
                {
                    Id = "furnace",
                    PropId = "furnace",
                    Name = "Fornalha",
                    Type = ItemType.Misc,
                    Description = "Libera as receitas de barras de metal para quem está perto dela",
                    Stackable = true,
                    MaxStackSize = 99,
                    Cooldown = 0.25f,
                },

                ["anvil"] = () => new PlacePropItemDefinition
                {
                    Id = "anvil",
                    PropId = "anvil",
                    Name = "Bigorna",
                    Type = ItemType.Misc,
                    Description = "Libera as receitas de armaduras e acessórios para quem está perto dela",
                    Stackable = true,
                    MaxStackSize = 99,
                    Cooldown = 0.25f,
                },

                ["arrow"] = () => new ConsumableDefinition
                {
                    Id = "arrow",
                    Name = "Flecha",
                    Type = ItemType.Consumable,
                    Description = "MuniÃ§Ã£o para arcos",
                    Stackable = true,
                    MaxStackSize = 9999,
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
                    Effects = new Godot.Collections.Array<string> { "poison_damage" }
                },

                ["fire_damage_potion"] = () => new ConsumableDefinition
                {
                    Id = "fire_damage_potion",
                    Name = "Poção de Dano de Fogo",
                    Type = ItemType.Consumable,
                    Stackable = true,
                    MaxStackSize = 10,
                    Effects = new Godot.Collections.Array<string> { "fire_damage" }
                },

                ["health_regen_potion"] = () => new ConsumableDefinition
                {
                    Id = "health_regen_potion",
                    Name = "Poção de Regeneração",
                    Type = ItemType.Consumable,
                    Stackable = true,
                    MaxStackSize = 10,
                    Effects = new Godot.Collections.Array<string> { "health_regen" }
                },

                ["speed_potion"] = () => new ConsumableDefinition
                {
                    Id = "speed_potion",
                    Name = "Poção de Velocidade",
                    Type = ItemType.Consumable,
                    Stackable = true,
                    MaxStackSize = 10,
                    Effects = new Godot.Collections.Array<string> { "speed_boost" }
                },

                ["instant_heal_potion"] = () => new ConsumableDefinition
                {
                    Id = "instant_heal_potion",
                    Name = "Poção de Cura Instantânea",
                    Type = ItemType.Consumable,
                    Stackable = true,
                    MaxStackSize = 10,
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

        public static ItemData CreateInstance(string id)
        {
            var def = Create(id) ?? throw new Exception($"[ItemFactory] Item '{id}' nao encontrado.");
            var instance = new ItemData(id)
            {
                InstanceId = InstanceIdGenerator.NextInstanceId(),
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
