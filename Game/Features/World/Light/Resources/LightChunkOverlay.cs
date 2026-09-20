using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Light
{
    public sealed class LightChunkOverlay
    {
        #region Constructors

        public LightChunkOverlay(Node2D root, Vector2I chunkCoord, int tileSize)
        {
            _tileSize = tileSize;

            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;

            _sprite = new Sprite2D
            {
                Name = $"LightChunk_{chunkCoord.X}_{chunkCoord.Y}",
                Centered = false,
                Position = new Vector2(chunkCoord.X * chunkSize * tileSize, chunkCoord.Y * chunkSize * tileSize),
                Scale = new Vector2(tileSize, tileSize),
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                Material = new ShaderMaterial
                {
                    Shader = GD.Load<Shader>("res://Assets/Shaders/terrain_light_overlay.gdshader")
                },
            };

            root.AddChild(_sprite);
        }

        #endregion

        #region Dinamic properties

        private readonly Sprite2D _sprite;
        private readonly int _tileSize;

        private LightTextureOutput _output;
        private ImageTexture _maskTexture;

        #endregion

        #region Core - Atualizacao

        public void UpdateOutput(LightTextureOutput output, Image mask, int padding, int regionSize)
        {
            var material = (ShaderMaterial)_sprite.Material;
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;

            TerrainLightMask.UpdateMaterial(material, ref _maskTexture, mask);

            material.SetShaderParameter("light_uv_rect", new Vector4(
                (float)padding / regionSize,
                (float)padding / regionSize,
                (float)chunkSize / regionSize,
                (float)chunkSize / regionSize));
            material.SetShaderParameter("ambient_min", LightingConstants.AMBIENT_MIN);

            _sprite.Texture = output.Texture;
            _sprite.Scale = new Vector2(
                (float)(chunkSize * _tileSize) / regionSize,
                (float)(chunkSize * _tileSize) / regionSize);

            _output?.Dispose();
            _output = output;
        }

        #endregion

        #region Core - Descarte

        public void Dispose()
        {
            _output?.Dispose();
            _output = null;

            if (GodotObject.IsInstanceValid(_sprite))
            {
                _sprite.QueueFree();
            }
        }

        #endregion
    }
}
