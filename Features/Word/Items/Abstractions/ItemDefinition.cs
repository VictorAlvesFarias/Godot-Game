using Godot;
using Jogo25D.Characters;
using Jogo25D.Effects;
using Jogo25D.Features.Word.Items.Resources;
using Jogo25D.Properties;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Items
{
    public abstract class ItemDefinition
    {
        #region Properties

        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public Texture2D Icon { get; set; }
        public ItemType Type { get; init; }
        public bool Stackable { get; init; } = false;
        public int MaxStackSize { get; init; } = 99;
        public bool Rechargeable { get; set; }
        public float Cooldown { get; init; } = 0.5f;
        public bool IsEquippable => true;
        public Godot.Collections.Array<BasePropertyData> Properties { get; set; } = new();
        public Godot.Collections.Array<EffectDefinition> OnHitEffects { get; set; } = new();
        public Godot.Collections.Array<EffectDefinition> OnUseEffects { get; set; } = new();

        #endregion

        #region Node references

        public PackedScene HitboxScene { get; set; }

        #endregion

        #region Core - Abstract

        public abstract void Use(Player player, ItemDefinitionData data);

        public virtual void OnEquip(Player player, ItemDefinitionData data) { }

        public virtual void OnUnequip(Player player, ItemDefinitionData data) { }

        #endregion

        #region Core - Timers

        public void TriggerReloadTimer(ItemDefinitionData data)
        {
            if (!CanReload(data))
            {
                return;
            }

            var chargesProp = Properties.OfType<ChargesPropertyData>().FirstOrDefault();

            if (chargesProp != null)
            {
                data.ReloadTimer = chargesProp.ReloadCooldown;
            }
        }

        public void TriggerCooldownTimer(ItemDefinitionData data)
        {
            if (!CanUse(data))
            {
                return;
            }

            data.CooldownRemainingTimer = Cooldown;
        }

        public float GetRemainingReloadTime(ItemDefinitionData data)
        {
            return data == null ? 0f : Mathf.Max(0f, data.ReloadTimer);
        }

        public bool IsEmpty(ItemDefinitionData data)
        {
            return data == null || string.IsNullOrEmpty(data.Id) || data.Quantity <= 0;
        }

        #endregion

        #region Core 

        public float GetReloadProgress(ItemDefinitionData data)
        {
            var chargesProp = Properties.OfType<ChargesPropertyData>().FirstOrDefault();
            var reloadCooldown = chargesProp != null ? chargesProp.ReloadCooldown : 1f;

            if (reloadCooldown <= 0f)
            {
                return 1f;
            }

            return 1f - data.ReloadTimer / reloadCooldown;
        }

        public bool CanReload(ItemDefinitionData data)
        {
            var chargesProp = Properties.OfType<ChargesPropertyData>().FirstOrDefault();

            if (chargesProp == null || chargesProp.InfiniteCharges)
            {
                return false;
            }

            return !IsReloading(data) && data.CurrentCharges < chargesProp.MaxCharges;
        }

        public bool IsReloading(ItemDefinitionData data)
        {
            return data.ReloadTimer > 0f;
        }

        public void ConsumeCharge(ItemDefinitionData data)
        {
            data.CurrentCharges = Math.Max(0, data.CurrentCharges - 1);
        }

        public void FinishReload(int chargesAdded, ItemDefinitionData data)
        {
            var chargesProp = Properties.OfType<ChargesPropertyData>().FirstOrDefault();
            var maxCharges = chargesProp != null ? chargesProp.MaxCharges : chargesAdded;

            data.ReloadTimer = 0f;
            data.CurrentCharges = Math.Min(data.CurrentCharges + chargesAdded, maxCharges);
        }

        public bool CanAddMore(ItemDefinitionData data)
        {
            return Stackable && data.Quantity < MaxStackSize;
        }

        #endregion

        #region Core - Virtuals 

        public virtual bool CanUse(ItemDefinitionData data)
        {
            var chargesProp = Properties.OfType<ChargesPropertyData>().FirstOrDefault();
            var infiniteCharges = chargesProp != null ? chargesProp.InfiniteCharges : true;
            var hasCharges = infiniteCharges || data.CurrentCharges > 0;

            return data.CooldownRemainingTimer <= 0f && !IsReloading(data) && hasCharges;
        }

        public void Update(float delta, ItemDefinitionData data)
        {
            if (data.CooldownRemainingTimer > 0)
            {
                data.CooldownRemainingTimer -= delta;
            }
            if (data.ReloadTimer > 0)
            {
                data. ReloadTimer -= delta;
            }
        }

        #endregion
    }
}