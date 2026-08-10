using Godot;
using Jogo25D.Biomes;
using Jogo25D.Constants;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jogo25D.Chunks
{
    public static class ChunkGenerator
    {
        // Passado pra ReconnectForeignBorder(Dependent) do bioma (ground/bordercap/base) pra
        // essas passadas nunca tratarem uma celula de arvore vizinha como "vizinho estrangeiro
        // do bioma" - sem isso, uma arvore encostada na borda de um chunk era detectada como tal
        // pelo chunk vizinho ao carregar, reconectada via ConnectDependent (que depende da
        // Texture ter uma celula ali) e apagada, porque tronco/copa nao espelham nas 3 camadas
        // do jeito que bioma espelha.
        private static readonly HashSet<int> TreeTerrainSets = new() { TreeWoodTerrainSet, TreeLeafTerrainSet };

        #region Core - Generation

        public static void Paint(TerrainLayer target, TerrainLayer borderCapTarget, TerrainLayer baseTarget, long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize)
        {
            var tileSet = target.TileSet;
            var (solidCellsByBiome, columnSurfaces) = ResolveSolidCellsByBiome(worldSeed, dimensionId, chunkCoord, chunkSize);

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
                    target.ReconnectForeignBorder(group.Cells, group.BiomeDef.TerrainSet, TreeTerrainSets);
                }

                if (baseTarget != null)
                {
                    foreach (var group in biomeGroups)
                    {
                        baseTarget.ConnectDependent(target, group.Cells, group.BiomeDef.BaseTerrainSet);
                    }

                    foreach (var group in biomeGroups)
                    {
                        baseTarget.ReconnectForeignBorderDependent(target, group.Cells, group.BiomeDef.BaseTerrainSet, TreeTerrainSets);
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
                        borderCapTarget.ReconnectForeignBorderDependent(target, group.Cells, group.BiomeDef.BorderCapTerrainSet, TreeTerrainSets);
                    }
                }

                PlaceTrees(baseTarget, columnSurfaces, worldSeed, dimensionId, chunkCoord, chunkSize);
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
            var (solidCellsByBiome, columnSurfaces) = ResolveSolidCellsByBiome(worldSeed, dimensionId, chunkCoord, chunkSize);

            if (tileSet.GetTerrainSetsCount() > 0)
            {
                var biomeGroups = BuildBiomeGroups(target, solidCellsByBiome, chunkCoord, chunkSize);

                foreach (var group in biomeGroups)
                {
                    await target.ConnectAsync(group.Cells, group.BiomeDef.TerrainSet, cellsPerFrame);
                }

                foreach (var group in biomeGroups)
                {
                    await target.ReconnectForeignBorderAsync(group.Cells, group.BiomeDef.TerrainSet, cellsPerFrame, TreeTerrainSets);
                }

                if (baseTarget != null)
                {
                    foreach (var group in biomeGroups)
                    {
                        await baseTarget.ConnectDependentAsync(target, group.Cells, group.BiomeDef.BaseTerrainSet, cellsPerFrame);
                    }

                    foreach (var group in biomeGroups)
                    {
                        await baseTarget.ReconnectForeignBorderDependentAsync(target, group.Cells, group.BiomeDef.BaseTerrainSet, cellsPerFrame, TreeTerrainSets);
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
                        await borderCapTarget.ReconnectForeignBorderDependentAsync(target, group.Cells, group.BiomeDef.BorderCapTerrainSet, cellsPerFrame, TreeTerrainSets);
                    }
                }

                PlaceTrees(baseTarget, columnSurfaces, worldSeed, dimensionId, chunkCoord, chunkSize);
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

        private static (Dictionary<BiomeType, List<Vector2I>> SolidCellsByBiome, List<ColumnSurface> ColumnSurfaces) ResolveSolidCellsByBiome(long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize)
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
                        Frequency = columnBiomeDef.NoiseFrequency,
                    };
                    heightNoiseByBiome[columnBiome] = heightNoise;
                }

                var groundHeight = columnBiomeDef.HeightOffset + Mathf.RoundToInt(heightNoise.GetNoise1D(worldX) * columnBiomeDef.HeightAmplitude);

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

        // Arvore modelada a partir de uma arvore desenhada a mao no editor (Upsidedown.tscn):
        // Tronco (terrain_set 6 = Tree Wood) e copa (terrain_set 7 = Tree Leaf) vao os dois na
        // layer Base - nao competem visualmente com nada, ja que as celulas da arvore ficam
        // todas no ar acima do chao (Base so tem chao onde Texture/Bordercap tambem tem, entao
        // fica vazia exatamente onde a arvore cresce). Os dois passam pelo mediator (Connect)
        // usando o mesmo mapeamento blob47 do resto do tileset, entao ganham bordas/cantos
        // organicos em vez de bloco solido. A copa e o tronco se conectam entre si por padrao
        // (mesma layer, sem bloqueio configurado) - o layer pode ter regras de bloqueio contra
        // o proprio chao da Base (terrain_set 4/5) pra nao "derreterem" na base do bioma.
        // Cada arvore fica inteira dentro dos limites locais do proprio chunk pra nao depender
        // de chunks vizinhos ainda nao carregados nem sumir pela metade quando um vizinho
        // descarrega.
        public const int TreeWoodTerrainSet = 6;
        public const int TreeLeafTerrainSet = 7;

        // As 4 arvores desenhadas a mao no editor (achadas na layer Bordercap, resquicio de
        // antes da arvore mudar pra Base - trunk em x=-37/-33/-27/-22 do Upsidedown.tscn) viraram
        // 4 MODELOS FIXOS em vez de intervalos aleatorios - cada arvore gerada usa a altura de
        // tronco e o formato de copa EXATOS de um dos 4 exemplos, sem variar. CanopyRadii vai de
        // BAIXO (linha 0, encostada no tronco) pra CIMA (ultima linha, topo da copa) - cada valor
        // e o raio lateral daquela linha (largura = 2*raio+1), sempre centrado no tronco (X=0).
        private readonly struct TreeTemplate
        {
            public readonly int TrunkHeight;
            public readonly int[] CanopyRadii;

            public TreeTemplate(int trunkHeight, int[] canopyRadii)
            {
                TrunkHeight = trunkHeight;
                CanopyRadii = canopyRadii;
            }
        }

        private static readonly TreeTemplate[] TreeTemplates =
        {
            // Arvore 1 (x=-37 no editor): tronco 6, copa 1->3->5->7->7 (topo->base).
            new TreeTemplate(6, new[] { 3, 3, 2, 1, 0 }),
            // Arvore 2 (x=-33 no editor): tronco 2, copa 1->3->3 (topo->base).
            new TreeTemplate(2, new[] { 1, 1, 0 }),
            // Arvore 3 (x=-27 no editor): tronco 8, copa 1->3->5->5->3 (topo->base) - afunila
            // nos dois extremos, copa arredondada.
            new TreeTemplate(8, new[] { 1, 2, 2, 1, 0 }),
            // Arvore 4 (x=-22 no editor): tronco 3, copa 1->3->5->5 (topo->base).
            new TreeTemplate(3, new[] { 2, 2, 1, 0 }),
        };

        private static void PlaceTrees(TerrainLayer baseTarget, List<ColumnSurface> columnSurfaces, long worldSeed, string dimensionId, Vector2I chunkCoord, int chunkSize)
        {
            if (baseTarget == null)
            {
                return;
            }

            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;
            var lastTreeRightEdge = int.MinValue;
            var trunkCells = new List<Vector2I>();
            var canopyCells = new List<Vector2I>();

            foreach (var column in columnSurfaces)
            {
                var biomeDef = BiomeDB.Get(column.Biome);

                if (biomeDef.TreeChance <= 0f)
                {
                    continue;
                }

                var localX = column.WorldX - baseCellX;
                var localSurfaceY = column.GroundHeight - baseCellY;

                if (localSurfaceY < 0 || localSurfaceY >= chunkSize)
                {
                    continue;
                }

                if (ColumnRandom01(worldSeed, dimensionId, column.WorldX, 0) >= biomeDef.TreeChance)
                {
                    continue;
                }

                // Sorteia entre os 4 modelos com a MESMA chance cada (25%) - indice 0..3 direto,
                // sem pesos.
                var templateIndex = ColumnRandomInt(worldSeed, dimensionId, column.WorldX, 1, (0, TreeTemplates.Length - 1));
                var template = TreeTemplates[templateIndex];
                var maxRadius = template.CanopyRadii.Length == 0 ? 0 : template.CanopyRadii[0];

                var treeHeight = template.TrunkHeight + template.CanopyRadii.Length;
                var leftEdge = column.WorldX - maxRadius;

                // Garante pelo menos 1 celula vazia de folga entre a copa dessa arvore e a borda
                // direita da ultima arvore plantada.
                if (localX < maxRadius || localX > chunkSize - 1 - maxRadius
                    || localSurfaceY - treeHeight < 0
                    || leftEdge <= lastTreeRightEdge + 1)
                {
                    continue;
                }

                CollectTreeCells(new Vector2I(column.WorldX, column.GroundHeight), template, trunkCells, canopyCells);

                lastTreeRightEdge = column.WorldX + maxRadius;
            }

            if (trunkCells.Count == 0)
            {
                return;
            }

            baseTarget.Connect(trunkCells, TreeWoodTerrainSet);
            baseTarget.Connect(canopyCells, TreeLeafTerrainSet);
        }

        private static void CollectTreeCells(Vector2I groundCell, TreeTemplate template, List<Vector2I> trunkCells, List<Vector2I> canopyCells)
        {
            for (int trunkStep = 1; trunkStep <= template.TrunkHeight; trunkStep++)
            {
                trunkCells.Add(groundCell + new Vector2I(0, -trunkStep));
            }

            for (int row = 0; row < template.CanopyRadii.Length; row++)
            {
                var offsetY = -template.TrunkHeight - row;
                var radius = template.CanopyRadii[row];

                for (int canopyX = -radius; canopyX <= radius; canopyX++)
                {
                    canopyCells.Add(groundCell + new Vector2I(canopyX, offsetY));
                }
            }
        }

        private static float ColumnRandom01(long worldSeed, string dimensionId, int worldX, int salt)
        {
            unchecked
            {
                long hash = worldSeed;

                hash = hash * 397 ^ StableStringHash(dimensionId);
                hash = hash * 397 ^ worldX;
                hash = hash * 397 ^ salt;
                hash = hash * 397 ^ 0x5EED5EEDL;

                return (hash & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        private static int ColumnRandomInt(long worldSeed, string dimensionId, int worldX, int salt, (int Min, int Max) range)
        {
            var span = range.Max - range.Min + 1;

            return range.Min + Mathf.Min(span - 1, (int)(ColumnRandom01(worldSeed, dimensionId, worldX, salt) * span));
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
