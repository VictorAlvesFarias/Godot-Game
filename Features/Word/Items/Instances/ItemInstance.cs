using System;
using System.Collections.Generic;
using System.Linq;
using Jogo25D.Properties;
using Jogo25D.Effects;

namespace Jogo25D.Items
{
    public class ItemInstance
    {

        public ItemDefinition Definition { get; set; }
        public int Quantity { get; set; }
        public float CooldownRemaining { get; set; }
        public List<BaseProperty> Properties { get; set; } = new();
        public List<EffectDefinition> OnHitEffects { get; set; } = new();
        public List<EffectDefinition> OnUseEffects { get; set; } = new();

        public int CurrentCharges { get; set; }
        public float ReloadTimer { get; set; }

        public bool IsReloading
        {
            get
            {
                return ReloadTimer > 0f;
            }
        }

        public float GetReloadProgress()
        {
            var chargesProp = Properties.OfType<ChargesProperty>().FirstOrDefault();
            float reloadCooldown = chargesProp != null ? chargesProp.ReloadCooldown : 1f;
            if (reloadCooldown <= 0f)
            {
                return 1f;
            }
            return 1f - ReloadTimer / reloadCooldown;
        }

        public float GetRemainingReloadTime()
        {
            return ReloadTimer;
        }

        public virtual bool CanAttack()
        {
            var chargesProp = Properties.OfType<ChargesProperty>().FirstOrDefault();
            bool infiniteCharges = chargesProp != null ? chargesProp.InfiniteCharges : true;
            bool hasCharges = infiniteCharges || CurrentCharges > 0;
            return CooldownRemaining <= 0f && !IsReloading && hasCharges;
        }

        public bool CanReload()
        {
            var chargesProp = Properties.OfType<ChargesProperty>().FirstOrDefault();
            if (chargesProp == null || chargesProp.InfiniteCharges)
            {
                return false;
            }
            return !IsReloading && CurrentCharges < chargesProp.MaxCharges;
        }

        public void ConsumeCharge()
        {
            CurrentCharges = Math.Max(0, CurrentCharges - 1);
        }

        public void StartReload()
        {
            if (!CanReload())
            {
                return;
            }
            var chargesProp = Properties.OfType<ChargesProperty>().FirstOrDefault();
            if (chargesProp != null)
            {
                ReloadTimer = chargesProp.ReloadCooldown;
            }
        }

        public void FinishReload(int chargesAdded)
        {
            var chargesProp = Properties.OfType<ChargesProperty>().FirstOrDefault();
            int maxCharges = chargesProp != null ? chargesProp.MaxCharges : chargesAdded;
            ReloadTimer = 0f;
            CurrentCharges = Math.Min(CurrentCharges + chargesAdded, maxCharges);
        }

        public bool IsEmpty()
        {
            return Definition == null || Quantity <= 0;
        }

        public bool CanAddMore()
        {
            if (IsEmpty() || Definition == null)
            {
                return true;
            }
            return Definition.Stackable && Quantity < Definition.MaxStackSize;
        }

        public virtual void Clear()
        {
            Definition = null;
            Quantity = 0;
            CooldownRemaining = 0;
            CurrentCharges = 0;
            ReloadTimer = 0;
            Properties.Clear();
            OnHitEffects.Clear();
            OnUseEffects.Clear();
        }

        public virtual void Update(float delta)
        {
            if (CooldownRemaining > 0)
            {
                CooldownRemaining -= delta;
            }
            if (ReloadTimer > 0)
            {
                ReloadTimer -= delta;
            }
        }

        public bool CanUse()
        {
            return CanAttack();
        }

        public void TriggerCooldown()
        {
            if (Definition != null)
            {
                CooldownRemaining = Definition.Cooldown;
            }
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
}