using Jogo25D.Characters;
using Jogo25D.Items;
using System.Collections.Generic;
using System.Dynamic;

namespace Jogo25D.Effects
{
    public class DamageEffectDefinition : EffectDefinition
    {
        public List<DamageInfo> Damages { get; set; }
        public float Timer { get; set; }

        protected override void Apply(Player player, float delta)
        {
            Timer += delta;

            if (Timer >= 1f)
            {
                Timer -= 1f;

                foreach (var damage in Damages)
                {
                    player.ReceiveDamage(damage);
                }

            }
        }
    }
}
