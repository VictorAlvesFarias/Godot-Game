using Godot;

namespace Jogo25D.Chunks
{
    public class DiscoveredMapImage
    {
        private const int InitialSize = 256;
        private const int GrowMargin = 64;

        private Image _image;
        private ImageTexture _texture;
        private Vector2I _origin;
        private bool _dirty;

        public Vector2I Origin => _origin;

        public Texture2D GetTexture()
        {
            EnsureImage();

            if (_texture == null)
            {
                _texture = ImageTexture.CreateFromImage(_image);
                _dirty = false;
            }
            else if (_dirty)
            {
                _texture.Update(_image);
                _dirty = false;
            }

            return _texture;
        }

        public void SetCell(Vector2I cell, Color color)
        {
            EnsureImage();
            EnsureCovers(cell);

            _image.SetPixel(cell.X - _origin.X, cell.Y - _origin.Y, color);

            _dirty = true;
        }

        public void Reset()
        {
            _image = null;
            _texture = null;
            _dirty = false;
            _origin = Vector2I.Zero;
        }

        private void EnsureImage()
        {
            if (_image != null)
            {
                return;
            }

            _image = Image.CreateEmpty(InitialSize, InitialSize, false, Image.Format.Rgba8);
            _origin = new Vector2I(-InitialSize / 2, -InitialSize / 2);
        }

        private void EnsureCovers(Vector2I cell)
        {
            var localX = cell.X - _origin.X;
            var localY = cell.Y - _origin.Y;

            if (localX >= 0 && localY >= 0 && localX < _image.GetWidth() && localY < _image.GetHeight())
            {
                return;
            }

            var oldImage = _image;
            var oldOrigin = _origin;
            var oldWidth = oldImage.GetWidth();
            var oldHeight = oldImage.GetHeight();

            var minX = Mathf.Min(oldOrigin.X, cell.X) - GrowMargin;
            var minY = Mathf.Min(oldOrigin.Y, cell.Y) - GrowMargin;
            var maxX = Mathf.Max(oldOrigin.X + oldWidth, cell.X + 1) + GrowMargin;
            var maxY = Mathf.Max(oldOrigin.Y + oldHeight, cell.Y + 1) + GrowMargin;

            var newImage = Image.CreateEmpty(maxX - minX, maxY - minY, false, Image.Format.Rgba8);

            newImage.BlitRect(
                oldImage,
                new Rect2I(Vector2I.Zero, new Vector2I(oldWidth, oldHeight)),
                new Vector2I(oldOrigin.X - minX, oldOrigin.Y - minY));

            _image = newImage;
            _origin = new Vector2I(minX, minY);

            _texture = null;
        }
    }
}
