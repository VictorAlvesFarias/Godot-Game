using Godot;
using Jogo25D.Biomes;
using Jogo25D.Blocks;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using Jogo25D.Save.Structures;
using Jogo25D.Save.Types;
using Jogo25D.Utils.Coordinates;
using System;
using System.Collections.Generic;

namespace Jogo25D.Dimensions
{
    // Cache do terreno resolvido da dimensao: geracao pela seed (strips e tiles descartaveis) + projecao das mutacoes
    // da Dimension (edicoes e walls, permanentes). Nao vai para o save; e remontado a partir da seed e das mutacoes.
    public sealed class TerrainCache
    {
        #region Constructors

        public TerrainCache(long seed, string dimensionId, int worldScale, bool procedural = true)
        {
            Seed = seed;
            DimensionId = dimensionId;
            WorldScale = worldScale;
            Procedural = procedural;

            var bound = _generator.GetSurfaceBound(worldScale);

            TerrainBottom = (CoordinateUtilities.CellToChunk(bound) + 1) * ChunkStreamingConstants.CHUNK_SIZE;
            SkyTop = _generator.GetSkyTop(worldScale);
        }

        #endregion

        #region Dinamic properties

        private const int ProceduralInterior = int.MaxValue;
        private const int StripCacheLimit = 128;
        private const int InteriorTileCacheLimit = 512;

        public long Seed { get; }
        public string DimensionId { get; }
        public int WorldScale { get; }
        public int TerrainBottom { get; }
        public int SkyTop { get; }
        public bool Procedural { get; }
        public long Revision { get; private set; }
        public long BackgroundRevision { get; private set; }

        public int OpticalSkyTop => Math.Min(SkyTop, _highestEdit);

        private readonly ChunkGeneratorSystem _generator = new();
        private readonly Dictionary<int, Strip> _strips = new();
        private readonly Dictionary<Vector2I, InteriorTile> _tiles = new();
        private readonly Dictionary<int, SortedDictionary<int, int>> _edits = new();
        private readonly SortedDictionary<int, int> _placedHeights = new();
        private readonly HashSet<Vector2I> _background = new();
        private readonly HashSet<Vector2I> _removedBackground = new();
        private readonly Dictionary<Vector2I, ChunkEdits> _chunkEdits = new();

        // Chunks cujas edicoes ja vieram do banco (ou do servidor) e estao na memoria.
        private readonly HashSet<Vector2I> _loadedChunks = new();

        private long _clock;
        private int _highestEdit = int.MaxValue;

        #endregion

        #region Core - Consulta

        public static int TileTerrain(TileMapLayer layer, Vector2I cell)
        {
            var terrain = layer.GetCellTileData(cell)?.TerrainSet ?? -1;

            return terrain >= 0 ? terrain : layer.GetCellSourceId(cell);
        }

        public static byte TerrainOpacity(int terrain)
        {
            return terrain < 0 ? (byte)0 : (byte)255;
        }

        // Runs da coluna x dentro de [yFrom, yTo), ja com as edicoes aplicadas.
        public IReadOnlyList<Run> OpticalColumn(int x, int yFrom, int yTo)
        {
            var runs = BaseRuns(x, yFrom, yTo);

            return _edits.TryGetValue(x, out var edits) ? MergeEdits(runs, edits, yFrom, yTo) : runs;
        }

        // Menor y solido da coluna a partir de OpticalSkyTop, procurando ate limit (exclusivo); limit quando nao ha solido.
        public int FirstSolid(int x, int limit)
        {
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var from = OpticalSkyTop;

            while (from < limit)
            {
                var to = from < TerrainBottom
                    ? Math.Min(limit, TerrainBottom)
                    : Math.Min(limit, (CoordinateUtilities.CellToChunk(from) + 1) * chunkSize);
                var runs = OpticalColumn(x, from, to);

                if (runs.Count > 0)
                {
                    return runs[0].Start;
                }

                from = to;
            }

            return limit;
        }

        public int Terrain(int x, int y)
        {
            var material = Material(x, y);

            if (material != ProceduralInterior)
            {
                return material;
            }

            return BiomeDB.Get(_generator.GetBiomeIdAtPosition(Seed, DimensionId, x, y)).TerrainSet;
        }

        public byte Opacity(int x, int y)
        {
            return TerrainOpacity(Material(x, y));
        }

        public bool HasBackground(int x, int y)
        {
            var cell = new Vector2I(x, y);

            return _background.Contains(cell) || (!_removedBackground.Contains(cell) && HasProceduralBackground(x, y));
        }

