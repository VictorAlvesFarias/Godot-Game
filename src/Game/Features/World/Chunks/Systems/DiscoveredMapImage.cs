using Godot;

namespace Jogo25D.Chunks
{
    public class DiscoveredMapImage
    {
        #region Dinamic properties

        public Image Image { get; set; }
        public ImageTexture Texture { get; set; }
        public Vector2I Origin { get; set; }
        public bool Dirty { get; set; }

        #endregion

        public Texture2D GetTexture()
        {
            EnsureImage();

            if (Texture == null)
            {
                Texture = ImageTexture.CreateFromImage(Image);
                Dirty = false;
            }
            else if (Dirty)
            {
                Texture.Update(Image);
                Dirty = false;
            }

            return Texture;
        }

        public void SetCell(Vector2I cell, Color color)
        {
            EnsureImage();
            EnsureCovers(cell);

            Image.SetPixel(cell.X - Origin.X, cell.Y - Origin.Y, color);

            Dirty = true;
        }

        public void Reset()
        {
            Image = null;
            Texture = null;
            Dirty = false;
            Origin = Vector2I.Zero;
        }

        private void EnsureImage()
        {
            if (Image != null)
            {
                return;
            }

            Image = Godot.Image.CreateEmpty(256, 256, false, Godot.Image.Format.Rgba8);
            Origin = new Vector2I(-256 / 2, -256 / 2);
        }

        private void EnsureCovers(Vector2I cell)
        {
            var localX = cell.X - Origin.X;
            var localY = cell.Y - Origin.Y;

            if (localX >= 0 && localY >= 0 && localX < Image.GetWidth() && localY < Image.GetHeight())
            {
                return;
            }

            var oldImage = Image;
            var oldOrigin = Origin;
            var oldWidth = oldImage.GetWidth();
            var oldHeight = oldImage.GetHeight();
            var minX = Mathf.Min(oldOrigin.X, cell.X) - 64;
            var minY = Mathf.Min(oldOrigin.Y, cell.Y) - 64;
            var maxX = Mathf.Max(oldOrigin.X + oldWidth, cell.X + 1) + 64;
            var maxY = Mathf.Max(oldOrigin.Y + oldHeight, cell.Y + 1) + 64;
            var newImage = Godot.Image.CreateEmpty(maxX - minX, maxY - minY, false, Godot.Image.Format.Rgba8);

            newImage.BlitRect(
                oldImage,
                new Rect2I(Vector2I.Zero, new Vector2I(oldWidth, oldHeight)),
                new Vector2I(oldOrigin.X - minX, oldOrigin.Y - minY)
            );

            Image = newImage;
            Origin = new Vector2I(minX, minY);

            Texture = null;
        }
    }
}
