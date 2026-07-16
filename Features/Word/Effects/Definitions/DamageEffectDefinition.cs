using Jogo25D.Characters;
using Jogo25D.Items;

namespace Jogo25D.Effects
{
    public class DamageEffectDefinition : EffectDefinition
    {
        #region Properties

        public Godot.Collections.Array<DamageInfo> Damages { get; set; } = new();

        #endregion

        #region Core - Virtuals

        public override void Apply(Player player, EffectDefinitionData data, float delta)
        {
            data.Timer += delta;

            if (data.Timer >= 1f)
            {
                data.Timer -= 1f;

                foreach (var damage in Damages)
                {
                    player.ReceiveDamage(damage);
                }
            }
        }

        #endregion
    }
}
