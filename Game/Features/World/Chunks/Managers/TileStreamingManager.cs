using Godot;
using Jogo25D.Biomes;
using Jogo25D.Blocks;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Features.World.Chunks.Resources;
using Jogo25D.Systems;
using Jogo25D.Utils.Coordinates;
using Jogo25D.Utils.GodotDictionaryParser;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jogo25D.Chunks
{
    public partial class TileStreamingManager : Node
    {
        #region Events

        public event System.Action<string, Vector2I> ChunkLoaded;
        public event System.Action<string, Vector2I> ChunkUnloaded;

        #endregion

        #region Dinamic properties

        private const int TerrainBatchLimit = 3072;
        private const int BackgroundBatchLimit = 2048;

        public bool Enabled { get; set; } = false;
        public int TileSize => Dimensions.TileSize;

        private static DimensionManager Dimensions => Game.Managers.DimensionManager.Node;
        public float EvaluateTimer { get; set; }
        public long WorldSeed { get; set; }

        #endregion

        #region World control

        private readonly HashSet<Vector2I> _loadedOverworld = new();
        private readonly HashSet<Vector2I> _loadedUpsidedown = new();
        private readonly Dictionary<Vector2I, ChunkStateData> _overworldState = new();
        private readonly Dictionary<Vector2I, ChunkStateData> _upsidedownState = new();
        private readonly Dictionary<Vector2I, HashSet<long>> _overworldLoadedPeers = new();
        private readonly Dictionary<Vector2I, HashSet<long>> _upsidedownLoadedPeers = new();

        #endregion

        #region Systems

        private readonly ChunkGeneratorSystem _generator = new();

        private readonly MinimapSystem _minimap = new();

        #endregion

        #region Node references

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            if (IsServerAuthoritative())
            {
                WorldSeed = (uint)GD.Randi();
            }
        }

        public override void _Process(double delta)
        {
            if (!Enabled || !IsServerAuthoritative() || Game.Managers.WorldManager.Node == null)
            {
                return;
            }

            EvaluateTimer += (float)delta;

            if (EvaluateTimer < ChunkStreamingConstants.EVALUATE_INTERVAL_SECONDS)
            {
                return;
            }

            EvaluateTimer = 0f;

            if (!_isEvaluatingOverworld)
            {
                _isEvaluatingOverworld = true;

                _ = EvaluateAsync(ChunkStreamingConstants.OVERWORLD_ID, Dimensions.ResolveParent(ChunkStreamingConstants.OVERWORLD_ID), _loadedOverworld, _overworldState, _overworldLoadedPeers, () => _isEvaluatingOverworld = false);
            }

            if (!_isEvaluatingUpsidedown)
            {
                _isEvaluatingUpsidedown = true;

                _ = EvaluateAsync(ChunkStreamingConstants.UPSIDEDOWN_ID, Dimensions.ResolveParent(ChunkStreamingConstants.UPSIDEDOWN_ID), _loadedUpsidedown, _upsidedownState, _upsidedownLoadedPeers, () => _isEvaluatingUpsidedown = false);
            }
        }

        #endregion

        #region Core - Evaluation (load/unload decision)

        private bool _isEvaluatingOverworld;
        private bool _isEvaluatingUpsidedown;

        private bool IsServerAuthoritative()
        {
            return Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();
        }

        private async Task EvaluateAsync(string dimensionId, Node2D dimensionParent, HashSet<Vector2I> loaded, Dictionary<Vector2I, ChunkStateData> state, Dictionary<Vector2I, HashSet<long>> loadedPeers, System.Action onDone)
        {
            try
            {
                await EvaluateCore(dimensionId, dimensionParent, loaded, state, loadedPeers);
            }
            finally
            {
                onDone();
            }
        }

        private async Task EvaluateCore(string dimensionId, Node2D dimensionParent, HashSet<Vector2I> loaded, Dictionary<Vector2I, ChunkStateData> state, Dictionary<Vector2I, HashSet<long>> loadedPeers)
        {
            if (dimensionParent == null)
            {
                return;
            }

            var playersHere = Game.Managers.WorldManager.Node
                .GetPlayersInDimension(dimensionId)
                .Where(p => p.PeerId > 0)
                .ToList();

            if (playersHere.Count == 0)
            {
                return;
            }

            var playerChunks = playersHere.Select(p => CoordinateUtilities.WorldToChunk(p.GlobalPosition, TileSize)).ToList();
            var needed = new HashSet<Vector2I>();
            var neededByPeer = new Dictionary<Vector2I, HashSet<long>>();

            foreach (var player in playersHere)
            {
                var playerChunk = CoordinateUtilities.WorldToChunk(player.GlobalPosition, TileSize);

                for (int dx = -ChunkStreamingConstants.LOAD_RADIUS_CHUNKS; dx <= ChunkStreamingConstants.LOAD_RADIUS_CHUNKS; dx++)
                {
                    for (int dy = -ChunkStreamingConstants.LOAD_RADIUS_CHUNKS; dy <= ChunkStreamingConstants.LOAD_RADIUS_CHUNKS; dy++)
                    {
                        var coord = playerChunk + new Vector2I(dx, dy);

                        needed.Add(coord);

                        if (!neededByPeer.TryGetValue(coord, out var peers))
                        {
                            peers = new HashSet<long>();
                            neededByPeer[coord] = peers;
                        }

                        peers.Add(player.PeerId);
                    }
                }
            }

            var missing = needed
                .Where(c => !loaded.Contains(c))
                .OrderBy(c => playerChunks.Min(pc => CoordinateUtilities.ChunkDistance(c, pc)))
                .Take(ChunkStreamingConstants.MAX_CHUNK_LOADS_PER_TICK);

            var missingList = missing.ToList();

            foreach (var chunkCoord in missingList)
            {
                var requestingPeers = neededByPeer.TryGetValue(chunkCoord, out var peers) ? peers : new HashSet<long>();

                await LoadChunkAsync(dimensionId, chunkCoord, loaded, state, loadedPeers, requestingPeers);
            }

            SendPendingChunksToPeers(dimensionId, needed, loaded, state, loadedPeers, neededByPeer);

            var toUnload = new List<Vector2I>();

            foreach (var chunkCoord in loaded)
            {
                var withinUnloadRadius = playerChunks.Any(playerChunk =>
                    CoordinateUtilities.ChunkDistance(chunkCoord, playerChunk) <= ChunkStreamingConstants.UNLOAD_RADIUS_CHUNKS);

                if (!withinUnloadRadius)
                {
                    toUnload.Add(chunkCoord);
                }
            }

            foreach (var chunkCoord in toUnload)
            {
                await UnloadChunkAsync(dimensionId, chunkCoord, loaded, state, loadedPeers);
            }
        }

        private void SendPendingChunksToPeers(
            string dimensionId,
            HashSet<Vector2I> needed,
            HashSet<Vector2I> loaded,
            Dictionary<Vector2I, ChunkStateData> state,
            Dictionary<Vector2I, HashSet<long>> loadedPeers,
            Dictionary<Vector2I, HashSet<long>> neededByPeer)
        {
            var ownPeerId = Multiplayer != null && Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 1;

            foreach (var chunkCoord in needed)
            {
                if (!loaded.Contains(chunkCoord) || !neededByPeer.TryGetValue(chunkCoord, out var wanted))
                {
                    continue;
                }

                if (!loadedPeers.TryGetValue(chunkCoord, out var have))
                {
                    have = new HashSet<long>();
                    loadedPeers[chunkCoord] = have;
                }

                Godot.Collections.Dictionary stateDict = null;

                foreach (var peerId in wanted)
                {
                    if (peerId == ownPeerId || have.Contains(peerId))
                    {
                        continue;
                    }

                    stateDict ??= GodotDictionaryParser.ToDictionary(
                        state.TryGetValue(chunkCoord, out var chunkState) ? chunkState : new ChunkStateData());

                    LoadChunkRequest(peerId, dimensionId, chunkCoord, stateDict);

                    have.Add(peerId);
                }
            }
        }

        public async Task PreloadSpawnAreaAsync(string dimensionId, Node2D dimensionParent, Vector2 worldPosition)
        {
            if (dimensionParent == null)
            {
                return;
            }

            var loaded = ResolveLoaded(dimensionId);
            var state = ResolveState(dimensionId);
            var loadedPeers = ResolveLoadedPeers(dimensionId);
            var centerChunk = CoordinateUtilities.WorldToChunk(worldPosition, TileSize);
            var ownPeerId = Multiplayer != null && Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 1;

            for (int dx = -ChunkStreamingConstants.LOAD_RADIUS_CHUNKS; dx <= ChunkStreamingConstants.LOAD_RADIUS_CHUNKS; dx++)
            {
                for (int dy = -ChunkStreamingConstants.LOAD_RADIUS_CHUNKS; dy <= ChunkStreamingConstants.LOAD_RADIUS_CHUNKS; dy++)
                {
                    var chunkCoord = centerChunk + new Vector2I(dx, dy);

                    if (!loaded.Contains(chunkCoord))
                    {
                        await LoadChunkAsync(dimensionId, chunkCoord, loaded, state, loadedPeers, new HashSet<long> { ownPeerId });
                    }
                }
            }
        }

        #endregion

        #region Core - Load/Unload (server-side)

        public void RecordMutation(string dimensionId, Vector2I cell, string type, string extraData)
        {
            Game.Managers.LightMapManager.Node?.SetCell(dimensionId, cell, type, extraData);
            var state = ResolveState(dimensionId);
            var chunkCoord = CoordinateUtilities.CellToChunk(cell);

            if (!state.TryGetValue(chunkCoord, out var chunkState))
            {
                chunkState = new ChunkStateData();
                state[chunkCoord] = chunkState;
            }

            chunkState.Mutations.Add(new ChunkMutationData
            {
                Type = type,
                Position = new Vector2(cell.X, cell.Y),
                ExtraData = extraData ?? "",
            });
        }

        public Texture2D GetDiscoveredTexture(TileMapLayer layer, out Vector2I origin)
        {
            foreach (var dimensionId in new[] { ChunkStreamingConstants.OVERWORLD_ID, ChunkStreamingConstants.UPSIDEDOWN_ID })
            {
                if (layer == Dimensions.ResolveLayer(dimensionId))
                {
                    return _minimap.GetTexture(dimensionId, out origin);
                }
            }

            origin = Vector2I.Zero;

            return null;
        }

        public BiomeDefinition ResolveBiome(string dimensionId, int worldX, int worldY)
        {
            return BiomeDB.Get(_generator.GetBiomeIdAtPosition(WorldSeed, dimensionId, worldX, worldY));
        }

        private async Task LoadChunkAsync(string dimensionId, Vector2I chunkCoord, HashSet<Vector2I> loaded, Dictionary<Vector2I, ChunkStateData> state, Dictionary<Vector2I, HashSet<long>> loadedPeers, HashSet<long> requestingPeers)
        {
            var layer = Dimensions.ResolveLayer(dimensionId);
            var baseLayer = Dimensions.ResolveBaseLayer(dimensionId);

            loaded.Add(chunkCoord);

            await _generator.PaintTilesAsync(layer, baseLayer, WorldSeed, dimensionId, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);

            if (!state.TryGetValue(chunkCoord, out var chunkState))
            {
                chunkState = new ChunkStateData();
                state[chunkCoord] = chunkState;
            }

            ApplyMutations(layer, chunkState);
            _minimap.RecordChunk(dimensionId, layer, chunkCoord);

            loadedPeers[chunkCoord] = new HashSet<long>(requestingPeers);

            var stateDict = GodotDictionaryParser.ToDictionary(chunkState);
            var ownPeerId = Multiplayer != null && Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 1;

            foreach (var peerId in requestingPeers)
            {
                if (peerId == ownPeerId)
                {
                    continue;
                }

                LoadChunkRequest(peerId, dimensionId, chunkCoord, stateDict);
            }

            ChunkLoaded?.Invoke(dimensionId, chunkCoord);
        }

        private void ApplyMutations(TerrainLayer layer, ChunkStateData chunkState)
        {
            foreach (var mutation in chunkState.Mutations)
            {
                if (mutation.Type is "wall_place" or "wall_break")
                {
                    layer.GetParent().GetNodeOrNull<BackgroundWallLayer>("BackgroundWalls")?.ApplyMutation(mutation);
                }
                else
                {
                    layer.ApplyChunkMutation(mutation);
                }
            }
        }

        private void LoadChunkRequest(long peerId, string dimensionId, Vector2I chunkCoord, Godot.Collections.Dictionary stateDict)
        {
            if (!IsPeerConnected(peerId))
            {
                return;
            }

            RpcId(peerId, nameof(LoadChunkReceive), dimensionId, chunkCoord, stateDict);
        }

        private bool IsPeerConnected(long peerId)
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
            {
                return false;
            }

            if (peerId == Multiplayer.GetUniqueId())
            {
                return true;
            }

            foreach (var connectedId in Multiplayer.GetPeers())
            {
                if (connectedId == peerId)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task UnloadChunkAsync(string dimensionId, Vector2I chunkCoord, HashSet<Vector2I> loaded, Dictionary<Vector2I, ChunkStateData> state, Dictionary<Vector2I, HashSet<long>> loadedPeers)
        {
            if (!loaded.Remove(chunkCoord))
            {
                return;
            }

            var layer = Dimensions.ResolveLayer(dimensionId);
            var baseLayer = Dimensions.ResolveBaseLayer(dimensionId);

            await _generator.EraseTilesAsync(layer, baseLayer, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);

            if (loadedPeers.TryGetValue(chunkCoord, out var peers))
            {
                var ownPeerId = Multiplayer != null && Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 1;

                foreach (var peerId in peers)
                {
                    if (peerId != ownPeerId)
                    {
                        UnloadChunkRequest(peerId, dimensionId, chunkCoord);
                    }
                }

                loadedPeers.Remove(chunkCoord);
            }

            ChunkUnloaded?.Invoke(dimensionId, chunkCoord);
        }

        private void UnloadChunkRequest(long peerId, string dimensionId, Vector2I chunkCoord)
        {
            if (!IsPeerConnected(peerId))
            {
                return;
            }

            RpcId(peerId, nameof(UnloadChunkReceive), dimensionId, chunkCoord);
        }

        #endregion

        #region Core - Rpc - Chunks

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SetWorldSeedReceive(long seed)
        {
            WorldSeed = seed;
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public async void LoadChunkReceive(string dimensionId, Vector2I chunkCoord, Godot.Collections.Dictionary stateDict)
        {
            var dimensionParent = Dimensions.ResolveParent(dimensionId);
            var loaded = ResolveLoaded(dimensionId);

            if (dimensionParent == null || loaded.Contains(chunkCoord))
            {
                return;
            }

            var layer = Dimensions.ResolveLayer(dimensionId);
            var baseLayer = Dimensions.ResolveBaseLayer(dimensionId);

            loaded.Add(chunkCoord);

            var chunkState = GodotDictionaryParser.ToResource<ChunkStateData>(stateDict);

            foreach (var mutation in chunkState.Mutations)
            {
                Game.Managers.LightMapManager.Node?.SetCell(
                    dimensionId,
                    new Vector2I((int)mutation.Position.X, (int)mutation.Position.Y),
                    mutation.Type,
                    mutation.ExtraData);
            }

            await _generator.PaintTilesAsync(layer, baseLayer, WorldSeed, dimensionId, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);

            ApplyMutations(layer, chunkState);
            _minimap.RecordChunk(dimensionId, layer, chunkCoord);

            ResolveState(dimensionId)[chunkCoord] = chunkState;

            ChunkLoaded?.Invoke(dimensionId, chunkCoord);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public async void UnloadChunkReceive(string dimensionId, Vector2I chunkCoord)
        {
            var loaded = ResolveLoaded(dimensionId);

            if (!loaded.Remove(chunkCoord))
            {
                return;
            }

            var dimensionParent = Dimensions.ResolveParent(dimensionId);

            if (dimensionParent == null)
            {
                return;
            }

            var layer = Dimensions.ResolveLayer(dimensionId);
            var baseLayer = Dimensions.ResolveBaseLayer(dimensionId);

            await _generator.EraseTilesAsync(layer, baseLayer, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);

            ChunkUnloaded?.Invoke(dimensionId, chunkCoord);
        }

        #endregion

        #region Core - Peer catch-up

        public void CatchUpPeer(long targetPeerId)
        {
            CatchUpPeer(targetPeerId, Vector2.Zero);
        }

        public void CatchUpPeer(long targetPeerId, Vector2 aroundPosition)
        {
            SetWorldSeedRequest(targetPeerId);
            SendLightWorld(targetPeerId, ChunkStreamingConstants.OVERWORLD_ID);
            SendLightWorld(targetPeerId, ChunkStreamingConstants.UPSIDEDOWN_ID);

            var aroundChunk = CoordinateUtilities.WorldToChunk(aroundPosition, TileSize);

            CatchUpDimension(ChunkStreamingConstants.OVERWORLD_ID, _loadedOverworld, _overworldState, _overworldLoadedPeers, targetPeerId, aroundChunk);
            CatchUpDimension(ChunkStreamingConstants.UPSIDEDOWN_ID, _loadedUpsidedown, _upsidedownState, _upsidedownLoadedPeers, targetPeerId, aroundChunk);
        }

        private void SetWorldSeedRequest(long targetPeerId)
        {
            RpcId(targetPeerId, nameof(SetWorldSeedReceive), WorldSeed);
        }

        private void SendLightWorld(long peerId, string dimensionId)
        {
            var world = Game.Managers.LightMapManager.Node.GetWorld(dimensionId);
            var batch = new List<int>(TerrainBatchLimit);
            var reset = true;

            foreach (var edit in world.Edits())
            {
                batch.Add(edit.X);
                batch.Add(edit.Y);
                batch.Add(edit.Terrain);

                if (batch.Count < TerrainBatchLimit)
                {
                    continue;
                }

                RpcId(peerId, nameof(ReceiveLightWorld), dimensionId, world.Procedural, reset, batch.ToArray());

                reset = false;

                batch.Clear();
            }

            if (reset || batch.Count > 0)
            {
                RpcId(peerId, nameof(ReceiveLightWorld), dimensionId, world.Procedural, reset, batch.ToArray());
            }

            batch.Clear();

            foreach (var cell in world.BackgroundCells)
            {
                batch.Add(cell.X);
                batch.Add(cell.Y);

                if (batch.Count < BackgroundBatchLimit)
                {
                    continue;
                }

                RpcId(peerId, nameof(ReceiveBackgroundLight), dimensionId, batch.ToArray());

                batch.Clear();
            }

            if (batch.Count > 0)
            {
                RpcId(peerId, nameof(ReceiveBackgroundLight), dimensionId, batch.ToArray());
            }
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ReceiveBackgroundLight(string dimensionId, int[] cells)
        {
            var world = Game.Managers.LightMapManager.Node.GetWorld(dimensionId);

            for (int i = 0; i + 1 < cells.Length; i += 2)
            {
                world.SetBackground(cells[i], cells[i + 1], true);
            }
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ReceiveLightWorld(string dimensionId, bool procedural, bool reset, int[] edits)
        {
            var manager = Game.Managers.LightMapManager.Node;

            if (reset)
            {
                manager.ReplaceWorld(dimensionId, procedural);
            }

            var world = manager.GetWorld(dimensionId);

            for (int i = 0; i + 2 < edits.Length; i += 3)
            {
                world.SetTerrain(edits[i], edits[i + 1], edits[i + 2]);
            }
        }

        private void CatchUpDimension(string dimensionId, HashSet<Vector2I> loaded, Dictionary<Vector2I, ChunkStateData> state, Dictionary<Vector2I, HashSet<long>> loadedPeers, long targetPeerId, Vector2I aroundChunk)
        {
            foreach (var chunkCoord in loaded)
            {
                if (CoordinateUtilities.ChunkDistance(chunkCoord, aroundChunk) > ChunkStreamingConstants.UNLOAD_RADIUS_CHUNKS)
                {
                    continue;
                }

                var chunkState = state.TryGetValue(chunkCoord, out var s) ? s : new ChunkStateData();

                LoadChunkRequest(targetPeerId, dimensionId, chunkCoord, GodotDictionaryParser.ToDictionary(chunkState));

                if (!loadedPeers.TryGetValue(chunkCoord, out var peers))
                {
                    peers = new HashSet<long>();
                    loadedPeers[chunkCoord] = peers;
                }

                peers.Add(targetPeerId);
            }
        }

        #endregion

        #region Core - Peer disconnect

        public void RemovePeer(long peerId)
        {
            RemovePeerFrom(_overworldLoadedPeers, peerId);
            RemovePeerFrom(_upsidedownLoadedPeers, peerId);
        }

        private static void RemovePeerFrom(Dictionary<Vector2I, HashSet<long>> loadedPeers, long peerId)
        {
            foreach (var peers in loadedPeers.Values)
            {
                peers.Remove(peerId);
            }
        }

        #endregion

        #region Core - Persistencia (save/load de mundo)

        public void SetWorldSeed(long seed)
        {
            WorldSeed = seed;
        }

        public Godot.Collections.Array ExportMutations(string dimensionId)
        {
            var lista = new Godot.Collections.Array();

            foreach (var (_, chunkState) in ResolveState(dimensionId))
            {
                foreach (var mutacao in chunkState.Mutations)
                {
                    lista.Add(new Godot.Collections.Dictionary
                    {
                        { "type", mutacao.Type },
                        { "x", (int)mutacao.Position.X },
                        { "y", (int)mutacao.Position.Y },
                        { "blockId", mutacao.ExtraData },
                    });
                }
            }

            return lista;
        }

        public void ImportMutations(string dimensionId, Godot.Collections.Array lista)
        {
            var state = ResolveState(dimensionId);

            state.Clear();

            if (lista == null)
            {
                return;
            }

            foreach (var bruta in lista)
            {
                var mutacao = bruta.AsGodotDictionary();
                var cell = new Vector2I(mutacao["x"].AsInt32(), mutacao["y"].AsInt32());
                var chunk = CoordinateUtilities.CellToChunk(cell);
                Game.Managers.LightMapManager.Node?.SetCell(dimensionId, cell, mutacao["type"].AsString(),
                    mutacao.TryGetValue("blockId", out var opticalBlock) ? opticalBlock.AsString() : "");

                if (!state.TryGetValue(chunk, out var chunkState))
                {
                    chunkState = new ChunkStateData();
                    state[chunk] = chunkState;
                }

                var restoredMutation = new ChunkMutationData
                {
                    Type = mutacao["type"].AsString(),
                    Position = new Vector2(cell.X, cell.Y),
                    ExtraData = mutacao.TryGetValue("blockId", out var b) ? b.AsString() : "",
                };
                chunkState.Mutations.Add(restoredMutation);

                if (restoredMutation.Type is "wall_place" or "wall_break")
                {
                    Dimensions.ResolveParent(dimensionId)?.GetNodeOrNull<BackgroundWallLayer>("BackgroundWalls")?.ApplyMutation(restoredMutation);
                }
            }
        }

        #endregion

        #region Core - Reset

        public void ResetState()
        {
            Enabled = false;

            _loadedOverworld.Clear();
            _loadedUpsidedown.Clear();
            _overworldState.Clear();
            _upsidedownState.Clear();
            _overworldLoadedPeers.Clear();
            _upsidedownLoadedPeers.Clear();
            _minimap.Reset();
            EvaluateTimer = 0f;
        }

        #endregion

        #region Utils

        private HashSet<Vector2I> ResolveLoaded(string dimensionId)
        {
            return dimensionId == ChunkStreamingConstants.OVERWORLD_ID ? _loadedOverworld : _loadedUpsidedown;
        }

        private Dictionary<Vector2I, ChunkStateData> ResolveState(string dimensionId)
        {
            return dimensionId == ChunkStreamingConstants.OVERWORLD_ID ? _overworldState : _upsidedownState;
        }

        private Dictionary<Vector2I, HashSet<long>> ResolveLoadedPeers(string dimensionId)
        {
            return dimensionId == ChunkStreamingConstants.OVERWORLD_ID ? _overworldLoadedPeers : _upsidedownLoadedPeers;
        }

        #endregion
    }
}
