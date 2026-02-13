using Godot;
using System;

namespace Jogo25D.UI
{
    public partial class ContextMenuUI : Panel
    {
        [Signal]
        public delegate void OptionSelectedEventHandler(string optionName);

        private VBoxContainer container;

        public override void _Ready()
        {
            Visible = false;
            MouseFilter = MouseFilterEnum.Stop;

            var styleBox = new StyleBoxFlat();

            styleBox.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            styleBox.BorderColor = Colors.White;
            styleBox.BorderWidthLeft = 2;
            styleBox.BorderWidthRight = 2;
            styleBox.BorderWidthTop = 2;
            styleBox.BorderWidthBottom = 2;

            AddThemeStyleboxOverride("panel", styleBox);

            container = new VBoxContainer();
            container.AddThemeConstantOverride("separation", 0);
            container.MouseFilter = MouseFilterEnum.Pass;

            AddChild(container);
        }

        public void ShowMenu(Vector2 position, string[] options)
        {
            foreach (Node child in container.GetChildren())
            {
                container.RemoveChild(child);
                child.QueueFree();
            }

            foreach (var option in options)
            {
                var button = new Button();

                button.Text = option;
                button.CustomMinimumSize = new Vector2(120, 30);
                button.Alignment = HorizontalAlignment.Center;
                button.MouseFilter = MouseFilterEnum.Stop;

                string optionCopy = option;

                button.Pressed += () =>
                {
                    EmitSignal(SignalName.OptionSelected, optionCopy);
                    Visible = false;
                };

                container.AddChild(button);
            }

            GlobalPosition = position;
            Visible = true;

            MoveToFront();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!Visible) return;

            if (@event is InputEventMouseButton mouseEvent)
            {
                if (mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
                {
                    var rect = GetGlobalRect();

                    if (!rect.HasPoint(mouseEvent.GlobalPosition))
                    {
                        Visible = false;
                    }
                }
            }
        }
    }
}
