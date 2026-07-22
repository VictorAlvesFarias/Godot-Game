using Jogo25D.Characters;
using Jogo25D.Items;

namespace Jogo25D.Effects
{
    // Cura de uma vez so (nao ao longo do tempo, diferente de health_regen).
    // Duration=0 no EffectDB faz o Tick base nunca expirar por tempo, entao
    // o proprio Apply se auto-encerra marcando Expired na primeira (e unica)
    // execucao - TickEffects remove do CurrentEffects logo em seguida, no
    // mesmo frame.
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
