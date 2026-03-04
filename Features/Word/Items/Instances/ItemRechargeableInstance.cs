using System;
using System.Linq;
using Jogo25D.Properties;

namespace Jogo25D.Items
{
    public class ItemRechargeableInstance : ItemInstance
    {
        #region Charges & Reload

        public int CurrentCharges { get; set; }
        private float _reloadTimer;

        public bool IsReloading
        {
            get
            {
                return _reloadTimer > 0f;
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
            return 1f - _reloadTimer / reloadCooldown;
        }

        public float GetRemainingReloadTime()
        {
            return _reloadTimer;
        }

        public override bool CanAttack()
        {
            var chargesProp = Properties.OfType<ChargesProperty>().FirstOrDefault();
            bool infiniteCharges = chargesProp != null ? chargesProp.InfiniteCharges : true;
            bool hasCharges = infiniteCharges || CurrentCharges > 0;
            return base.CanAttack() && !IsReloading && hasCharges;
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
                _reloadTimer = chargesProp.ReloadCooldown;
            }
        }

        public void FinishReload(int chargesAdded)
        {
            var chargesProp = Properties.OfType<ChargesProperty>().FirstOrDefault();
            int maxCharges  = chargesProp != null ? chargesProp.MaxCharges : chargesAdded;
            _reloadTimer   = 0f;
            CurrentCharges = Math.Min(CurrentCharges + chargesAdded, maxCharges);
        }

        #endregion

        #region Update

        public override void Update(float delta)
        {
            base.Update(delta);
            if (_reloadTimer > 0)
            {
                _reloadTimer -= delta;
            }
        }

        public override void Clear()
        {
            base.Clear();
            CurrentCharges = 0;
            _reloadTimer = 0;
        }

        #endregion
    }
}
