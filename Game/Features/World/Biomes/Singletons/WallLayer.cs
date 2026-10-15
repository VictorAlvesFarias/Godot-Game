using Godot;
using Jogo25D.Blocks;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Entities;
using Jogo25D.Save.Types;
using Jogo25D.Items;
using Jogo25D.Light;
using Jogo25D.Utils.Coordinates;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jogo25D.Biomes
{
    // Herda o autotile do TerrainLayer para as paredes geradas. No mundo procedural tudo sai do TerrainCache
    // (geradas com autotile, as do jogador com o tile do BlockDB); no autoral as paredes ficam guardadas como estao.
    [Tool, GlobalClass]
    public partial class WallLayer : TerrainLayer
    {
        #region Dinamic properties

        public IEnumerable<Vector2I> LogicalCells
        {
            get
            {
                foreach (var cells in _chunks.Values)
                {
                    foreach (var cell in cells.Keys)
                    {
                        yield return cell;
                    }
                }
            }
        }

        // So no mundo autoral: as paredes da cena e as editadas, por chunk.
        private readonly Dictionary<Vector2I, Dictionary<Vector2I, (int Source, Vector2I Atlas, int Alternative)>> _chunks = new();
        private readonly HashSet<Vector2I> _residentChunks = new();

        private bool _streaming;

        private bool Procedural => GetParent() is Dimension dimension && dimension.Procedural;

        #endregion

        #region Godot implementation

        public override void _EnterTree()
        {
            CollisionEnabled = false;
            NavigationEnabled = false;
            OcclusionEnabled = false;
        }

        public override void _Ready()
        {
            base._Ready();

            if (Engine.IsEditorHint())
            {
                return;
            }

            foreach (var cell in GetUsedCells())
            {
                Store(cell, GetCellSourceId(cell), GetCellAtlasCoords(cell), GetCellAlternativeTile(cell));
            }
        }

        // Sem recalculo automatico no editor: as paredes pintadas a mao na cena ficam como foram desenhadas.
        public override void _UpdateCells(Godot.Collections.Array<Vector2I> coords, bool forcedCleanup)
        {
        }

        #endregion

        #region Core - Streaming

        public void RestoreChunk(Vector2I chunk)
        {
            _residentChunks.Add(chunk);

            if (Procedural || !_chunks.TryGetValue(chunk, out var cells))
            {
                return;
            }

            foreach (var (cell, tile) in cells)
            {
                SetCell(cell, tile.Source, tile.Atlas, tile.Alternative);
            }
        }

        // Paredes geradas: todo o terreno abaixo do chao natural tem fundo (caverna incluida), menos onde o jogador
        // quebrou a parede. Sao conectadas pelo autotile com o terrain set de bordercap do bioma, como a camada Base.
        // Paredes colocadas pelo jogador (edicoes de parede no cache) tem prioridade e usam o tile do BlockDB.
        public async Task PaintProceduralChunkAsync(Vector2I chunk, int cellsPerFrame)
        {
            if (Engine.IsEditorHint() || GetParent() is not Dimension dimension || !dimension.Procedural)
            {
                return;
            }

            var terrain = dimension.EnsureTerrain();
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var origin = chunk * chunkSize;
            var cellsByTerrainSet = new Dictionary<int, List<Vector2I>>();

            for (var localX = 0; localX < chunkSize; localX++)
            {
                for (var localY = 0; localY < chunkSize; localY++)
                {
                    var cell = origin + new Vector2I(localX, localY);
                    var placed = terrain.PlacedWallAt(cell);

                    if (placed != null)
                    {
                        if (BlockDB.TryGet(placed, out var block) && block.IsBackground)
                        {
                            SetCell(cell, block.SourceId, block.AtlasCoord);
                        }

                        continue;
                    }

                    if (!terrain.HasProceduralBackground(cell.X, cell.Y) || !terrain.HasBackground(cell.X, cell.Y))
                    {
                        continue;
                    }

                    var terrainSet = dimension.ResolveBiome(cell.X, cell.Y).BorderCapTerrainSet;

                    if (!cellsByTerrainSet.TryGetValue(terrainSet, out var cells))
                    {
                        cellsByTerrainSet[terrainSet] = cells = new List<Vector2I>();
                    }

                    cells.Add(cell);
                }
            }

            foreach (var group in cellsByTerrainSet)
            {
                await ConnectAsync(group.Value, group.Key, cellsPerFrame);

                if (!IsInstanceValid(this) || !_residentChunks.Contains(chunk))
                {
                    return;
                }
            }

            foreach (var group in cellsByTerrainSet)
            {
                await ReconnectForeignBorderAsync(group.Value, group.Key, cellsPerFrame);
            }

            // As paredes geradas dos chunks vizinhos ja carregados foram conectadas sem este chunk; refaz a borda deles.
            var ring = new List<Vector2I>();

            for (var i = -1; i <= chunkSize; i++)
            {
                ring.Add(origin + new Vector2I(i, -1));
                ring.Add(origin + new Vector2I(i, chunkSize));
                ring.Add(origin + new Vector2I(-1, i));
                ring.Add(origin + new Vector2I(chunkSize, i));
            }

            ReconnectProceduralCells(ring);
        }

        private void ReconnectProceduralCells(IEnumerable<Vector2I> cells)
        {
            var procedural = new List<Vector2I>();

            foreach (var cell in cells)
            {
                if (!IsStored(cell) && GetCellSourceId(cell) != -1)
                {
                    procedural.Add(cell);
                }
            }

            ReconnectExistingCells(procedural);
        }

        // Parede que nao e gerada: a do jogador no procedural, ou qualquer parede guardada no autoral.
        private bool IsStored(Vector2I cell)
        {
            if (Procedural)
            {
                return GetParent() is Dimension dimension && dimension.EnsureTerrain().PlacedWallAt(cell) != null;
            }

            return _chunks.TryGetValue(CoordinateUtilities.CellToChunk(cell), out var stored) && stored.ContainsKey(cell);
        }


        public void UnloadChunk(Vector2I chunk)
        {
            _residentChunks.Remove(chunk);

            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var origin = chunk * chunkSize;

            // Apaga a area inteira: alem das paredes guardadas, ha as geradas.
            for (var localX = 0; localX < chunkSize; localX++)
            {
                for (var localY = 0; localY < chunkSize; localY++)
                {
                    EraseCell(origin + new Vector2I(localX, localY));
                }
            }
        }

        public void ResetForNewWorld()
        {
            _chunks.Clear();

            ClearRenderedForStreaming();
        }

        public void ClearRenderedForStreaming()
        {
            _streaming = true;

            _residentChunks.Clear();

            Clear();
        }

        #endregion

        #region Core - Edicao

        public bool EditAuthoritative(Vector2I cell, string blockId, bool remove)
        {
            if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer())
            {
                return false;
            }

            if (remove ? GetCellSourceId(cell) < 0 : GetCellSourceId(cell) >= 0)
            {
                return false;
            }

            if (!remove && (!BlockDB.TryGet(blockId, out var block) || !block.IsBackground))
            {
                return false;
            }

            var previousSource = GetCellSourceId(cell);
            var dimension = Dimension.IdOf(this);

            ApplyEdit(cell, blockId, remove);

            if (remove)
            {
                DropWall(previousSource, cell, dimension);
            }

            if (Multiplayer.HasMultiplayerPeer())
            {
                Rpc(nameof(ReceiveEdit), cell, blockId, remove);
            }

            return true;
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ReceiveEdit(Vector2I cell, string blockId, bool remove)
        {
            ApplyEdit(cell, blockId, remove);
        }

        // Edicao de parede ja salva (blockId nulo = parede removida), aplicada sem registrar de novo.
        public void ApplyStoredEdit(Vector2I cell, string blockId)
        {
            ApplyEdit(cell, blockId ?? "", blockId == null);
        }

        private void ApplyEdit(Vector2I cell, string blockId, bool remove)
        {
            if (!remove && (!BlockDB.TryGet(blockId, out var validated) || !validated.IsBackground))
            {
                return;
            }

            if (!Engine.IsEditorHint())
            {
                (GetParent() as Dimension)?.ApplyEdit(cell, TerrainLayerType.Wall, remove ? null : blockId);

                GetParent()?.GetNodeOrNull<LightMap2D>("LightMap")?.OnCellChanged(cell);
            }

            var procedural = Procedural;

            if (remove)
            {
                if (!procedural)
                {
                    Store(cell, -1, Vector2I.Zero);
                }

                EraseCell(cell);

                var neighbors = new List<Vector2I>();

                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if (dx != 0 || dy != 0)
                        {
                            neighbors.Add(cell + new Vector2I(dx, dy));
                        }
                    }
                }

                ReconnectProceduralCells(neighbors);

                return;
            }

            if (!BlockDB.TryGet(blockId, out var block) || !block.IsBackground)
            {
                return;
            }

            if (!procedural)
            {
                Store(cell, block.SourceId, block.AtlasCoord);
            }

            if (!_streaming || _residentChunks.Contains(CoordinateUtilities.CellToChunk(cell)))
            {
                SetCell(cell, block.SourceId, block.AtlasCoord);
            }
        }

        private void DropWall(int previousSource, Vector2I cell, string dimension)
        {
            var drop = previousSource == TerrainsConstants.WOOD
                ? "wall_wood"
                : previousSource == 0 ? "wall_dirt" : null;

            if (drop == null)
            {
                return;
            }

            EntitySpawner.SpawnWorldItemRequest(
                ItemFactory.CreateInstance(drop),
                ToGlobal(MapToLocal(cell)),
                dimension);
        }

        #endregion

        #region Utils

        private void Store(Vector2I cell, int source, Vector2I atlas, int alternative = 0)
        {
            var chunk = CoordinateUtilities.CellToChunk(cell);

            if (!_chunks.TryGetValue(chunk, out var cells))
            {
                _chunks[chunk] = cells = new Dictionary<Vector2I, (int Source, Vector2I Atlas, int Alternative)>();
            }

            if (source < 0)
            {
                cells.Remove(cell);
            }
            else
            {
                cells[cell] = (source, atlas, alternative);
            }
        }

        #endregion
    }
}
