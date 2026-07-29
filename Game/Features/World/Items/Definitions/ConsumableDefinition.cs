using Godot;
using Jogo25D.Characters;
using Jogo25D.Effects;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Properties;
using System.Linq;

namespace Jogo25D.Items
{
    public class ConsumableDefinition : ItemDefinition
    {
        #region Core - Virtuals

        public override void Use(Player player, ItemData instance)
        {
            if (instance == null || instance.Quantity <= 0)
            {
                return;
            }

            if (!CanUse(instance))
            {
                return;
            }

            TriggerCooldownTimer(instance);

            foreach (var effect in instance.Effects.Where(e => e.Type == EffectTriggerType.OnUse))
            {
                player.GiveEffect(effect.Id);
            }

            instance.Quantity -= 1;

            GD.Print($"[Use] '{Name}' consumido - {instance.Quantity} restante(s)");
        }

        #endregion
    }
}