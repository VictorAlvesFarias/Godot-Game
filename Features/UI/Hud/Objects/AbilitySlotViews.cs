using Godot;

namespace Jogo25D.UI
{
    public class AbilitySlotViews
    {
        public Panel Panel { get; set; }
        public ProgressBar FillBar { get; set; }
        public TextureRect IconRect { get; set; }
        public Label InnerNameLabel { get; set; }
        public Label TimerLabel { get; set; }
        public Label ChargesLabel { get; set; }

        public AbilitySlotViews(Panel panel, ProgressBar fillBar, TextureRect iconRect, Label innerNameLabel, Label timerLabel, Label chargesLabel)
        {
            Panel = panel;
            FillBar = fillBar;
            IconRect = iconRect;
            InnerNameLabel = innerNameLabel;
            TimerLabel = timerLabel;
            ChargesLabel = chargesLabel;
        }
    }
}
