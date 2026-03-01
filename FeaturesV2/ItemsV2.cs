using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Core
{
    #region Types

    public enum ItemType
    {
        Weapon,
        Consumable,
        Material,
        Misc
    }

    #endregion

    #region Damage system

    public enum DamageType
    {
        Physical,
        Fire,
        Ice,
        Poison,
        Electric,
        True
    }

    public struct DamageInfo
    {
        public int Amount;
        public DamageType Type;
        public int SourcePeerId;
    }

    #endregion

    #region Abstract Definitions

    public abstract class EffectDefinition
    {
        public float Duration { get; set; }
        public float Elapsed { get; private set; }
        public bool Expired { get; set; }
        public bool Infinite { get; set; }

        public void Tick(Player player, float delta)
        {
            if (!Infinite)
            {
                if (Expired)
                {
                    return;
                }

                if (Duration > 0)
                {
                    Elapsed += delta;

                    if (Elapsed >= Duration)
                    {
                        Expired = true;

                        OnFinished(player, delta);
                    }
                }
            }

            Apply(player, delta);
        }

        public EffectDefinition Clone()
        {
            return (EffectDefinition)this.MemberwiseClone();
        }

        public virtual void Apply(Player player, float delta)
        {
            return;
        }

        public virtual void OnFinished(Player player, float delta)
        {
            return;
        }
    }

    public class ItemDefinition
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public ItemType Type { get; init; }
        public float Cooldown { get; init; }
        public bool Stackable { get; init; }
        public List<BaseProperty> Properties { get; set; } = new();
        public List<EffectDefinition> OnHitEffects { get; set; } = new();
        public PackedScene HitboxScene { get; set; }

        public virtual void Use(Player player, ItemInstance instance) { }

        public virtual void Attack(Player player, ItemInstance instance)
        {
            if (!instance.CanUse())
            {
                return;
            }

            var damageProp = instance.Properties.OfType<DamageProperty>().FirstOrDefault();

            if (damageProp == null)
            {
                return;
            }

            if (HitboxScene == null)
            {
                return;
            }

            var critProp = instance.Properties.OfType<CritProperty>().FirstOrDefault();

            float multiplier = 1f;

            if (critProp != null && GD.Randf() <= critProp.CritChance)
            {
                multiplier += critProp.CritDamage;
            }

            int finalDamage = (int)(damageProp.DamageAmount * multiplier);

            if (HitboxScene.Instantiate<Area2D>() is not BaseHitbox hitbox)
            {
                return;
            }

            hitbox.Initialize(
                new DamageInfo
                {
                    Amount = finalDamage,
                    Type = damageProp.DamageType,
                    SourcePeerId = player.PeerId
                },
                instance.OnHitEffects,
                player
            );

            hitbox.GlobalPosition = player.GlobalPosition;

            player.GetTree().CurrentScene.AddChild(hitbox);

            instance.TriggerCooldown();
        }

        public virtual void OnEquip(Player player, ItemInstance instance) { }

        public virtual void OnUnequip(Player player, ItemInstance instance) { }
    }

    #endregion

    #region Instances

    public class ItemInstance
    {
        public ItemDefinition Definition { get; private set; }
        public float CooldownRemaining { get; private set; }
        public int Quantity { get; private set; }
        public List<BaseProperty> Properties { get; set; }
        public List<EffectDefinition> OnHitEffects { get; set; }

        public ItemInstance(ItemDefinition definition, int quantity = 1)
        {
            Definition = definition;
            Quantity = quantity;

            Properties = definition.Properties.ToList();
            OnHitEffects = definition.OnHitEffects.ToList();
        }

        public void Update(float delta)
        {
            if (CooldownRemaining > 0)
            {
                CooldownRemaining -= delta;
            }
        }

        public bool CanUse()
        {
            return CooldownRemaining <= 0;
        }

        public void TriggerCooldown()
        {
            CooldownRemaining = Definition.Cooldown;
        }

        public void AddQuantity(int amount)
        {
            Quantity += amount;
        }

        public void RemoveQuantity(int amount)
        {
            Quantity -= amount;
        }
    }

    #endregion

    #region Combat

    public class BaseProperty { }

    public class DamageProperty : BaseProperty
    {
        public DamageType DamageType { get; set; }
        public int DamageAmount { get; set; }
    }

    public class DamageMultiplierProperty : BaseProperty
    {
        public DamageType DamageType { get; set; }
        public int DamageAmount { get; set; }
    }

    public class DamageResistenceProperty : BaseProperty
    {
        public DamageType DamageType { get; set; }
        public float DamageAmount { get; set; }
    }

    public class DamageResistenceMultiplierProperty : BaseProperty
    {
        public DamageType DamageType { get; set; }
        public int DamageAmount { get; set; }
    }

    public class CritProperty : BaseProperty
    {
        public float CritChance { get; set; }
        public float CritDamage { get; set; }
    }

    public partial class BaseHitbox : Area2D
    {
        protected DamageInfo Damage;
        protected List<EffectDefinition> Effects;
        protected new Player Owner;

        public virtual void Initialize(DamageInfo damage, List<EffectDefinition> effects, Player owner)
        {
            Damage = damage;
            Effects = new List<EffectDefinition>(effects ?? new());
            Owner = owner;

            BodyEntered += OnBodyEntered;
        }

        protected virtual void OnBodyEntered(Node body)
        {
            if (body == Owner)
            {
                return;
            }

            if (body is Player player)
            {
                player.ReceiveDamage(Damage);

                foreach (var effect in Effects)
                {
                    player.AddEffect(effect);
                }

                QueueFree();
            }
        }
    }

    public partial class MeleeHitbox : BaseHitbox
    {
        public float Lifetime = 0.2f;

        private float _timer;

        public override void _PhysicsProcess(double delta)
        {
            if (Owner == null)
            {
                return;
            }

            GlobalPosition = Owner.GlobalPosition;

            _timer += (float)delta;

            if (_timer >= Lifetime)
            {
                QueueFree();
            }
        }
    }

    public partial class ProjectileHitbox : BaseHitbox
    {
        [Export] public float Speed = 600f;
        [Export] public Vector2 Direction;
        [Export] public float Lifetime = 2f;

        private float _timer;

        public override void _PhysicsProcess(double delta)
        {
            Position += Direction * Speed * (float)delta;

            _timer += (float)delta;

            if (_timer >= Lifetime)
            {
                QueueFree();
            }
        }
    }

    #endregion

    #region Inventory

    public partial class Player : CharacterBody2D
    {
        public int MaxHealth { get; private set; } = 100;
        public int Health { get; private set; } = 100;
        public int PeerId { get; private set; } = 1;
        public List<BaseProperty> Buffs { get; private set; } = new();
        public List<EffectDefinition> Effects { get; private set; } = new();
        public Inventory Inventory { get; private set; } = new();

        public override void _Process(double delta)
        {
            Inventory.Update((float)delta);

            for (int i = Effects.Count - 1; i >= 0; i--)
            {
                var effect = Effects[i];

                effect.Tick(this, (float)delta);

                if (effect.Expired)
                {
                    Effects.RemoveAt(i);
                }
            }
        }

        public void AddEffect(EffectDefinition definition)
        {
            Effects.Add(definition.Clone());
        }

        public void ReceiveDamage(DamageInfo damage)
        {
            var resistance = Buffs.OfType<DamageResistenceProperty>().FirstOrDefault();
            float resistanceFactor = resistance?.DamageAmount ?? 0f;
            int finalDamage = (int)(damage.Amount * (1f - resistanceFactor));

            Health -= finalDamage;

            GD.Print($"{Name} recebeu {finalDamage} de dano ({damage.Type})");

            if (Health <= 0)
            {
                GD.Print($"{Name} morreu.");
            }
        }
    }

    public class Inventory
    {
        public ItemInstance EquippedWeapon { get; private set; }
        public IEnumerable<ItemInstance> Items => _items;

        private readonly List<ItemInstance> _items = new();

        public void AddItem(ItemDefinition definition, int quantity = 1)
        {
            if (definition.Stackable)
            {
                var existing = _items.FirstOrDefault(i => i.Definition.Id == definition.Id);

                if (existing != null)
                {
                    existing.AddQuantity(quantity);
                    return;
                }
            }

            _items.Add(new ItemInstance(definition, quantity));
        }

        public void Equip(ItemInstance instance)
        {
            if (instance.Definition.Type == ItemType.Weapon)
            {
                EquippedWeapon = instance;
            }
        }

        public void UseEquipped(Player player)
        {
            EquippedWeapon?.Definition.Attack(player, EquippedWeapon);
        }

        public void Update(float delta)
        {
            foreach (var item in _items)
            {
                item.Update(delta);
            }
        }
    }

    #endregion

    #region Effects definitions

    public class PoisonEffect : EffectDefinition
    {
        public int DamagePerSecond = 5;

        private float _tickAccumulator;

        public override void Apply(Player player, float delta)
        {
            _tickAccumulator += delta;

            if (_tickAccumulator >= 1f)
            {
                player.ReceiveDamage(new DamageInfo
                {
                    Amount = DamagePerSecond,
                    Type = DamageType.Poison,
                    SourcePeerId = 0
                });

                _tickAccumulator = 0f;
            }
        }

        public override void OnFinished(Player player, float delta)
        {
            return;
        }
    }

    #endregion

    #region Static declarations

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

            var sword = new ItemDefinition
            {
                Id = "sword_basic",
                Name = "Espada Básica",
                Type = ItemType.Weapon,
                Stackable = false,
                Cooldown = 0.6f,
                HitboxScene = GD.Load<PackedScene>("res://MeleeHitbox.tscn"),
                Properties = new List<BaseProperty>
                {
                    new DamageProperty
                    {
                        DamageAmount = 15,
                        DamageType = DamageType.Physical
                    },
                    new CritProperty
                    {
                        CritChance = 0.1f,
                        CritDamage = 0.5f
                    }
                }
            };

            _items[sword.Id] = sword;
            _initialized = true;
        }

        public static ItemDefinition Get(string id)
        {
            if (!_initialized)
            {
                Initialize();
            }

            return _items[id];
        }
    }

    #endregion
}