        // Parede de fundo gerada: todo o terreno abaixo do chao natural tem fundo, como no mundo autoral,
        // inclusive onde as cavernas abriram o terreno.
        public bool HasProceduralBackground(int x, int y)
        {
            if (!Procedural)
            {
                return false;
            }

            if (y >= TerrainBottom)
            {
                return true;
            }

            BaseColumn(x);

            return _strips[CoordinateUtilities.CellToChunk(x)].WallTops.TryGetValue(new Vector2I(x, CoordinateUtilities.CellToChunk(y)), out var top) && y >= top;
        }

        private int Material(int x, int y)
        {
            if (_edits.TryGetValue(x, out var edits) && edits.TryGetValue(y, out var edited))
            {
                return edited;
            }

            if (y >= TerrainBottom)
            {
                if (!Procedural)
                {
                    return -1;
                }

                var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
                var tileCoord = new Vector2I(CoordinateUtilities.CellToChunk(x), CoordinateUtilities.CellToChunk(y));
                var tile = Tile(tileCoord);
                var local = (y - tileCoord.Y * chunkSize) * chunkSize + x - tileCoord.X * chunkSize;

                return tile.Open[local] ? -1 : ProceduralInterior;
            }

            foreach (var run in BaseColumn(x))
            {
                if (y < run.Start)
                {
                    break;
                }

                if (y < run.End)
                {
                    return run.Terrain;
                }
            }

            return -1;
        }

        #endregion

        #region Core - Mutacao

        // Edicao do jogador: entra no chunk (que fica sujo, para ir ao banco) e nos indices de consulta.
        // blockId nulo = removido (bloco quebrado / parede quebrada).
        public void ApplyEdit(int x, int y, TerrainLayerType layer, string blockId)
        {
            var cell = new Vector2I(x, y);
            var edits = EditsOf(CoordinateUtilities.CellToChunk(cell), create: true);

            if (layer == TerrainLayerType.Wall)
            {
                edits.Walls[cell] = blockId;

                SetWallIndex(cell, blockId != null);
            }
            else
            {
                edits.Terrain[cell] = blockId;

                SetTerrain(x, y, TerrainOf(blockId));
            }

            edits.Dirty = true;
        }

        // Edicoes de um chunk vindas do banco (ou do servidor). Substitui o que o cache tinha dele.
        public void LoadChunkEdits(Vector2I chunk, IEnumerable<CellEdit> cells)
        {
            UnloadChunkEdits(chunk);

            _loadedChunks.Add(chunk);

            ChunkEdits edits = null;

            foreach (var cell in cells)
            {
                var position = new Vector2I(cell.X, cell.Y);

                edits ??= EditsOf(chunk, create: true);

                if (cell.Layer == TerrainLayerType.Wall)
                {
                    edits.Walls[position] = cell.BlockId;

                    SetWallIndex(position, cell.BlockId != null);
                }
                else
                {
                    edits.Terrain[position] = cell.BlockId;

                    SetTerrain(cell.X, cell.Y, TerrainOf(cell.BlockId));
                }
            }
        }

        // Solta as edicoes do chunk da memoria; o terreno dele volta a ser o gerado.
        public void UnloadChunkEdits(Vector2I chunk)
        {
            _loadedChunks.Remove(chunk);

            if (!_chunkEdits.Remove(chunk, out var edits))
            {
                return;
            }

            foreach (var cell in edits.Terrain.Keys)
            {
                ClearTerrain(cell.X, cell.Y);
            }

            foreach (var cell in edits.Walls.Keys)
            {
                ClearWallIndex(cell);
            }
        }

        public bool HasChunkEdits(Vector2I chunk)
        {
            return _chunkEdits.ContainsKey(chunk);
        }

        public bool IsDirty(Vector2I chunk)
        {
            return _chunkEdits.TryGetValue(chunk, out var edits) && edits.Dirty;
        }

        public bool IsChunkLoaded(Vector2I chunk)
        {
            return _loadedChunks.Contains(chunk);
        }

        public IEnumerable<Vector2I> LoadedChunks => _loadedChunks;

        // Chunks com edicoes na memoria, vindas do banco ou feitas agora.
        public IEnumerable<Vector2I> EditedChunks => _chunkEdits.Keys;
        public int EditedChunkCount => _chunkEdits.Count;

        public IEnumerable<Vector2I> DirtyChunks()
        {
            foreach (var (chunk, edits) in _chunkEdits)
            {
                if (edits.Dirty)
                {
                    yield return chunk;
                }
            }
        }

        public List<CellEdit> CellEditsOf(Vector2I chunk)
        {
            var result = new List<CellEdit>();

            if (!_chunkEdits.TryGetValue(chunk, out var edits))
            {
                return result;
            }

            foreach (var (cell, blockId) in edits.Terrain)
            {
                result.Add(new CellEdit(cell.X, cell.Y, TerrainLayerType.Terrain, blockId));
            }

            foreach (var (cell, blockId) in edits.Walls)
            {
                result.Add(new CellEdit(cell.X, cell.Y, TerrainLayerType.Wall, blockId));
            }

            return result;
        }

