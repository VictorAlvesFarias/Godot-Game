using Godot;
using System.Collections.Generic;
using Jogo25D.Properties;
using Jogo25D.Effects;
using Jogo25D.Characters;

namespace Jogo25D.Items
{
    public abstract class ItemDefinition
    {
        #region Identity

        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public Texture2D Icon { get; set; }
        public ItemType Type { get; init; }

        #endregion

        #region Stacking

        public bool Stackable { get; init; } = false;
        public int MaxStackSize { get; init; } = 99;

        #endregion

        #region Combate V2

        public float Cooldown { get; init; } = 0.5f;
        public List<BaseProperty> Properties { get; set; } = new();
        public List<EffectDefinition> OnHitEffects { get; set; } = new();
        public List<EffectDefinition> OnUseEffects { get; set; } = new();
        public PackedScene HitboxScene { get; set; }

        #endregion

        #region Helpers

        public bool IsEquippable => true;

        #endregion

        #region Métodos do item

        public abstract void Use(Player player, ItemInstance instance);

        public virtual void OnEquip(Player player, ItemInstance instance)
        {
        }

        public virtual void OnUnequip(Player player, ItemInstance instance)
        {

        }

        #endregion
    }
}