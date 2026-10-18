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

        // Mascara so do terreno (Rg8, sem agua) e a aplicada (terreno + agua): a agua muda sem refazer o terreno.
        private byte[] _terrainMask;
        private byte[] _mask;
        private Vector2I _maskSize;

        #endregion

        #region Core - Atualizacao

        // O terreno mudou nas celulas `cells` (relativas ao chunk): troca esse pedaco da mascara do terreno pelo `patch`
        // (do tamanho do pedaco, em pixels) e recompoe a agua nele. Falso sem mascara de terreno ainda.
        public bool PatchTerrain(Rect2I cells, Image patch, Func<Vector2I, int> waterRows)
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

                Array.Copy(data, row * rowBytes, _terrainMask, target, rowBytes);
            }

            ApplyWater(cells, waterRows);
            UploadMask();

            return true;
        }

        // A agua mudou nas celulas `cells` (relativas ao chunk): refaz so esse pedaco, do terreno guardado + agua, e mantem
        // a luz. Falso sem mascara de terreno ainda.
        public bool PatchWater(Rect2I cells, Func<Vector2I, int> waterRows)
        {
            if (_mask == null)
            {
                return false;
            }

            ApplyWater(cells, waterRows);
            UploadMask();

            return true;
        }

        // mask: a mascara do terreno (sem agua), ou nula quando so a luz mudou. waterRows: linhas de pixel cobertas pela
        // agua na celula (relativa ao chunk), de baixo para cima.
        public void UpdateOutput(LightTextureOutput output, Image mask, Func<Vector2I, int> waterRows, int padding, int regionSize)
        {
            var material = Material;
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;

            // Sem mascara nova (so a luz mudou), fica a do terreno ja aplicada.
            if (mask != null)
            {
                _maskSize = mask.GetSize();
                _terrainMask = mask.GetData();
                _mask = (byte[])_terrainMask.Clone();

                ApplyWater(new Rect2I(Vector2I.Zero, Vector2I.One * chunkSize), waterRows);
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

        // A agua cobre a celula inteira na mascara (R cheio, sem parede exposta) nas linhas de baixo que ela ocupa, como o
        // tile de agua; o resto da celula volta ao terreno.
        private void ApplyWater(Rect2I cells, Func<Vector2I, int> waterRows)
        {
            var width = _maskSize.X;

            for (var cy = cells.Position.Y; cy < cells.End.Y; cy++)
            {
                for (var cx = cells.Position.X; cx < cells.End.X; cx++)
                {
                    var rows = waterRows(new Vector2I(cx, cy));

                    for (var py = 0; py < _tileSize; py++)
                    {
                        var wet = py >= _tileSize - rows;
                        var index = ((cy * _tileSize + py) * width + cx * _tileSize) * 2;

                        for (var px = 0; px < _tileSize; px++, index += 2)
                        {
                            _mask[index] = wet ? (byte)255 : _terrainMask[index];
                            _mask[index + 1] = wet ? (byte)0 : _terrainMask[index + 1];
                        }
                    }
                }
            }
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