        // Parede colocada pelo jogador na celula (id do BlockDB), ou nulo.
        public string PlacedWallAt(Vector2I cell)
        {
            return _chunkEdits.TryGetValue(CoordinateUtilities.CellToChunk(cell), out var edits) && edits.Walls.TryGetValue(cell, out var blockId)
                ? blockId
                : null;
        }

        public void MarkClean(Vector2I chunk)
        {
            if (_chunkEdits.TryGetValue(chunk, out var edits))
            {
                edits.Dirty = false;
            }
        }

        // Parede de fundo que nao e edicao do jogador (paredes autorais da cena).
        public void SetBackground(int x, int y, bool present)
        {
            SetWallIndex(new Vector2I(x, y), present);
        }

        // Terreno que nao e edicao do jogador (mundo autoral). Para edicoes, use ApplyEdit.
        public void SetTerrain(int x, int y, int terrain)
        {
            if (!_edits.TryGetValue(x, out var column))
            {
                _edits[x] = column = new SortedDictionary<int, int>();
            }

            var hadPrevious = column.TryGetValue(y, out var previous);

            if (hadPrevious && previous == terrain)
            {
                return;
            }

            if (hadPrevious && previous >= 0 && --_placedHeights[y] == 0)
            {
                _placedHeights.Remove(y);
            }

            column[y] = terrain;

            if (terrain >= 0)
            {
                _placedHeights[y] = _placedHeights.GetValueOrDefault(y) + 1;
            }

            RefreshHighestEdit();

            Revision++;
        }

        private ChunkEdits EditsOf(Vector2I chunk, bool create)
        {
            if (!_chunkEdits.TryGetValue(chunk, out var edits) && create)
            {
                _chunkEdits[chunk] = edits = new ChunkEdits();
            }

            return edits;
        }

        private static int TerrainOf(string blockId)
        {
            return blockId != null && BlockDB.TryGet(blockId, out var block) ? block.TerrainSet ?? block.SourceId : -1;
        }

        private void ClearTerrain(int x, int y)
        {
            if (!_edits.TryGetValue(x, out var column) || !column.Remove(y, out var previous))
            {
                return;
            }

            if (column.Count == 0)
            {
                _edits.Remove(x);
            }

            if (previous >= 0 && --_placedHeights[y] == 0)
            {
                _placedHeights.Remove(y);
            }

            RefreshHighestEdit();

            Revision++;
        }

        private void RefreshHighestEdit()
        {
            _highestEdit = int.MaxValue;

            foreach (var height in _placedHeights.Keys)
            {
                _highestEdit = height;

                break;
            }
        }

        private void SetWallIndex(Vector2I cell, bool present)
        {
            var changed = present
                ? _background.Add(cell) | _removedBackground.Remove(cell)
                : _background.Remove(cell) | (HasProceduralBackground(cell.X, cell.Y) && _removedBackground.Add(cell));

            if (changed)
            {
                BackgroundRevision++;
            }
        }

        private void ClearWallIndex(Vector2I cell)
        {
            if (_background.Remove(cell) | _removedBackground.Remove(cell))
            {
                BackgroundRevision++;
            }
        }

        #endregion

        #region Core - Colunas

        private static void Append(List<Run> runs, int start, int end, int terrain)
        {
            if (start >= end || terrain < 0)
            {
                return;
            }

            if (runs.Count > 0 && runs[^1].End == start && runs[^1].Terrain == terrain)
            {
                runs[^1] = runs[^1] with
                {
                    End = end
                };
            }
            else
            {
                runs.Add(new Run(start, end, terrain));
            }
        }

        private List<Run> BaseColumn(int x)
        {
            var key = CoordinateUtilities.CellToChunk(x);

            if (!_strips.TryGetValue(key, out var strip))
            {
                strip = BuildStrip(key);

                if (_strips.Count >= StripCacheLimit)
                {
                    DropOldestStrip();
                }

                _strips[key] = strip;
            }

            strip.LastUse = ++_clock;

            return strip.Columns[x];
        }

        private Strip BuildStrip(int key)
        {
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var wallTops = new Dictionary<Vector2I, int>();
            var cells = Procedural
                ? _generator.GenerateLogicalStrip(Seed, DimensionId, key, WorldScale, out wallTops)
                : new Dictionary<Vector2I, int>();
            var strip = new Strip(wallTops);

            for (var cx = key * chunkSize; cx < key * chunkSize + chunkSize; cx++)
            {
                var column = new SortedDictionary<int, int>();

                foreach (var pair in cells)
                {
                    if (pair.Key.X == cx)
                    {
                        column[pair.Key.Y] = pair.Value;
                    }
                }

                var runs = new List<Run>();

                foreach (var pair in column)
                {
                    Append(runs, pair.Key, pair.Key + 1, pair.Value);
                }

                strip.Columns[cx] = runs;
            }

            return strip;
        }

