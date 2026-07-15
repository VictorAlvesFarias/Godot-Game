using Godot;
using Jogo25D.Characters;
using Jogo25D.Items;
using Jogo25D.Utils.GodotDictionaryParser;
using System.Collections.Generic;
using System.Dynamic;

namespace Jogo25D.Effects
{
    public partial class DamageEffectDefinition : EffectDefinition
    {
        [Export, GodotDictionaryField]
        public Godot.Collections.Array<DamageInfo> Damages { get; set; }

        [Export, GodotDictionaryField]
        public float Timer { get; set; }

        public override void Apply(Player player, float delta)
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