using Godot;

namespace Jogo25D.UI
{
    [Tool]
    public partial class PhysicalSizeTextureRect : TextureRect
    {
        [Export]
        public Vector2 PhysicalSize
        {
            get => _physicalSize;
            set
            {
                _physicalSize = value;
                ApplySize();
            }
        }

        [Export(PropertyHint.Range, "0.01,10,0.01")]
        public float Zoom
        {
            get => _zoom;
            set
            {
                _zoom = value;
                ApplySize();
            }
        }

        private Vector2 _physicalSize = Vector2.Zero;
        private float _zoom = 1f;

        public override void _Ready()
        {
            if (_physicalSize == Vector2.Zero && Texture != null)
            {
                _physicalSize = Texture.GetSize();
            }

            ApplySize();
        }

        private void ApplySize()
        {
            var appliedSize = _physicalSize * _zoom;

            CustomMinimumSize = appliedSize;
            Size = appliedSize;
        }
    }
}