        private void DropOldestStrip()
        {
            var oldestKey = 0;
            var oldest = long.MaxValue;

            foreach (var pair in _strips)
            {
                if (pair.Value.LastUse < oldest)
                {
                    oldest = pair.Value.LastUse;
                    oldestKey = pair.Key;
                }
            }

            _strips.Remove(oldestKey);
        }

        private InteriorTile Tile(Vector2I tileCoord)
        {
            if (!_tiles.TryGetValue(tileCoord, out var tile))
            {
                tile = BuildTile(tileCoord);

                if (_tiles.Count >= InteriorTileCacheLimit)
                {
                    DropOldestTile();
                }

                _tiles[tileCoord] = tile;
            }

            tile.LastUse = ++_clock;

            return tile;
        }

        private InteriorTile BuildTile(Vector2I tileCoord)
        {
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var tile = new InteriorTile(_generator.ResolveInteriorOpenCells(Seed, DimensionId, tileCoord, WorldScale), chunkSize);
            var top = tileCoord.Y * chunkSize;

            for (var localX = 0; localX < chunkSize; localX++)
            {
                var runs = tile.Columns[localX];

                for (var localY = 0; localY < chunkSize; localY++)
                {
                    if (!tile.Open[localY * chunkSize + localX])
                    {
                        Append(runs, top + localY, top + localY + 1, ProceduralInterior);
                    }
                }
            }

            return tile;
        }

        private void DropOldestTile()
        {
            var oldestKey = Vector2I.Zero;
            var oldest = long.MaxValue;

            foreach (var pair in _tiles)
            {
                if (pair.Value.LastUse < oldest)
                {
                    oldest = pair.Value.LastUse;
                    oldestKey = pair.Key;
                }
            }

            _tiles.Remove(oldestKey);
        }

        private List<Run> BaseRuns(int x, int yFrom, int yTo)
        {
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var result = new List<Run>();

            if (yFrom < TerrainBottom)
            {
                var stripTo = Math.Min(yTo, TerrainBottom);

                foreach (var run in BaseColumn(x))
                {
                    Append(result, Math.Max(run.Start, yFrom), Math.Min(run.End, stripTo), run.Terrain);
                }
            }

            if (!Procedural || yTo <= TerrainBottom)
            {
                return result;
            }

            var tileX = CoordinateUtilities.CellToChunk(x);
            var localX = x - tileX * chunkSize;
            var lastTileY = CoordinateUtilities.CellToChunk(yTo - 1);

            for (var tileY = CoordinateUtilities.CellToChunk(Math.Max(yFrom, TerrainBottom)); tileY <= lastTileY; tileY++)
            {
                foreach (var run in Tile(new Vector2I(tileX, tileY)).Columns[localX])
                {
                    Append(result, Math.Max(run.Start, yFrom), Math.Min(run.End, yTo), run.Terrain);
                }
            }

            return result;
        }

        private static List<Run> MergeEdits(List<Run> original, SortedDictionary<int, int> edits, int yFrom, int yTo)
        {
            var result = new List<Run>();
            var index = 0;
            var cursor = yFrom;

            foreach (var edit in edits)
            {
                if (edit.Key < yFrom)
                {
                    continue;
                }

                if (edit.Key >= yTo)
                {
                    break;
                }

                while (index < original.Count && original[index].End <= edit.Key)
                {
                    Append(result, Math.Max(original[index].Start, cursor), original[index].End, original[index].Terrain);

                    index++;
                }

                if (index < original.Count && original[index].Start < edit.Key)
                {
                    Append(result, Math.Max(original[index].Start, cursor), edit.Key, original[index].Terrain);
                }

                Append(result, edit.Key, edit.Key + 1, edit.Value);

                cursor = edit.Key + 1;
            }

            for (; index < original.Count; index++)
            {
                Append(result, Math.Max(original[index].Start, cursor), original[index].End, original[index].Terrain);
            }

            return result;
        }

        #endregion

        #region Types

        public readonly record struct Run(int Start, int End, int Terrain);

        private sealed class Strip
        {
            public Strip(Dictionary<Vector2I, int> wallTops)
            {
                WallTops = wallTops;
            }

            public readonly Dictionary<int, List<Run>> Columns = new();
            public readonly Dictionary<Vector2I, int> WallTops;

            public long LastUse;
        }

        private sealed class InteriorTile
        {
            public InteriorTile(bool[] open, int chunkSize)
            {
                Open = open;
                Columns = new List<Run>[chunkSize];

                for (var i = 0; i < chunkSize; i++)
                {
                    Columns[i] = new List<Run>();
                }
            }

            public readonly bool[] Open;
            public readonly List<Run>[] Columns;

            public long LastUse;
        }

        #endregion
    }
}
