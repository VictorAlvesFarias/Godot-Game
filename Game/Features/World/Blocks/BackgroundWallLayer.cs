using Godot;
using Jogo25D.Core;
using Jogo25D.Features.World.Chunks.Resources;
using System.Collections.Generic;

namespace Jogo25D.Blocks
{
    // A receiving surface behind the playable plane, never a foreground occluder.
    [Tool, GlobalClass]
    public partial class BackgroundWallLayer : TileMapLayer
    {
        private readonly Dictionary<Vector2I, Dictionary<Vector2I, (int Source, Vector2I Atlas, int Alternative)>> _chunks = new();
        private readonly HashSet<Vector2I> _residentChunks = new();
        private bool _streaming;
        private static Vector2I Chunk(Vector2I c) => new((int)System.Math.Floor(c.X / (double)Jogo25D.Constants.ChunkStreamingConstants.CHUNK_SIZE), (int)System.Math.Floor(c.Y / (double)Jogo25D.Constants.ChunkStreamingConstants.CHUNK_SIZE));
        public IEnumerable<Vector2I> LogicalCells
        {
            get { foreach (var cells in _chunks.Values) foreach (var cell in cells.Keys) yield return cell; }
        }
        public override void _EnterTree()
        {
            CollisionEnabled = false;
            NavigationEnabled = false;
            OcclusionEnabled = false;
        }
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;
            foreach (var cell in GetUsedCells()) Store(cell, GetCellSourceId(cell), GetCellAtlasCoords(cell), GetCellAlternativeTile(cell));
        }
        private void Store(Vector2I cell, int source, Vector2I atlas, int alternative = 0)
        {
            var chunk = Chunk(cell);
            if (!_chunks.TryGetValue(chunk, out var cells)) _chunks[chunk] = cells = new();
            if (source < 0) cells.Remove(cell);
            else cells[cell] = (source, atlas, alternative);
        }
        public void RestoreChunk(Vector2I chunk)
        {
            _residentChunks.Add(chunk);
            if (_chunks.TryGetValue(chunk, out var cells))
                foreach (var (cell, tile) in cells) SetCell(cell, tile.Source, tile.Atlas, tile.Alternative);
        }
        public void ClearRenderedForStreaming()
        {
            _streaming = true;
            _residentChunks.Clear();
            Clear();
        }
        public void UnloadChunk(Vector2I chunk)
        {
            _residentChunks.Remove(chunk);
            if (_chunks.TryGetValue(chunk, out var cells))
                foreach (var cell in cells.Keys) EraseCell(cell);
        }
        public bool EditAuthoritative(Vector2I cell, string blockId, bool remove)
        {
            if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) return false;
            if (remove ? GetCellSourceId(cell) < 0 : GetCellSourceId(cell) >= 0) return false;
            if (!remove && (!BlockDB.TryGet(blockId, out var block) || !block.IsBackground)) return false;
            int previousSource = GetCellSourceId(cell);
            string dimension = Game.Managers.DimensionManager.Node.ResolveDimensionIdOf(this);
            string type = remove ? "wall_break" : "wall_place";
            ApplyEdit(cell, blockId, remove);
            Game.Managers.TileStreamingManager.Node.RecordMutation(dimension, cell, type, blockId);
            if (remove)
            {
                string drop = previousSource == Jogo25D.Constants.TerrainsConstants.WOOD ? "wall_wood" : previousSource == 0 ? "wall_dirt" : null;
                if (drop != null) Game.Managers.DimensionManager.Node.SpawnWorldItemRequest(
                    Jogo25D.Items.ItemFactory.CreateInstance(drop), ToGlobal(MapToLocal(cell)), dimension);
            }
            if (Multiplayer.HasMultiplayerPeer()) Rpc(nameof(ReceiveEdit), cell, blockId, remove);
            return true;
        }
        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ReceiveEdit(Vector2I cell, string blockId, bool remove) => ApplyEdit(cell, blockId, remove);
        public void ApplyMutation(ChunkMutationData mutation) => ApplyEdit(
            new Vector2I((int)mutation.Position.X, (int)mutation.Position.Y), mutation.ExtraData, mutation.Type == "wall_break");
        private void ApplyEdit(Vector2I cell, string blockId, bool remove)
        {
            if (!remove && (!BlockDB.TryGet(blockId, out var validated) || !validated.IsBackground)) return;
            if (!Engine.IsEditorHint())
            {
                string dimension = Game.Managers.DimensionManager.Node.ResolveDimensionIdOf(this);
                Game.Managers.LightMapManager.Node?.GetWorld(dimension).SetBackground(cell.X, cell.Y, !remove);
            }
            if (remove) { Store(cell, -1, Vector2I.Zero); EraseCell(cell); return; }
            if (!BlockDB.TryGet(blockId, out var block) || !block.IsBackground) return;
            Store(cell, block.SourceId, block.AtlasCoord);
            if (!_streaming || _residentChunks.Contains(Chunk(cell))) SetCell(cell, block.SourceId, block.AtlasCoord);
        }
    }
}
