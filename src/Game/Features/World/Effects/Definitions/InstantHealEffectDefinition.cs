using Jogo25D.Characters;
using Jogo25D.Items;

namespace Jogo25D.Effects
{
    public class InstantHealEffectDefinition : EffectDefinition
    {
        public int HealAmount { get; set; } = 30;

        public override void Apply(Player player, EffectDefinitionData data, float delta)
        {
            if (data.Expired)
            {
                return;
            }

            data.Expired = true;

            player.ReceiveDamage(new DamageInfo
            {
                Type = DamageType.Physical,
                Amount = -HealAmount,
                SourcePeerId = -1
            });
        }
    }
}
