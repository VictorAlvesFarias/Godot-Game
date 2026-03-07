using System.Collections.Generic;

namespace Jogo25D.Properties
{
    public class ChargesProperty : BaseProperty
    {
        public int MaxCharges { get; set; } = 1;
        public string ChargeItemId { get; set; } = "";
        public bool InfiniteCharges { get; set; } = true;
        public float ReloadCooldown { get; set; } = 1.0f;
    }
}