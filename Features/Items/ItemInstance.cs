using System.Collections.Generic;
using System.Linq;
using Jogo25D.Properties;
using Jogo25D.Effects;

namespace Jogo25D.Items
{
    public class ItemInstance
    {
        #region Core data

        public ItemDefinition Definition { get; set; }
        public int Quantity { get; set; }
        public float CooldownRemaining { get; set; }
        public List<BaseProperty> Properties { get; set; } = new();
        public List<EffectDefinition> OnHitEffects { get; set; } = new();

        #endregion

        #region Charges & Reload

        public virtual bool CanAttack()
        {
            return CooldownRemaining <= 0f;
        }

        #endregion

        #region Slot helpers

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
            Properties.Clear();
            OnHitEffects.Clear();
        }

        #endregion

        #region Update

        public virtual void Update(float delta)
        {
            if (CooldownRemaining > 0)
            {
                CooldownRemaining -= delta;
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

        #endregion

        #region Quantity helpers

        public void AddQuantity(int amount)
        {
            Quantity += amount;
        }

        public void RemoveQuantity(int amount)
        {
            Quantity -= amount;
        }

        #endregion
    }
}