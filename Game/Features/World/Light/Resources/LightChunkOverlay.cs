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
        private readonly int GameTileSize;
        private ImageTexture _texture;
        private LightTextureOutput _output;
        private ImageTexture _maskTexture;

        public LightChunkOverlay(Node2D root, Vector2I chunkCoord, int tileSize)
        {
            GameTileSize=tileSize;
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

        public void UpdateOutput(LightTextureOutput output,Image mask,int padding,int regionSize)
        {
            var material=(ShaderMaterial)_sprite.Material;
            TerrainLightMask.UpdateMaterial(material,ref _maskTexture,mask);
            material.SetShaderParameter("light_uv_rect",new Vector4((float)padding/regionSize,(float)padding/regionSize,(float)ChunkStreamingConstants.CHUNK_SIZE/regionSize,(float)ChunkStreamingConstants.CHUNK_SIZE/regionSize));
            material.SetShaderParameter("ambient_min",LightingConstants.AMBIENT_MIN);
            _sprite.Texture=output.Texture;
            _sprite.Scale=new Vector2((float)(ChunkStreamingConstants.CHUNK_SIZE*GameTileSize)/regionSize,(float)(ChunkStreamingConstants.CHUNK_SIZE*GameTileSize)/regionSize);
            _output?.Dispose();_output=output;
        }

        public void Dispose()
        {
            _output?.Dispose();_output=null;
            if (GodotObject.IsInstanceValid(_sprite))
            {
                _sprite.QueueFree();
            }
        }
    }
}
