using Godot;
using System.Collections.Generic;
using Jogo25D.Properties;
using Jogo25D.Effects;

namespace Jogo25D.Items
{
    public static class ItemDB
    {
        private static Dictionary<string, ItemDefinition> _items;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _items = new Dictionary<string, ItemDefinition>();

            var swordStarting = new WeaponDefinition
            {
                Id          = "sword_starting",
                Name        = "Sword",
                Type        = ItemType.WeaponMelee,
                Description = "Uma espada básica para combate corpo a corpo",
                Cooldown    = 0.5f,
                Icon        = ResourceLoader.Exists("res://Assets/Icons/sword.png") ? GD.Load<Texture2D>("res://Assets/Icons/sword.png") : null,
                HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Sword.tscn"),
                Properties  = new List<BaseProperty>
                {   
                    new DamageProperty { DamageAmount = 15, DamageType = DamageType.Physical },
                    new WeaponProperty { AttackRange = 80f, KnockbackForce = 200f },
                    new ChargesProperty { InfiniteCharges = true }
                }
            };

            var bowStarting = new WeaponDefinition
            {
                Id          = "bow_starting",
                Name        = "Arco",
                Type        = ItemType.WeaponRanged,
                Description = "Um arco para ataques à distância",
                Cooldown    = 0.8f,
                Icon        = ResourceLoader.Exists("res://Assets/Icons/bow.png") ? GD.Load<Texture2D>("res://Assets/Icons/bow.png") : null,
                HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Projectile.tscn"),
                Properties  = new List<BaseProperty>
                {
                    new DamageProperty { DamageAmount = 10, DamageType = DamageType.Physical },
                    new WeaponProperty { AttackRange = 1500f, AttackArea = 50f, ProjectileSpeed = 750f },
                    new ChargesProperty { InfiniteCharges = false, MaxCharges = 10, ChargeType = "arrow", ReloadCooldown = 1.0f }
                }
            };

            var fireballStarting = new WeaponDefinition
            {
                Id          = "bow_starting2",
                Name        = "Arco2",
                Type        = ItemType.WeaponRanged,
                Description = "Um arco melhorado para ataques à distância",
                Cooldown    = 0.01f,
                Icon        = ResourceLoader.Exists("res://Assets/Icons/fireball.png") ? GD.Load<Texture2D>("res://Assets/Icons/fireball.png") : null,
                HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Fireball.tscn"),
                Properties  = new List<BaseProperty>
                {
                    new DamageProperty { DamageAmount = 5, DamageType = DamageType.Fire },
                    new WeaponProperty { AttackRange = 2000f, AttackArea = 15f, ProjectileSpeed = 1200f },
                    new ChargesProperty { InfiniteCharges = true, MaxCharges = 1, ReloadCooldown = 1.5f }
                }
            };

            var arrow = new ConsumableDefinition
            {
                Id           = "arrow",
                Name         = "Flecha",
                Type         = ItemType.Consumable,
                Description  = "Munição para arcos",
                Stackable    = true,
                MaxStackSize = 9999,
                Icon         = ResourceLoader.Exists("res://Assets/Icons/arrow.png") ? GD.Load<Texture2D>("res://Assets/Icons/arrow.png") : null,
                Properties   = new List<BaseProperty>
                {
                    new ChargesProperty { ChargeType = "arrow" }
                }
            };

            var swordBasic = new WeaponDefinition
            {
                Id          = "sword_basic",
                Name        = "Espada Básica",
                Type        = ItemType.WeaponMelee,
                Cooldown    = 0.6f,
                Icon        = ResourceLoader.Exists("res://Assets/Icons/sword.png") ? GD.Load<Texture2D>("res://Assets/Icons/sword.png") : null,
                HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Sword.tscn"),
                Properties  = new List<BaseProperty>
                {
                    new DamageProperty { DamageAmount = 15, DamageType = DamageType.Physical },
                    new WeaponProperty { AttackRange = 80f, AttackArea = 30f, KnockbackForce = 250f },
                    new CritProperty { CritChance = 0.1f, CritDamage = 0.5f },
                    new ChargesProperty { InfiniteCharges = true }
                }
            };

            var bowBasic = new WeaponDefinition
            {
                Id          = "bow_basic",
                Name        = "Arco Básico",
                Type        = ItemType.WeaponRanged,
                Cooldown    = 0.8f,
                Icon        = ResourceLoader.Exists("res://Assets/Icons/bow.png") ? GD.Load<Texture2D>("res://Assets/Icons/bow.png") : null,
                HitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Projectile.tscn"),
                Properties  = new List<BaseProperty>
                {
                    new DamageProperty { DamageAmount = 10, DamageType = DamageType.Physical },
                    new WeaponProperty { AttackRange = 1500f, AttackArea = 50f, ProjectileSpeed = 750f },
                    new ChargesProperty { InfiniteCharges = false, MaxCharges = 10, ChargeType = "arrow", ReloadCooldown = 1.0f }
                }
            };

            var poisonFlask = new ConsumableDefinition
            {
                Id           = "poison_flask",
                Name         = "Frasco de Veneno",
                Type         = ItemType.Consumable,
                Stackable    = true,
                MaxStackSize = 10,
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

            _initialized = true;
        }

        public static void Register(ItemDefinition definition)
        {
            if (_items == null)
            {
                _items = new Dictionary<string, ItemDefinition>();
            }
            _items[definition.Id] = definition;
        }

        public static ItemDefinition Get(string id)
        {
            if (!_initialized)
            {
                Initialize();
            }

            _items.TryGetValue(id, out var def);
            return def;
        }

        public static bool TryGet(string id, out ItemDefinition definition)
        {
            if (!_initialized)
            {
                Initialize();
            }

            return _items.TryGetValue(id, out definition);
        }

        public static IEnumerable<string> GetAllIds()
        {
            if (!_initialized)
            {
                Initialize();
            }

            return _items.Keys;
        }
    }
}
