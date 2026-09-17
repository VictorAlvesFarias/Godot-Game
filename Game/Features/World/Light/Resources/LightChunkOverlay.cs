using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Light
{
    // Sprite2D com blend multiplicativo por cima do chunk: 1 pixel = 1 tile, escalado pra cobrir
    // o chunk inteiro em coordenadas de mundo. TextureFilter Linear da o gradiente suave de graca
    // (bilinear do GPU), sem precisar de supersampling manual.
    public sealed class LightChunkOverlay
    {
        private readonly Sprite2D _sprite;
        private ImageTexture _texture;
        private ImageTexture _maskTexture;

        public LightChunkOverlay(Node2D root, Vector2I chunkCoord, int tileSize)
        {
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;

            _sprite = new Sprite2D
            {
                Name = $"LightChunk_{chunkCoord.X}_{chunkCoord.Y}",
                Centered = false,
                Position = new Vector2(chunkCoord.X * chunkSize * tileSize, chunkCoord.Y * chunkSize * tileSize),
                Scale = new Vector2(tileSize, tileSize),
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Features/World/Light/Resources/TerrainLightOverlay.gdshader") },
            };

            root.AddChild(_sprite);
        }

        public void UpdateTexture(Image image, Image mask)
        {
            TerrainLightMask.UpdateMaterial((ShaderMaterial)_sprite.Material, ref _maskTexture, mask);
            if (_texture == null)
            {
                _texture = ImageTexture.CreateFromImage(image);
                _sprite.Texture = _texture;

                return;
            }

            _texture.Update(image);
        }

        public void Dispose()
        {
            if (GodotObject.IsInstanceValid(_sprite))
            {
                _sprite.QueueFree();
            }
        }
    }
}
