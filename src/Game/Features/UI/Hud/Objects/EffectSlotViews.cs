using Godot;

namespace Jogo25D.UI
{
    public class EffectSlotViews
    {
        public Panel Panel { get; set; }
        public TextureRect IconRect { get; set; }
        public Label TimerLabel { get; set; }

        public EffectSlotViews(Panel panel, TextureRect iconRect, Label timerLabel)
        {
            Panel = panel;
            IconRect = iconRect;
            TimerLabel = timerLabel;
        }
    }
}
