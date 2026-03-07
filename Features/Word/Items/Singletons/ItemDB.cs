using Godot;
using System.Collections.Generic;
using Jogo25D.Properties;
using Jogo25D.Effects;
using Jogo25D.Constants;

namespace Jogo25D.Items
{
    public static class ItemDB
    {
        public static Dictionary<string, ItemDefinition> Items { get; set; }
        public static bool Initialized { get; set; }

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
                Properties = new List<BaseProperty>
                {   
                    new DamageProperty { DamageAmount = 15, DamageType = DamageType.Physical },
                    new AttackProperty { AttackRange = 80f, KnockbackForce = 200f },
                    new ChargesProperty { InfiniteCharges = true }
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
                Properties = new List<BaseProperty>
                {
                    new DamageProperty { DamageAmount = 10, DamageType = DamageType.Physical },
                    new AttackProperty { AttackRange = 1500f, AttackArea = 50f, ProjectileSpeed = 750f },
                    new ChargesProperty { InfiniteCharges = false, MaxCharges = 10, ChargeItemId = "arrow", ReloadCooldown = 1.0f }
                }
            };

            var fireballStarting = new WeaponDefinition {
                Id = "bow_starting2",
                Name = "Arco2",
                Type = ItemType.WeaponRanged,
                Description = "Um arco melhorado para ataques Ã  distÃ¢ncia",
                Cooldown = 0.01f,
                Icon = GD.Load<Texture2D>(Assets.Icons.Bows.ICON_BOW_11),
                HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Fireball.tscn"),
                Properties = new List<BaseProperty>
                {
                    new DamageProperty { DamageAmount = 5, DamageType = DamageType.Fire },
                    new AttackProperty { AttackRange = 2000f, AttackArea = 15f, ProjectileSpeed = 1200f },
                    new ChargesProperty { InfiniteCharges = true, MaxCharges = 1, ReloadCooldown = 1.5f }
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
                Properties = new List<BaseProperty>
                {
                    new ChargesProperty { ChargeItemId = "arrow" }
                }
            };

            var swordBasic = new WeaponDefinition {
                Id = "sword_basic",
                Name = "Espada BÃ¡sica",
                Type = ItemType.WeaponMelee,
                Cooldown = 0.6f,
                Icon = ResourceLoader.Exists(Assets.Icons.Swords.ICON_SWORD_1) ? GD.Load<Texture2D>(Assets.Icons.Swords.ICON_SWORD_1) : null,
                HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Sword.tscn"),
                Properties = new List<BaseProperty>
                {
                    new DamageProperty { DamageAmount = 15, DamageType = DamageType.Physical },
                    new AttackProperty { AttackRange = 80f, AttackArea = 30f, KnockbackForce = 250f },
                    new CritProperty { CritChance = 0.1f, CritDamage = 0.5f },
                    new ChargesProperty { InfiniteCharges = true }
                }
            };

            var bowBasic = new WeaponDefinition {
                Id = "bow_basic",
                Name = "Arco BÃ¡sico",
                Type = ItemType.WeaponRanged,
                Cooldown = 0.8f,
                Icon = GD.Load<Texture2D>(Assets.Icons.Bows.ICON_BOW_1),
                HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Projectile.tscn"),
                Properties = new List<BaseProperty>
                {
                    new DamageProperty { DamageAmount = 10, DamageType = DamageType.Physical },
                    new AttackProperty { AttackRange = 1500f, AttackArea = 50f, ProjectileSpeed = 750f },
                    new ChargesProperty { InfiniteCharges = false, MaxCharges = 10, ChargeItemId = "arrow", ReloadCooldown = 1.0f }
                }
            };

            var poisonFlask = new ConsumableDefinition {
                Id = "poison_flask",
                Name = "Frasco de Veneno",
                Type = ItemType.Consumable,
                Stackable = true,
                MaxStackSize = 10,
                Icon = GD.Load<Texture2D>(Assets.Icons.Potions.ICON_POTION_1),
                OnUseEffects = new List<EffectDefinition>
                {
                    new DamageEffectDefinition
                    {
                        Damages = new List<DamageInfo>() 
                        { 
                            new DamageInfo()
                            { 
                                Type = DamageType.Physical,
                                Amount = 5,
                                SourcePeerId = -1,
                                CritChance = 0.2f,
                                CritDamage = 0.5f
                            }
                        }
                    }
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

        public static ItemDefinition Get(string id)
        {
            if (!Initialized)
            {
                Initialize();
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
    }
}