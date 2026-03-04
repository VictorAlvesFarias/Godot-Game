using Godot;
using Jogo25D.Characters;

namespace Jogo25D.Items
{
    public class ConsumableDefinition : ItemDefinition
    {
        public override void Use(Player player, ItemInstance instance)
        {
            if (!instance.CanUse()) 
            {  
                return;
            }

            instance.TriggerCooldown();

            foreach (var effect in instance.OnUseEffects)
            {
                player.AddEffect(effect);
            }

            instance.RemoveQuantity(1);

            if (instance.Quantity <= 0)
            {
                instance.Clear();
            }

            GD.Print($"[Use] '{Name}' consumido - {instance.Quantity} restante(s)");
        }
    }
}
