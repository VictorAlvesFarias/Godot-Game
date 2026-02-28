using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

//TODO: Adicionar efeitos/buffs on hit.
//TODO: Adicionar efeitos/buffs on hit ganhos em runtime.

//TODO: Adicionar efeitos/buffs player enquanto equipado.
//TODO: Adicionar efeitos/buffs player enquanto equipado ganhos em runtime.

//TODO: Adicionar efeitos/buffs para o item.
//TODO: Adicionar efeitos/buffs para o item ganhos em runtime.

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
        public Player Source;
        public float CritMultiplier;
    }

    #endregion

    #region Abstract Definitions

    public abstract class EffectDefinition
    {
        public float Duration { get; init; }
        public virtual void Apply(Player player) { }
        public virtual void Remove(Player player) { }
        public virtual void Tick(Player player, float delta) { }
    }
    
    public abstract class ItemDefinition
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public ItemType Type { get; init; }
        public bool Stackable { get; init; }
        public int MaxStack { get; init; }
        public float Cooldown { get; init; } 
        public List<BaseProperty> Properties { get; init; } 
        public List<EffectDefinition> SelfEffects { get; init; } 
        public List<EffectDefinition> OnHitEffects { get; init; } 

        public ItemDefinition()
        {
            Cooldown = 0.5f;
            Properties = new();
            SelfEffects = new();
            OnHitEffects = new();
        }

        public abstract void Use(Player player, ItemInstance instance);
    }

    public abstract class BaseProperty
    {
    }

    #endregion

    #region Properties 

    public class WeaponDamageProperty : BaseProperty
    {
        public int BaseDamage;
        public DamageType DamageType;
        public float CritChance = 0.1f;
        public float CritMultiplier = 1.5f;
    }

    #endregion

    #region Weapons definitions

    public class DefaultWeaponDefinition : ItemDefinition
    {
        public PackedScene HitboxScene;

        public override void Use(Player player, ItemInstance instance)
        {
            if (!instance.CanUse()) return;

            foreach (var effect in SelfEffects)
                player.AddEffect(effect);

            var hitbox = HitboxScene.Instantiate<MeleeHitbox>();


            var damageProp = Properties.OfType<WeaponDamageProperty>().FirstOrDefault();

            if (damageProp == null)
                hitbox.Damage = default;

            bool crit = GD.Randf() <= damageProp.CritChance;
            float multiplier = crit ? damageProp.CritMultiplier : 1f;

            hitbox.Damage = new DamageInfo
            {
                Amount = (int)((damageProp.BaseDamage + player.Attack) * multiplier),
                Type = damageProp.DamageType,
                Source = player,
                CritMultiplier = multiplier
            };

            hitbox.Owner = player;
            hitbox.OnHitEffects = new List<EffectDefinition>(OnHitEffects);

            hitbox.GlobalPosition = player.GlobalPosition;

            player.GetTree().CurrentScene.AddChild(hitbox);

            instance.TriggerCooldown();
        }
    }

    #endregion

    #region Hitbox

    public partial class MeleeHitbox : Area2D
    {
        public Player Owner;
        public DamageInfo Damage;
        public List<EffectDefinition> OnHitEffects = new();
        public float Lifetime = 0.2f;

        private float _timer;
        private bool _destroyed;

        public override void _Ready()
        {
            BodyEntered += OnBodyEntered;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (Owner == null || _destroyed)
                return;

            GlobalPosition = Owner.GlobalPosition;

            _timer += (float)delta;
            if (_timer >= Lifetime)
                Destroy();
        }

        private void OnBodyEntered(Node body)
        {
            if (_destroyed) return;
            if (body == Owner) return;

            if (body is Player player)
            {
                player.ReceiveDamage(Damage);

                foreach (var effect in OnHitEffects)
                    player.AddEffect(effect);
            }
        }

        private void Destroy()
        {
            _destroyed = true;
            QueueFree();
        }
    }

    public partial class ProjectileHitbox : Area2D
    {
        [Export] public float Speed = 600f;
        [Export] public Vector2 Direction;
        [Export] public float Lifetime = 2f;

        public DamageInfo Damage;
        public List<EffectDefinition> OnHitEffects = new();

        private float _timer;
        private bool _destroyed;

        public override void _Ready()
        {
            BodyEntered += OnBodyEntered;
        }

        public override void _PhysicsProcess(double delta)
        {
            Position += Direction * Speed * (float)delta;

            _timer += (float)delta;
            if (_timer >= Lifetime && !_destroyed)
                Destroy();
        }

        private void OnBodyEntered(Node body)
        {
            if (_destroyed) return;
            if (body == Damage.Source) return;

            if (body is Player player)
            {
                player.ReceiveDamage(Damage);

                foreach (var effect in OnHitEffects)
                    player.AddEffect(effect);

                Destroy();
            }
        }

        private void Destroy()
        {
            _destroyed = true;
            QueueFree();
        }
    }

    #endregion

    #region Items definitions

    public class SimpleItemDefinition : ItemDefinition
    {
        public Action<Player, ItemInstance> OnUse;

        public override void Use(Player player, ItemInstance instance)
        {
            if (!instance.CanUse())
                return;

            OnUse?.Invoke(player, instance);

            instance.TriggerCooldown();
        }
    }

    #endregion

    #region Instances 

    public class EffectInstance
    {
        public EffectDefinition Definition { get; }
        private float _remaining;
        public bool Expired { get; private set; }

        public EffectInstance(EffectDefinition definition)
        {
            Definition = definition;
            _remaining = definition.Duration;
        }

        public void Apply(Player player)
        {
            Definition.Apply(player);
        }

        public void Update(Player player, float delta)
        {
            if (Expired) return;

            Definition.Tick(player, delta);

            _remaining -= delta;
            if (_remaining <= 0)
            {
                Definition.Remove(player);
                Expired = true;
            }
        }
    }

    public class ItemInstance
    {
        public ItemDefinition Definition { get; private set; }
        public int Quantity { get; private set; }
        public float CooldownRemaining { get; private set; }

        public ItemInstance(ItemDefinition definition, int quantity = 1)
        {
            Definition = definition;
            Quantity = quantity;
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
        public void AddQuantity(int amount) => Quantity += amount;
        public void RemoveQuantity(int amount) => Quantity -= amount;
    }

    #endregion

    #region Inventory

    public partial class Player : CharacterBody2D
    {
        public int MaxHealth = 100;
        public int Health = 100;
        public int Attack = 5;
        public int Defense = 2;

        private readonly Dictionary<DamageType, float> _resistances = new();
        private readonly List<EffectInstance> _effects = new();

        public Inventory Inventory { get; private set; } = new();

        public override void _Ready()
        {
            AddToGroup("damageable");

            foreach (DamageType type in Enum.GetValues(typeof(DamageType)))
                _resistances[type] = 0f;
        }

        public override void _Process(double delta)
        {
            Inventory.Update((float)delta);

            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                _effects[i].Update(this, (float)delta);
                if (_effects[i].Expired)
                    _effects.RemoveAt(i);
            }
        }

        public void ReceiveDamage(DamageInfo damage)
        {
            float resistance = _resistances.GetValueOrDefault(damage.Type, 0f);
            int finalDamage = (int)(damage.Amount * (1f - resistance));

            if (damage.Type == DamageType.Physical)
                finalDamage = Math.Max(0, finalDamage - Defense);

            Health -= finalDamage;

            GD.Print($"{Name} recebeu {finalDamage} de dano ({damage.Type})");

            if (Health <= 0)
                Die();
        }

        public void AddEffect(EffectDefinition definition)
        {
            var instance = new EffectInstance(definition);
            _effects.Add(instance);
            instance.Apply(this);
        }

        public Vector2 GetAimDirection()
        {
            return Vector2.Right;
        }

        private void Die()
        {
            GD.Print($"{Name} morreu.");
        }
    }

    public class Inventory
    {
        private readonly List<ItemInstance> _items = new();
        public ItemInstance EquippedWeapon { get; private set; }

        public IEnumerable<ItemInstance> Items => _items;

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
                EquippedWeapon = instance;
        }

        public void UseEquipped(Player player)
        {
            EquippedWeapon?.Definition.Use(player, EquippedWeapon);
        }

        public void Update(float delta)
        {
            foreach (var item in _items)
                item.Update(delta);
        }
    }

    #endregion

    #region Effects definitions

    public class PoisonEffect : EffectDefinition
    {
        public int DamagePerSecond = 5;
        private float _tickAccumulator;

        public override void Tick(Player player, float delta)
        {
            _tickAccumulator += delta;

            if (_tickAccumulator >= 1f)
            {
                player.ReceiveDamage(new DamageInfo
                {
                    Amount = DamagePerSecond,
                    Type = DamageType.Poison,
                    Source = null
                });

                _tickAccumulator = 0f;
            }
        }
    }

    public class AttackBuffEffect : EffectDefinition
    {
        public int Bonus;

        public override void Apply(Player player)
        {
            player.Attack += Bonus;
        }

        public override void Remove(Player player)
        {
            player.Attack -= Bonus;
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
            if (_initialized) return;

            _items = new Dictionary<string, ItemDefinition>();

            var ProjectileHitboxScene = GD.Load<PackedScene>("res://ProjectileHitbox.tscn");

            var sword = new DefaultWeaponDefinition
            {
                Id = "sword_basic",
                Name = "Espada Básica",
                Type = ItemType.Weapon,
                Stackable = false,
                Cooldown = 0.6f,
                HitboxScene = GD.Load<PackedScene>("res://MeleeHitbox.tscn"),
                Properties = new List<BaseProperty>
                {
                    new WeaponDamageProperty
                    {
                        BaseDamage = 15,
                        DamageType = DamageType.Physical,
                        CritChance = 0.2f,
                        CritMultiplier = 2f
                    }
                }
            };
            var poisonBow = new DefaultWeaponDefinition
            {
                Id = "bow_poison",
                Name = "Arco Envenenado",
                Type = ItemType.Weapon,
                Stackable = false,
                Cooldown = 0.5f,
                HitboxScene = ProjectileHitboxScene,
                Properties = new List<BaseProperty>
                {
                    new WeaponDamageProperty
                    {
                        BaseDamage = 10,
                        DamageType = DamageType.Physical,
                        CritChance = 0.15f,
                        CritMultiplier = 1.8f
                    }
                },
                            OnHitEffects = new List<EffectDefinition>
                {
                    new PoisonEffect
                    {
                        Duration = 5f,
                        DamagePerSecond = 4
                    }
                }
            };
            var potion = new SimpleItemDefinition
            {
                Id = "potion_strength",
                Name = "Poção de Força",
                Type = ItemType.Consumable,
                Stackable = true,
                MaxStack = 10,
                Cooldown = 1f,
                OnUse = (player, instance) =>
                {
                    player.AddEffect(new AttackBuffEffect
                    {
                        Duration = 5f,
                        Bonus = 10
                    });

                    instance.RemoveQuantity(1);
                }
            };

            Register(sword);
            Register(poisonBow);
            Register(potion);

            _initialized = true;
        }

        private static void Register(ItemDefinition item)
        {
            _items[item.Id] = item;
        }

        public static ItemDefinition Get(string id)
        {
            if (!_initialized)
                Initialize();

            return _items[id];
        }
    }

    #endregion
}

/// Definições
///
/// Efeitos:
/// - Efeitos podem apenas ser aplicados ao jogador, quando um item é usado ou quando um hitbox acerta um inimigo, 
/// o efeito é adicionado a lista de efeitos do jogador e ele processa os efeitos a cada frame, removendo quando expiram. Efeitos 
/// também podem ser aplicados em tempo de uso, como uma arma que aplica dano de veneno a si mesmo enquanto está equipada.
///
/// Buffs:
/// - Buffs também são efeitos, mas não são aplicados por tick, são aplicados uma vez e tem um efeito contínuo enquanto 
/// estão ativos, como um buff de ataque que aumenta o ataque do jogador enquanto está ativo.
/// 
/// Actions nos itens:
/// - Assim como as actions nos players, os itens também podem ter actions, 
/// que são funções que são chamadas quando o item é usado, como uma poção de força que aplica um buff de ataque ao jogador quando usada.
/// 
