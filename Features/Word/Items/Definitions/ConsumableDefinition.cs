using Godot;
using Jogo25D.Characters;
using Jogo25D.Effects;
using Jogo25D.Features.Word.Items.Resources;
using Jogo25D.Properties;

namespace Jogo25D.Items
{
    public class ConsumableDefinition : ItemDefinition
    {
        public override void Use(Player player, ItemDefinitionData instance)
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

            foreach (var effect in instance.OnUseEffects)
            {
                player.AddEffect(effect);
            }

            instance.Quantity -= 1;

            GD.Print($"[Use] '{Name}' consumido - {instance.Quantity} restante(s)");
        }
    }
}