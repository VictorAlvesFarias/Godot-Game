using Godot;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Entities;
using Jogo25D.Features.World.Chunks.Resources;
using Jogo25D.Items;
using Jogo25D.Light;
using Jogo25D.Utils.Coordinates;
using System.Collections.Generic;

namespace Jogo25D.Blocks
{
    [Tool, GlobalClass]
    public partial class BackgroundWallLayer : TileMapLayer
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

        private readonly Dictionary<Vector2I, Dictionary<Vector2I, (int Source, Vector2I Atlas, int Alternative)>> _chunks = new();
        private readonly HashSet<Vector2I> _residentChunks = new();

        private bool _streaming;

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
            if (Engine.IsEditorHint())
            {
                return;
            }

            foreach (var cell in GetUsedCells())
            {
                Store(cell, GetCellSourceId(cell), GetCellAtlasCoords(cell), GetCellAlternativeTile(cell));
            }
        }

        #endregion

        #region Core - Streaming

        public void RestoreChunk(Vector2I chunk)
        {
            _residentChunks.Add(chunk);

            if (!_chunks.TryGetValue(chunk, out var cells))
            {
                return;
            }

            foreach (var (cell, tile) in cells)
            {
                SetCell(cell, tile.Source, tile.Atlas, tile.Alternative);
            }
        }

        public void UnloadChunk(Vector2I chunk)
        {
            _residentChunks.Remove(chunk);

            if (!_chunks.TryGetValue(chunk, out var cells))
            {
                return;
            }

            foreach (var cell in cells.Keys)
            {
                EraseCell(cell);
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
            var type = remove ? "wall_break" : "wall_place";

            ApplyEdit(cell, blockId, remove);

            (GetParent() as Dimension)?.RecordMutation(cell, type, blockId);

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

        public void ApplyMutation(ChunkMutationData mutation)
        {
            ApplyEdit(
                new Vector2I((int)mutation.Position.X, (int)mutation.Position.Y),
                mutation.ExtraData,
                mutation.Type == "wall_break");
        }

        private void ApplyEdit(Vector2I cell, string blockId, bool remove)
        {
            if (!remove && (!BlockDB.TryGet(blockId, out var validated) || !validated.IsBackground))
            {
                return;
            }

            if (!Engine.IsEditorHint())
            {
                var dimension = Dimension.IdOf(this);

                (GetParent() as Dimension)?.EnsureWorld().SetBackground(cell.X, cell.Y, !remove);

                GetParent()?.GetNodeOrNull<LightMap2D>("LightMap")?.OnCellChanged(cell);
            }

            if (remove)
            {
                Store(cell, -1, Vector2I.Zero);
                EraseCell(cell);

                return;
            }

            if (!BlockDB.TryGet(blockId, out var block) || !block.IsBackground)
            {
                return;
            }

            Store(cell, block.SourceId, block.AtlasCoord);

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
