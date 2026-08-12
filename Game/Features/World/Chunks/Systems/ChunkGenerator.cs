using Godot;
using Jogo25D.Biomes;
using Jogo25D.Constants;
using Jogo25D.Structures;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jogo25D.Chunks
{
    public static class ChunkGenerator
    {
        // Todo o algoritmo original (altura de arvore, amplitude de relevo, frequencia de ruido,
        // espacamento minimo) foi desenhado e calibrado visualmente com tile_size=32 - continua
        // usando esses mesmos numeros como referencia, so multiplicando (dimensoes em tiles) ou
        // dividindo (frequencia, que e "ciclos por tile") pelo quanto o tile encolheu/cresceu
        // desde entao. Assim o mundo fica proporcional ao PLAYER (cujo tamanho em pixels nao
        // muda) em vez de proporcional ao tile, nao importa o tile_size escolhido no TileSet.
        private const int ReferenceTileSize = 32;

        private static int GetWorldScale(TileSet tileSet)
        {
            var tileSize = tileSet?.TileSize.X ?? ReferenceTileSize;

            return Mathf.Max(1, Mathf.RoundToInt(ReferenceTileSize / (float)tileSize));
        }

        #region Core - Generation

        public static void Paint(TerrainLayer target, TerrainLayer borderCapTarget, TerrainLayer baseTarget, long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize)
        {
            var tileSet = target.TileSet;
            var worldScale = GetWorldScale(tileSet);
            var (solidCellsByBiome, columnSurfaces) = ResolveSolidCellsByBiome(worldSeed, dimensionId, chunkCoord, chunkSize, worldScale);

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
                    target.ReconnectForeignBorder(group.Cells, group.BiomeDef.TerrainSet, StructureDB.AllTerrainSets);
                }

                if (baseTarget != null)
                {
                    foreach (var group in biomeGroups)
                    {
                        baseTarget.ConnectDependent(target, group.Cells, group.BiomeDef.BaseTerrainSet);
                    }

                    foreach (var group in biomeGroups)
                    {
                        baseTarget.ReconnectForeignBorderDependent(target, group.Cells, group.BiomeDef.BaseTerrainSet, StructureDB.AllTerrainSets);
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
                        borderCapTarget.ReconnectForeignBorderDependent(target, group.Cells, group.BiomeDef.BorderCapTerrainSet, StructureDB.AllTerrainSets);
                    }
                }

                PlaceStructures(baseTarget, columnSurfaces, worldSeed, dimensionId, chunkCoord, chunkSize, worldScale);
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
            var worldScale = GetWorldScale(tileSet);
            var (solidCellsByBiome, columnSurfaces) = ResolveSolidCellsByBiome(worldSeed, dimensionId, chunkCoord, chunkSize, worldScale);

            if (tileSet.GetTerrainSetsCount() > 0)
            {
                var biomeGroups = BuildBiomeGroups(target, solidCellsByBiome, chunkCoord, chunkSize);

                foreach (var group in biomeGroups)
                {
                    await target.ConnectAsync(group.Cells, group.BiomeDef.TerrainSet, cellsPerFrame);
                }

                foreach (var group in biomeGroups)
                {
                    await target.ReconnectForeignBorderAsync(group.Cells, group.BiomeDef.TerrainSet, cellsPerFrame, StructureDB.AllTerrainSets);
                }

                if (baseTarget != null)
                {
                    foreach (var group in biomeGroups)
                    {
                        await baseTarget.ConnectDependentAsync(target, group.Cells, group.BiomeDef.BaseTerrainSet, cellsPerFrame);
                    }

                    foreach (var group in biomeGroups)
                    {
                        await baseTarget.ReconnectForeignBorderDependentAsync(target, group.Cells, group.BiomeDef.BaseTerrainSet, cellsPerFrame, StructureDB.AllTerrainSets);
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
                        await borderCapTarget.ReconnectForeignBorderDependentAsync(target, group.Cells, group.BiomeDef.BorderCapTerrainSet, cellsPerFrame, StructureDB.AllTerrainSets);
                    }
                }

                PlaceStructures(baseTarget, columnSurfaces, worldSeed, dimensionId, chunkCoord, chunkSize, worldScale);
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
        private readonly struct ColumnSurface
        {
            public readonly int WorldX;
            public readonly int GroundHeight;
            public readonly BiomeType Biome;

            public ColumnSurface(int worldX, int groundHeight, BiomeType biome)
            {
                WorldX = worldX;
                GroundHeight = groundHeight;
                Biome = biome;
            }
        }

        private static (Dictionary<BiomeType, List<Vector2I>> SolidCellsByBiome, List<ColumnSurface> ColumnSurfaces) ResolveSolidCellsByBiome(long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize, int worldScale)
        {
            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;
            var solidCellsByBiome = new Dictionary<BiomeType, List<Vector2I>>();
            var columnSurfaces = new List<ColumnSurface>();
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
                        // Frequencia e "ciclos por tile" - com o tile menor, um mesmo passo de 1
                        // tile percorre MENOS distancia em pixel, entao teria que andar mais
                        // tiles pra completar o mesmo ciclo (mesma paisagem em pixels) que o
                        // algoritmo original desenhava a tile_size=32.
                        Frequency = columnBiomeDef.NoiseFrequency / worldScale,
                    };
                    heightNoiseByBiome[columnBiome] = heightNoise;
                }

                // Amplitude/offset sao alturas em TILES - escala pra cima junto com o encolhimento
                // do tile, senao o relevo (em pixels) fica achatado pela metade em vez de manter
                // a mesma proporcao com o player que tinha no algoritmo original.
                var groundHeight = columnBiomeDef.HeightOffset * worldScale + Mathf.RoundToInt(heightNoise.GetNoise1D(worldX) * columnBiomeDef.HeightAmplitude * worldScale);

                columnSurfaces.Add(new ColumnSurface(worldX, groundHeight, columnBiome));

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

            return (solidCellsByBiome, columnSurfaces);
        }

        // Decoracao procedural (arvore, e qualquer estrutura futura registrada no StructureDB e
        // listada em BiomeDefinition.StructureIds) - pintada na layer Base, que nao compete
        // visualmente com nada, ja que as celulas de estrutura ficam todas no ar acima do chao
        // (Base so tem chao onde Texture/Bordercap tambem tem, entao fica vazia exatamente onde
        // a estrutura ocupa). Passa pelo mesmo mediator (Connect) usado pro resto do tileset,
        // entao ganha bordas/cantos organicos em vez de bloco solido. Cada instancia fica inteira
        // dentro dos limites locais do proprio chunk pra nao depender de chunks vizinhos ainda
        // nao carregados nem sumir pela metade quando um vizinho descarrega.
        private static void PlaceStructures(TerrainLayer baseTarget, List<ColumnSurface> columnSurfaces, long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize, int worldScale)
        {
            if (baseTarget == null)
            {
                return;
            }

            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;
            var cellsByTerrainSet = new Dictionary<int, List<Vector2I>>();

            // Cursor de espacamento POR ESTRUTURA - distancia minima entre caixas de estrutura.
            var lastRightEdgeByStructure = new Dictionary<string, int>();
            var minBoundsGapTiles = MinStructureBoundsGapTiles;

            foreach (var column in columnSurfaces)
            {
                var biomeDef = BiomeDB.Get(column.Biome);

                if (biomeDef.StructureIds == null || biomeDef.StructureIds.Count == 0)
                {
                    continue;
                }

                var localX = column.WorldX - baseCellX;
                var localSurfaceY = column.GroundHeight - baseCellY;

                if (localSurfaceY < 0 || localSurfaceY >= chunkSize)
                {
                    continue;
                }

                foreach (var structureId in biomeDef.StructureIds)
                {
                    var structure = StructureDB.Get(structureId);

                    if (structure == null || structure.Chance <= 0f)
                    {
                        continue;
                    }

                    if (!lastRightEdgeByStructure.ContainsKey(structureId))
                    {
                        var spanLookback = Mathf.Max(MaxStructureSpacingLookback, structure.GetMaxRightExtent(worldScale));

                        lastRightEdgeByStructure[structureId] = ResolveLastRightEdgeBefore(
                            structure,
                            worldSeed,
                            dimensionId,
                            baseCellX,
                            spanLookback,
                            minBoundsGapTiles,
                            worldScale);
                    }

                    if (WorldRandom.StructureRandom01(worldSeed, dimensionId, structureId, column.WorldX, 0) >= structure.Chance)
                    {
                        continue;
                    }

                    var bounds = structure.GetBounds(worldSeed, dimensionId, column.WorldX, worldScale);
                    var candidateLeftX = column.WorldX - bounds.Left;
                    var hasPreviousRightEdge = lastRightEdgeByStructure.TryGetValue(structureId, out var lastRightEdge) && lastRightEdge != int.MinValue;

                    if (hasPreviousRightEdge && candidateLeftX <= lastRightEdge + minBoundsGapTiles)
                    {
                        continue;
                    }

                    var groups = structure.CollectCells(new Vector2I(column.WorldX, column.GroundHeight), worldSeed, dimensionId, worldScale);

                    foreach (var group in groups)
                    {
                        if (!cellsByTerrainSet.TryGetValue(group.TerrainSet, out var cells))
                        {
                            cells = new List<Vector2I>();
                            cellsByTerrainSet[group.TerrainSet] = cells;
                        }

                        cells.AddRange(group.Cells);
                    }

                    if (baseTarget != null)
                    {
                        var overlayText = structureId == "tree" ? column.WorldX.ToString() : $"{structureId}:{column.WorldX}";
                        baseTarget.AddDebugOverlayAnnotation(new Vector2I(column.WorldX, column.GroundHeight), overlayText, Colors.White);
                    }

                    lastRightEdgeByStructure[structureId] = column.WorldX + bounds.Right;
                }
            }

            foreach (var entry in cellsByTerrainSet)
            {
                baseTarget.Connect(entry.Value, entry.Key);
            }
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

        // Folga minima entre caixas delimitadoras de duas instancias da mesma estrutura.
            // Com valor 1, garante ao menos um bloco vazio entre a borda direita da arvore
            // anterior e a borda esquerda da proxima.
            private const int MinStructureBoundsGapTiles = 1;

            // Limite conservador de quanto olhar pra tras ao retomar o cursor de espacamento entre
            // chunks. O tamanho do lookback precisa ser maior ou igual ao maior alcance horizontal
            // de uma estrutura, para nao perder uma arvore anterior cujo bloco direito ainda entra
            // no chunk atual.
            private const int MaxStructureSpacingLookback = 32;
        private static int ResolveLastRightEdgeBefore(
            StructureDefinition structure,
            long worldSeed,
            string dimensionId,
            int chunkStartX,
            int lookbackTiles,
            int minBoundsGapTiles,
            int worldScale)
        {
            var scanStart = chunkStartX - lookbackTiles;
            var lastRightEdge = int.MinValue;

            for (int worldX = scanStart; worldX < chunkStartX; worldX++)
            {
                if (WorldRandom.StructureRandom01(worldSeed, dimensionId, structure.Id, worldX, 0) >= structure.Chance)
                {
                    continue;
                }

                var bounds = structure.GetBounds(worldSeed, dimensionId, worldX, worldScale);
                var candidateLeftX = worldX - bounds.Left;

                if (lastRightEdge != int.MinValue && candidateLeftX <= lastRightEdge + minBoundsGapTiles)
                {
                    continue;
                }

                lastRightEdge = worldX + bounds.Right;
            }

            return lastRightEdge;
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

            if (baseTarget is TerrainLayer terrainBaseLayer)
            {
                terrainBaseLayer.RemoveDebugOverlayAnnotationsInRegion(
                    new Vector2I(baseCellX, baseCellY),
                    new Vector2I(baseCellX + chunkSize - 1, baseCellY + chunkSize - 1));
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

                hash = hash * 397 ^ WorldRandom.StableStringHash(dimensionId);
                hash = hash * 397 ^ chunkCoord.X;
                hash = hash * 397 ^ chunkCoord.Y;

                return hash;
            }
        }

        #endregion
    }
}
