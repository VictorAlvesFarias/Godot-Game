using Godot;
using Jogo25D.Constants;
using System;

namespace Jogo25D.Light
{
    public sealed class LightChunkOverlay
    {
        #region Constructors

        public LightChunkOverlay(Node2D root, Vector2I chunkCoord, int tileSize)
        {
            _tileSize = tileSize;

            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;

            Material = new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://Assets/Shaders/terrain_light_overlay.gdshader")
            };

            _sprite = new Sprite2D
            {
                Name = $"LightChunk_{chunkCoord.X}_{chunkCoord.Y}",
                Centered = false,
                Position = new Vector2(chunkCoord.X * chunkSize * tileSize, chunkCoord.Y * chunkSize * tileSize),
                Scale = new Vector2(tileSize, tileSize),
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                ZIndex = LightingConstants.TERRAIN_OVERLAY_Z_INDEX,
                Material = Material,
            };

            root.AddChild(_sprite);
        }

        #endregion

        #region Dinamic properties

        private readonly Sprite2D _sprite;

        // Guardado: os laços por quadro do LightMap2D leem o material de todos os overlays.
        public ShaderMaterial Material { get; }
        private readonly int _tileSize;

        private LightTextureOutput _output;
        private ImageTexture _maskTexture;

        // Mascara do terreno (Rg8), guardada para remendar so o pedaco em volta de uma edicao.
        private byte[] _mask;
        private Vector2I _maskSize;

        #endregion

        #region Core - Atualizacao

        // O terreno mudou nas celulas `cells` (relativas ao chunk): troca esse pedaco da mascara pelo `patch` (do tamanho do
        // pedaco, em pixels). Falso sem mascara ainda.
        public bool PatchTerrain(Rect2I cells, Image patch)
        {
            if (_mask == null)
            {
                return false;
            }

            var data = patch.GetData();
            var rowBytes = cells.Size.X * _tileSize * 2;

            for (var row = 0; row < cells.Size.Y * _tileSize; row++)
            {
                var target = ((cells.Position.Y * _tileSize + row) * _maskSize.X + cells.Position.X * _tileSize) * 2;

                Array.Copy(data, row * rowBytes, _mask, target, rowBytes);
            }

            UploadMask();

            return true;
        }

        // mask: a mascara do terreno, ou nula quando so a luz mudou.
        public void UpdateOutput(LightTextureOutput output, Image mask, int padding, int regionSize)
        {
            var material = Material;
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;

            // Sem mascara nova (so a luz mudou), fica a ja aplicada.
            if (mask != null)
            {
                _maskSize = mask.GetSize();
                _mask = mask.GetData();

                UploadMask();
            }

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

        private void UploadMask()
        {
            using var image = Image.CreateFromData(_maskSize.X, _maskSize.Y, false, Image.Format.Rg8, _mask);

            TerrainLightMask.UpdateMaterial(Material, ref _maskTexture, image);
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
