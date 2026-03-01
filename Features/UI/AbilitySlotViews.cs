using Godot;

namespace Jogo25D.UI
{
    public class AbilitySlotViews
    {
        public VBoxContainer Wrapper { get; set; }
        public Panel Panel { get; set; }
        public ProgressBar FillBar { get; set; }
        public Label TimerLabel { get; set; }
        public Label NameLabel { get; set; }

        public AbilitySlotViews(VBoxContainer wrapper, Panel panel, ProgressBar fillBar, Label timerLabel, Label nameLabel)
        {
            Wrapper = wrapper;
            Panel = panel;
            FillBar = fillBar;
            TimerLabel = timerLabel;
            NameLabel = nameLabel;
        }
    }
}
