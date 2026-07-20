using Godot;

namespace Jogo25D.UI
{
    public static class UISlotStyle
    {
        public static StyleBoxFlat CreateDefault()
        {
            var style = new StyleBoxFlat();

            style.BgColor = new Color(0.16f, 0.16f, 0.16f, 0.85f);
            style.BorderColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            style.SetBorderWidthAll(2);

            return style;
        }
    }
}
