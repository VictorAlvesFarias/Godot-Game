using Godot;

namespace Jogo25D.UI
{
    [Tool]
    public partial class RatioFillRect : ColorRect
    {
        [Export] public NodePath ReferencePath { get; set; }

        [Export(PropertyHint.Range, "0,1,0.001")] public float LeftFraction { get; set; }
        [Export(PropertyHint.Range, "0,1,0.001")] public float TopFraction { get; set; }
        [Export(PropertyHint.Range, "0,1,0.001")] public float WidthFraction { get; set; } = 1f;
        [Export(PropertyHint.Range, "0,1,0.001")] public float HeightFraction { get; set; } = 1f;

        [Export(PropertyHint.Range, "0,1,0.001")]
        public float Ratio
        {
            get => _ratio;
            set
            {
                _ratio = Mathf.Clamp(value, 0f, 1f);
                ApplyLayout();
            }
        }

        private float _ratio = 1f;

        public Control Reference { get; set; }

        public override void _Ready()
        {
            Reference = GetNodeOrNull<Control>(ReferencePath);

            ApplyLayout();
        }

        public override void _Process(double delta)
        {
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            if (Reference == null)
            {
                return;
            }

            var size = Reference.Size;

            Position = new Vector2(size.X * LeftFraction, size.Y * TopFraction);
            Size = new Vector2(size.X * WidthFraction * _ratio, size.Y * HeightFraction);
        }
    }
}
