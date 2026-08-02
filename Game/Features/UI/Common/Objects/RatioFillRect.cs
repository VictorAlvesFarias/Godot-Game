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
        private Control _reference;

        public override void _Ready()
        {
            _reference = GetNodeOrNull<Control>(ReferencePath);

            ApplyLayout();
        }

        public override void _Process(double delta)
        {
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            if (_reference == null)
            {
                return;
            }

            var size = _reference.Size;

            Position = new Vector2(size.X * LeftFraction, size.Y * TopFraction);
            Size = new Vector2(size.X * WidthFraction * _ratio, size.Y * HeightFraction);
        }
    }
}
