using Jogo25D.Characters;
using Jogo25D.Items;

namespace Jogo25D.Effects
{
    public class PoisonEffect : EffectDefinition
    {
        public int DamagePerSecond { get; set; } = 5;

        private float _accumulator;

        public override void Apply(Player player, float delta)
        {
            _accumulator += delta;

            if (_accumulator >= 1f)
            {
                _accumulator -= 1f;

                player.ReceiveDamage(new DamageInfo
                {
                    Amount = DamagePerSecond,
                    Type = DamageType.Poison,
                    SourcePeerId = 0
                });
            }
        }
    }
}
