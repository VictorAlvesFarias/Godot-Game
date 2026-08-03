using Godot;
using Jogo25D.Characters;
using Jogo25D.Effects;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Features.World.Resolver.Singletons;
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
        public float Cooldown { get; init; } = 0.5f;
        public Godot.Collections.Array<BasePropertyData> Properties { get; set; } = new();
        public Godot.Collections.Array<BasePropertyData> Modifiers { get; set; } = new();
        public Godot.Collections.Array<string> Effects { get; set; } = new();

        #endregion

        #region Node references

        public PackedScene HitboxScene { get; set; }

        #endregion

        #region Core - Abstract

        public abstract void Use(Player player, ItemData data);

        public virtual void OnEquip(Player player, ItemData data)
        {
            foreach (var modifier in Modifiers)
            {
                player.Properties.Add(modifier);
            }

            foreach (var modifier in data.Modifiers)
            {
                player.Properties.Add(modifier);
            }
        }

        public virtual void OnUnequip(Player player, ItemData data)
        {
            foreach (var modifier in Modifiers)
            {
                player.Properties.Remove(modifier);
            }

            foreach (var modifier in data.Modifiers)
            {
                player.Properties.Remove(modifier);
            }
        }

        #endregion

        #region Core - Timers

        public void TriggerReloadTimer(ItemData data)
        {
            if (!CanReload(data))
            {
                return;
            }

            var chargesProp = Resolver.Resolve(Properties.OfType<ChargesPropertyData>().ToList()).FirstOrDefault();

            if (chargesProp != null)
            {
                data.ReloadTimer = chargesProp.ReloadCooldown;
            }
        }

        public void TriggerCooldownTimer(ItemData data)
        {
            if (!CanUse(data))
            {
                return;
            }

            data.CooldownRemainingTimer = Cooldown;
        }

        public float GetRemainingReloadTime(ItemData data)
        {
            return data == null ? 0f : Mathf.Max(0f, data.ReloadTimer);
        }

        public bool IsEmpty(ItemData data)
        {
            return data == null || string.IsNullOrEmpty(data.Id) || data.Quantity <= 0;
        }

        #endregion

        #region Core - Item

        public float GetReloadProgress(ItemData data)
        {
            var chargesProp = Resolver.Resolve(Properties.OfType<ChargesPropertyData>().ToList()).FirstOrDefault();
            var reloadCooldown = chargesProp != null ? chargesProp.ReloadCooldown : 1f;

            if (reloadCooldown <= 0f)
            {
                return 1f;
            }

            return 1f - data.ReloadTimer / reloadCooldown;
        }

        public bool CanReload(ItemData data)
        {
            var chargesProp = Resolver.Resolve(Properties.OfType<ChargesPropertyData>().ToList()).FirstOrDefault();

            if (chargesProp == null || chargesProp.InfiniteCharges)
            {
                return false;
            }

            return !IsReloading(data) && data.CurrentCharges < chargesProp.MaxCharges;
        }

        public bool IsReloading(ItemData data)
        {
            return data.ReloadTimer > 0f;
        }

        public void ConsumeCharge(ItemData data)
        {
            data.CurrentCharges = Math.Max(0, data.CurrentCharges - 1);
        }

        public void FinishReload(int chargesAdded, ItemData data)
        {
            var chargesProp = Resolver.Resolve(Properties.OfType<ChargesPropertyData>().ToList()).FirstOrDefault();
            var maxCharges = chargesProp != null ? chargesProp.MaxCharges : chargesAdded;

            data.ReloadTimer = 0f;
            data.CurrentCharges = Math.Min(data.CurrentCharges + chargesAdded, maxCharges);
        }

        #endregion

        #region Core - Virtuals 

        public virtual bool CanUse(ItemData data)
        {
            var chargesProp = Resolver.Resolve(Properties.OfType<ChargesPropertyData>().ToList()).FirstOrDefault();
            var infiniteCharges = chargesProp != null ? chargesProp.InfiniteCharges : true;
            var hasCharges = infiniteCharges || data.CurrentCharges > 0;

            return data.CooldownRemainingTimer <= 0f && !IsReloading(data) && hasCharges;
        }

        public void Update(float delta, ItemData data)
        {
            if (data.CooldownRemainingTimer > 0)
            {
                data.CooldownRemainingTimer -= delta;
            }
            if (data.ReloadTimer > 0)
            {
                data.ReloadTimer -= delta;
            }
        }

        #endregion

        #region Core - Indicator

        public virtual void UpdateIndicator(Player player, ItemData data, float delta) { }

        public virtual void HideIndicator(Player player) { }

        public virtual void DestroyIndicator() { }

        #endregion
    }
}