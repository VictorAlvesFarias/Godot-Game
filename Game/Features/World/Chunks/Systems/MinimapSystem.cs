using Godot;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using System.Collections.Generic;

namespace Jogo25D.Systems
{
    // Mapa de descoberta: marca as celulas que ja foram pintadas alguma vez, por dimensao.
    // Vivia dentro do streaming de tile, que nao tem nada com minimapa.
    //
    // Nao e node e nao conhece rede: e system. Quem chama e o TileStreamingManager, ao terminar
    // de carregar um chunk; quem le e a UI, sempre passando pelo manager.
    public class MinimapSystem
    {
        #region Dinamic properties

        private readonly Dictionary<string, DiscoveredMapImage> _discovered = new();

        #endregion

        #region Core - Registro

        // Varre o chunk recem-pintado e marca no mapa o que tem tile.
        public void RecordChunk(string dimensionId, TileMapLayer layer, Vector2I chunkCoord)
        {
            if (layer == null)
            {
                return;
            }

            var image = Resolve(dimensionId);
            var baseCellX = chunkCoord.X * ChunkStreamingConstants.CHUNK_SIZE;
            var baseCellY = chunkCoord.Y * ChunkStreamingConstants.CHUNK_SIZE;

            for (int localX = 0; localX < ChunkStreamingConstants.CHUNK_SIZE; localX++)
            {
                for (int localY = 0; localY < ChunkStreamingConstants.CHUNK_SIZE; localY++)
                {
                    var cell = new Vector2I(baseCellX + localX, baseCellY + localY);

                    if (layer.GetCellSourceId(cell) != -1)
                    {
                        image.SetCell(cell, new Color(0.4f, 0.4f, 0.45f, 1f));
                    }
                }
            }
        }

        #endregion

        #region Core - Consulta

        public Texture2D GetTexture(string dimensionId, out Vector2I origin)
        {
            if (!_discovered.TryGetValue(dimensionId, out var image))
            {
                origin = Vector2I.Zero;

                return null;
            }

            origin = image.Origin;

            return image.GetTexture();
        }

        #endregion

        #region Core - Reset

        public void Reset()
        {
            foreach (var image in _discovered.Values)
            {
                image.Reset();
            }
        }

        #endregion

        #region Utils

        private DiscoveredMapImage Resolve(string dimensionId)
        {
            if (!_discovered.TryGetValue(dimensionId, out var image))
            {
                image = new DiscoveredMapImage();

                _discovered[dimensionId] = image;
            }

            return image;
        }

        #endregion
    }
}
