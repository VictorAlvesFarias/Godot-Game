using Godot;
using Jogo25D.Biomes;
using Jogo25D.Constants;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jogo25D.Chunks
{
    public static class ChunkGenerator
    {
        #region Core - Generation

        public static void Paint(TerrainLayer target, TerrainLayer borderCapTarget, TerrainLayer baseTarget, long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize)
        {
            var tileSet = target.TileSet;
            var solidCellsByBiome = ResolveSolidCellsByBiome(worldSeed, dimensionId, chunkCoord, chunkSize);

            if (tileSet.GetTerrainSetsCount() > 0)
            {
                var biomeGroups = BuildBiomeGroups(target, solidCellsByBiome, chunkCoord, chunkSize);

                // Conecta TODOS os grupos primeiro, so depois reconecta a fronteira estrangeira
                // de cada um - senao o grupo processado primeiro veria os outros biomas (ainda
                // nao pintados nessa mesma chamada) como vazio, tratando a divisa como
                // precipicio de um lado so.
                foreach (var group in biomeGroups)
                {
                    target.Connect(group.Cells, group.BiomeDef.TerrainSet);
                }

                foreach (var group in biomeGroups)
                {
                    target.ReconnectForeignBorder(group.Cells, group.BiomeDef.TerrainSet);
                }

                if (baseTarget != null)
                {
                    foreach (var group in biomeGroups)
                    {
                        baseTarget.ConnectDependent(target, group.Cells, group.BiomeDef.BaseTerrainSet);
                    }

                    foreach (var group in biomeGroups)
                    {
                        baseTarget.ReconnectForeignBorderDependent(target, group.Cells, group.BiomeDef.BaseTerrainSet);
                    }
                }

                if (borderCapTarget != null)
                {
                    foreach (var group in biomeGroups)
                    {
                        borderCapTarget.ConnectDependent(target, group.Cells, group.BiomeDef.BorderCapTerrainSet);
                    }

                    foreach (var group in biomeGroups)
                    {
                        borderCapTarget.ReconnectForeignBorderDependent(target, group.Cells, group.BiomeDef.BorderCapTerrainSet);
                    }
                }

            }
            else
            {
                var (sourceId, atlasCoord) = GetFallbackTile(tileSet);

                foreach (var cells in solidCellsByBiome.Values)
                {
                    foreach (var cell in cells)
                    {
                        target.SetCell(cell, sourceId, atlasCoord);
                    }
                }
            }
        }

        // Mesma coisa que Paint(), so que cede um frame (await ToSignal ProcessFrame, por dentro
        // de cada Connect/ConnectDependent/ReconnectForeignBorder chamado) a cada "cellsPerFrame"
        // celulas processadas, em vez de pintar o chunk inteiro (ate CHUNK_SIZE^2 celulas x 3
        // camadas) tudo de uma vez num unico frame - e essa a trava que o carregamento de chunk
        // causava. Usado pelo ChunkStreamingManager no lugar de Paint() pra carregar chunk sem
        // travar o jogo.
        public static async Task PaintAsync(TerrainLayer target, TerrainLayer borderCapTarget, TerrainLayer baseTarget, long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize, int cellsPerFrame = 200)
        {
            var tileSet = target.TileSet;
            var solidCellsByBiome = ResolveSolidCellsByBiome(worldSeed, dimensionId, chunkCoord, chunkSize);

            if (tileSet.GetTerrainSetsCount() > 0)
            {
                var biomeGroups = BuildBiomeGroups(target, solidCellsByBiome, chunkCoord, chunkSize);

                foreach (var group in biomeGroups)
                {
                    await target.ConnectAsync(group.Cells, group.BiomeDef.TerrainSet, cellsPerFrame);
                }

                foreach (var group in biomeGroups)
                {
                    await target.ReconnectForeignBorderAsync(group.Cells, group.BiomeDef.TerrainSet, cellsPerFrame);
                }

                if (baseTarget != null)
                {
                    foreach (var group in biomeGroups)
                    {
                        await baseTarget.ConnectDependentAsync(target, group.Cells, group.BiomeDef.BaseTerrainSet, cellsPerFrame);
                    }

                    foreach (var group in biomeGroups)
                    {
                        await baseTarget.ReconnectForeignBorderDependentAsync(target, group.Cells, group.BiomeDef.BaseTerrainSet, cellsPerFrame);
                    }
                }

                if (borderCapTarget != null)
                {
                    foreach (var group in biomeGroups)
                    {
                        await borderCapTarget.ConnectDependentAsync(target, group.Cells, group.BiomeDef.BorderCapTerrainSet, cellsPerFrame);
                    }

                    foreach (var group in biomeGroups)
                    {
                        await borderCapTarget.ReconnectForeignBorderDependentAsync(target, group.Cells, group.BiomeDef.BorderCapTerrainSet, cellsPerFrame);
                    }
                }
            }
            else
            {
                var (sourceId, atlasCoord) = GetFallbackTile(tileSet);

                foreach (var cells in solidCellsByBiome.Values)
                {
                    foreach (var cell in cells)
                    {
                        target.SetCell(cell, sourceId, atlasCoord);
                    }
                }
            }
        }

        // A altura do relevo de cada COLUNA usa um bioma "de referencia" (resolvido no centro
        // vertical do chunk) - mantem o relevo suave, sem degrau quando a fronteira corta a
        // coluna no meio. Ja o bioma de CADA CELULA solida (usado pra escolher a textura) e
        // resolvido individualmente (X e Y), entao perto da fronteira algumas celulas divergem do
        // bioma da coluna, criando a tendrilha organica em vez de uma faixa reta.
        private static Dictionary<BiomeType, List<Vector2I>> ResolveSolidCellsByBiome(long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize)
        {
            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;
            var solidCellsByBiome = new Dictionary<BiomeType, List<Vector2I>>();
            var heightNoiseByBiome = new Dictionary<BiomeType, FastNoiseLite>();

            for (int localX = 0; localX < chunkSize; localX++)
            {
                var worldX = baseCellX + localX;
                var columnBiome = BiomeResolver.Resolve(worldSeed, dimensionId, worldX, baseCellY + chunkSize / 2);
                var columnBiomeDef = BiomeDB.Get(columnBiome);

                if (!heightNoiseByBiome.TryGetValue(columnBiome, out var heightNoise))
                {
                    heightNoise = new FastNoiseLite
                    {
                        Seed = (int)CombineSeed(worldSeed, dimensionId, chunkCoord),
                        Frequency = columnBiomeDef.NoiseFrequency,
                    };
                    heightNoiseByBiome[columnBiome] = heightNoise;
                }

                var groundHeight = columnBiomeDef.HeightOffset + Mathf.RoundToInt(heightNoise.GetNoise1D(worldX) * columnBiomeDef.HeightAmplitude);

                for (int localY = 0; localY < chunkSize; localY++)
                {
                    var worldY = baseCellY + localY;

                    if (worldY < groundHeight)
                    {
                        continue;
                    }

                    var cellBiome = BiomeResolver.Resolve(worldSeed, dimensionId, worldX, worldY);

                    if (!solidCellsByBiome.TryGetValue(cellBiome, out var cells))
                    {
                        cells = new List<Vector2I>();
                        solidCellsByBiome[cellBiome] = cells;
                    }

                    cells.Add(new Vector2I(worldX, worldY));
                }
            }

            return solidCellsByBiome;
        }

        private static List<(BiomeDefinition BiomeDef, List<Vector2I> Cells)> BuildBiomeGroups(TerrainLayer target, Dictionary<BiomeType, List<Vector2I>> solidCellsByBiome, Vector2I chunkCoord, int chunkSize)
        {
            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;
            var biomeGroups = new List<(BiomeDefinition BiomeDef, List<Vector2I> Cells)>();

            foreach (var entry in solidCellsByBiome)
            {
                var biomeDef = BiomeDB.Get(entry.Key);
                var cells = entry.Value;

                AddSolidBorderNeighbors(target, cells, baseCellX, baseCellY, chunkSize, biomeDef.TerrainSet);

                biomeGroups.Add((biomeDef, cells));
            }

            return biomeGroups;
        }

        private static void AddSolidBorderNeighbors(TileMapLayer target, List<Vector2I> solidCells, int baseCellX, int baseCellY, int chunkSize, int terrainSet)
        {
            for (int x = baseCellX - 1; x <= baseCellX + chunkSize; x++)
            {
                AddIfSolid(target, solidCells, new Vector2I(x, baseCellY - 1), terrainSet);
                AddIfSolid(target, solidCells, new Vector2I(x, baseCellY + chunkSize), terrainSet);
            }

            for (int y = baseCellY; y < baseCellY + chunkSize; y++)
            {
                AddIfSolid(target, solidCells, new Vector2I(baseCellX - 1, y), terrainSet);
                AddIfSolid(target, solidCells, new Vector2I(baseCellX + chunkSize, y), terrainSet);
            }
        }

        private static void AddIfSolid(TileMapLayer target, List<Vector2I> solidCells, Vector2I cell, int terrainSet)
        {
            if (target.GetCellSourceId(cell) == -1)
            {
                return;
            }

            var neighborTileData = target.GetCellTileData(cell);

            if (neighborTileData != null && neighborTileData.TerrainSet != terrainSet)
            {
                return;
            }

            solidCells.Add(cell);
        }

        public static void Erase(TileMapLayer target, TileMapLayer borderCapTarget, TileMapLayer baseTarget, Vector2I chunkCoord, int chunkSize)
        {
            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;

            for (int localX = 0; localX < chunkSize; localX++)
            {
                for (int localY = 0; localY < chunkSize; localY++)
                {
                    var cell = new Vector2I(baseCellX + localX, baseCellY + localY);

                    target.SetCell(cell, -1);
                    borderCapTarget?.SetCell(cell, -1);
                    baseTarget?.SetCell(cell, -1);
                }
            }
        }

        // Mesma coisa que Erase(), so que cede um frame a cada "cellsPerFrame" celulas apagadas -
        // descarregar um chunk tambem trava o jogo pelo mesmo motivo que carregar (ate
        // CHUNK_SIZE^2 SetCell(-1) x 3 camadas num frame so).
        public static async Task EraseAsync(TileMapLayer target, TileMapLayer borderCapTarget, TileMapLayer baseTarget, Vector2I chunkCoord, int chunkSize, int cellsPerFrame = 200)
        {
            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;
            var processedSinceYield = 0;

            for (int localX = 0; localX < chunkSize; localX++)
            {
                for (int localY = 0; localY < chunkSize; localY++)
                {
                    var cell = new Vector2I(baseCellX + localX, baseCellY + localY);

                    target.SetCell(cell, -1);
                    borderCapTarget?.SetCell(cell, -1);
                    baseTarget?.SetCell(cell, -1);

                    processedSinceYield++;

                    if (processedSinceYield >= cellsPerFrame)
                    {
                        processedSinceYield = 0;

                        await target.ToSignal(target.GetTree(), SceneTree.SignalName.ProcessFrame);
                    }
                }
            }
        }

        private static (int sourceId, Vector2I atlasCoord) GetFallbackTile(TileSet tileSet)
        {
            for (int i = 0; i < tileSet.GetSourceCount(); i++)
            {
                var sourceId = tileSet.GetSourceId(i);

                if (tileSet.GetSource(sourceId) is TileSetAtlasSource atlasSource && atlasSource.GetTilesCount() > 0)
                {
                    return (sourceId, atlasSource.GetTileId(0));
                }
            }

            return (0, Vector2I.Zero);
        }

        public static TileSet GetTileSet()
        {
            return GD.Load<TileSet>(Textures.Tiles.WORLD_TILE_SET);
        }

        private static long CombineSeed(long worldSeed, string dimensionId, Vector2I chunkCoord)
        {
            unchecked
            {
                long hash = worldSeed;

                hash = hash * 397 ^ StableStringHash(dimensionId);
                hash = hash * 397 ^ chunkCoord.X;
                hash = hash * 397 ^ chunkCoord.Y;

                return hash;
            }
        }

        private static long StableStringHash(string value)
        {
            unchecked
            {
                long hash = 1469598103934665603;

                foreach (var c in value)
                {
                    hash ^= c;
                    hash *= 1099511628211;
                }

                return hash;
            }
        }

        #endregion
    }
}
