using Godot;
using Jogo25D.Biomes;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Features.World.Chunks.Resources;
using Jogo25D.Systems;
using Jogo25D.Utils.GodotDictionaryParser;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jogo25D.Chunks
{
    public partial class ChunkStreamingManager : Node
    {
        #region Dinamic properties

        public bool Enabled { get; set; } = false;
        public int TileSize => OverworldLayer?.TileSet?.TileSize.X ?? UpsidedownLayer?.TileSet?.TileSize.X ?? 32;
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
        private readonly DiscoveredMapImage _discoveredOverworld = new();
        private readonly DiscoveredMapImage _discoveredUpsidedown = new();

        #endregion

        #region Systems

        private readonly ChunkGeneratorSystem _generator = new();

        #endregion

        #region Node references

        public WorldManager WorldManager { get; set; }

        #endregion

        #region Node children references

        public TerrainLayer OverworldLayer { get; set; }
        public TerrainLayer UpsidedownLayer { get; set; }
        public TerrainLayer OverworldBaseLayer { get; set; }
        public TerrainLayer UpsidedownBaseLayer { get; set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            WorldManager = GetTree().Root.GetNodeOrNull<WorldManager>(StaticNodePathsConstants.WorldManager);

            if (IsServerAuthoritative())
            {
                WorldSeed = (uint)GD.Randi();
            }
        }

        public override void _Process(double delta)
        {
            if (!Enabled || !IsServerAuthoritative() || WorldManager == null)
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

                _ = EvaluateAsync(ChunkStreamingConstants.OVERWORLD_ID, WorldManager.OverworldParent, _loadedOverworld, _overworldState, _overworldLoadedPeers, () => _isEvaluatingOverworld = false);
            }

            if (!_isEvaluatingUpsidedown)
            {
                _isEvaluatingUpsidedown = true;

                _ = EvaluateAsync(ChunkStreamingConstants.UPSIDEDOWN_ID, WorldManager.UpsidedownParent, _loadedUpsidedown, _upsidedownState, _upsidedownLoadedPeers, () => _isEvaluatingUpsidedown = false);
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

            var playersHere = GetTree().GetNodesInGroup("players")
                .OfType<Player>()
                .Where(p => p.PeerId > 0 && p.GetParent() == dimensionParent)
                .ToList();

            if (playersHere.Count == 0)
            {
                return;
            }

            var playerChunks = playersHere.Select(p => CellToChunk(WorldToCell(p.GlobalPosition))).ToList();
            var needed = new HashSet<Vector2I>();
            var neededByPeer = new Dictionary<Vector2I, HashSet<long>>();

            foreach (var player in playersHere)
            {
                var playerChunk = CellToChunk(WorldToCell(player.GlobalPosition));

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
                .OrderBy(c => playerChunks.Min(pc => Mathf.Max(Mathf.Abs(c.X - pc.X), Mathf.Abs(c.Y - pc.Y))))
                .Take(ChunkStreamingConstants.MAX_CHUNK_LOADS_PER_TICK);

            var missingList = missing.ToList();

            foreach (var chunkCoord in missingList)
            {
                var requestingPeers = neededByPeer.TryGetValue(chunkCoord, out var peers) ? peers : new HashSet<long>();

                await LoadChunkAsync(dimensionId, chunkCoord, loaded, state, loadedPeers, requestingPeers);
            }

            var toUnload = new List<Vector2I>();

            foreach (var chunkCoord in loaded)
            {
                var withinUnloadRadius = playerChunks.Any(playerChunk =>
                {
                    var distance = Mathf.Max(Mathf.Abs(chunkCoord.X - playerChunk.X), Mathf.Abs(chunkCoord.Y - playerChunk.Y));

                    return distance <= ChunkStreamingConstants.UNLOAD_RADIUS_CHUNKS;
                });

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

        private Vector2I WorldToCell(Vector2 globalPosition)
        {
            return new Vector2I(
                Mathf.FloorToInt(globalPosition.X / TileSize),
                Mathf.FloorToInt(globalPosition.Y / TileSize));
        }

        private static Vector2I CellToChunk(Vector2I cell)
        {
            return new Vector2I(
                Mathf.FloorToInt(cell.X / (float)ChunkStreamingConstants.CHUNK_SIZE),
                Mathf.FloorToInt(cell.Y / (float)ChunkStreamingConstants.CHUNK_SIZE));
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
            var centerChunk = CellToChunk(WorldToCell(worldPosition));
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
            var state = ResolveState(dimensionId);
            var chunkCoord = CellToChunk(cell);

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
            if (layer == OverworldLayer)
            {
                origin = _discoveredOverworld.Origin;

                return _discoveredOverworld.GetTexture();
            }

            if (layer == UpsidedownLayer)
            {
                origin = _discoveredUpsidedown.Origin;

                return _discoveredUpsidedown.GetTexture();
            }

            origin = Vector2I.Zero;

            return null;
        }

        // Ponto único de resolução de layer: Base/Compose sempre existem por padrão (pré-criadas
        // em Overworld.tscn/Upsidedown.tscn, com TileSet e script já atribuídos), então aqui é só
        // resolver e cachear - nunca cria layer em runtime.
        public TerrainLayer ResolveLayer(string dimensionId)
        {
            var cached = dimensionId == ChunkStreamingConstants.OVERWORLD_ID ? OverworldLayer : UpsidedownLayer;

            if (cached != null && IsInstanceValid(cached))
            {
                return cached;
            }

            var dimensionParent = WorldManager?.ResolveDimensionParent(dimensionId);
            var layer = dimensionParent?.GetNodeOrNull<TerrainLayer>(ChunkStreamingConstants.PROCEDURAL_LAYER_NAME);
            var baseLayer = dimensionParent?.GetNodeOrNull<TerrainLayer>(ChunkStreamingConstants.PROCEDURAL_BASE_LAYER_NAME);

            if (dimensionId == ChunkStreamingConstants.OVERWORLD_ID)
            {
                OverworldLayer = layer;
                OverworldBaseLayer = baseLayer;
            }
            else
            {
                UpsidedownLayer = layer;
                UpsidedownBaseLayer = baseLayer;
            }

            return layer;
        }

        public TerrainLayer ResolveBaseLayer(string dimensionId)
        {
            ResolveLayer(dimensionId);

            return dimensionId == ChunkStreamingConstants.OVERWORLD_ID ? OverworldBaseLayer : UpsidedownBaseLayer;
        }

        public BiomeDefinition ResolveBiome(string dimensionId, int worldX, int worldY)
        {
            return BiomeDB.Get(_generator.GetBiomeIdAtPosition(WorldSeed, dimensionId, worldX, worldY));
        }

        private async Task LoadChunkAsync(string dimensionId, Vector2I chunkCoord, HashSet<Vector2I> loaded, Dictionary<Vector2I, ChunkStateData> state, Dictionary<Vector2I, HashSet<long>> loadedPeers, HashSet<long> requestingPeers)
        {
            var layer = ResolveLayer(dimensionId);
            var baseLayer = ResolveBaseLayer(dimensionId);

            loaded.Add(chunkCoord);

            await _generator.PaintTilesAsync(layer, baseLayer, WorldSeed, dimensionId, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);

            if (!state.TryGetValue(chunkCoord, out var chunkState))
            {
                chunkState = new ChunkStateData();
                state[chunkCoord] = chunkState;
            }

            ApplyMutations(layer, chunkState, dimensionId);
            RecordDiscovered(dimensionId, layer, chunkCoord);

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
        }

        private void ApplyMutations(TerrainLayer layer, ChunkStateData chunkState, string dimensionId)
        {
            foreach (var mutation in chunkState.Mutations)
            {
                WorldManager?.ApplyChunkMutation(layer, mutation, dimensionId);
            }
        }

        private void RecordDiscovered(string dimensionId, TileMapLayer layer, Vector2I chunkCoord)
        {
            var discovered = dimensionId == ChunkStreamingConstants.OVERWORLD_ID ? _discoveredOverworld : _discoveredUpsidedown;
            var baseCellX = chunkCoord.X * ChunkStreamingConstants.CHUNK_SIZE;
            var baseCellY = chunkCoord.Y * ChunkStreamingConstants.CHUNK_SIZE;

            for (int localX = 0; localX < ChunkStreamingConstants.CHUNK_SIZE; localX++)
            {
                for (int localY = 0; localY < ChunkStreamingConstants.CHUNK_SIZE; localY++)
                {
                    var cell = new Vector2I(baseCellX + localX, baseCellY + localY);

                    if (layer.GetCellSourceId(cell) != -1)
                    {
                        discovered.SetCell(cell, new Color(0.4f, 0.4f, 0.45f, 1f));
                    }
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

            var layer = ResolveLayer(dimensionId);
            var baseLayer = ResolveBaseLayer(dimensionId);

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
            var dimensionParent = WorldManager?.ResolveDimensionParent(dimensionId);
            var loaded = ResolveLoaded(dimensionId);

            if (dimensionParent == null || loaded.Contains(chunkCoord))
            {
                return;
            }

            var layer = ResolveLayer(dimensionId);
            var baseLayer = ResolveBaseLayer(dimensionId);

            loaded.Add(chunkCoord);

            await _generator.PaintTilesAsync(layer, baseLayer, WorldSeed, dimensionId, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);

            var chunkState = GodotDictionaryParser.ToResource<ChunkStateData>(stateDict);

            ApplyMutations(layer, chunkState, dimensionId);
            RecordDiscovered(dimensionId, layer, chunkCoord);

            ResolveState(dimensionId)[chunkCoord] = chunkState;
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public async void UnloadChunkReceive(string dimensionId, Vector2I chunkCoord)
        {
            var loaded = ResolveLoaded(dimensionId);

            if (!loaded.Remove(chunkCoord))
            {
                return;
            }

            var dimensionParent = WorldManager?.ResolveDimensionParent(dimensionId);

            if (dimensionParent == null)
            {
                return;
            }

            var layer = ResolveLayer(dimensionId);
            var baseLayer = ResolveBaseLayer(dimensionId);

            await _generator.EraseTilesAsync(layer, baseLayer, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);
        }

        #endregion

        #region Core - Peer catch-up

        public void CatchUpPeer(long targetPeerId)
        {
            SetWorldSeedRequest(targetPeerId);

            CatchUpDimension(ChunkStreamingConstants.OVERWORLD_ID, _loadedOverworld, _overworldState, _overworldLoadedPeers, targetPeerId);
            CatchUpDimension(ChunkStreamingConstants.UPSIDEDOWN_ID, _loadedUpsidedown, _upsidedownState, _upsidedownLoadedPeers, targetPeerId);
        }

        private void SetWorldSeedRequest(long targetPeerId)
        {
            RpcId(targetPeerId, nameof(SetWorldSeedReceive), WorldSeed);
        }

        private void CatchUpDimension(string dimensionId, HashSet<Vector2I> loaded, Dictionary<Vector2I, ChunkStateData> state, Dictionary<Vector2I, HashSet<long>> loadedPeers, long targetPeerId)
        {
            foreach (var chunkCoord in loaded)
            {
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

        public DimensionSaveData ExportState(string dimensionId)
        {
            var state = ResolveState(dimensionId);
            var save = new DimensionSaveData();

            foreach (var (chunkCoord, chunkState) in state)
            {
                save.Chunks.Add(new ChunkEntryData
                {
                    ChunkCoordX = chunkCoord.X,
                    ChunkCoordY = chunkCoord.Y,
                    State = chunkState,
                });
            }

            return save;
        }

        public void ImportState(string dimensionId, DimensionSaveData save)
        {
            var state = ResolveState(dimensionId);

            state.Clear();

            if (save == null)
            {
                return;
            }

            foreach (var entry in save.Chunks)
            {
                state[new Vector2I(entry.ChunkCoordX, entry.ChunkCoordY)] = entry.State ?? new ChunkStateData();
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
            _discoveredOverworld.Reset();
            _discoveredUpsidedown.Reset();
            OverworldLayer = null;
            UpsidedownLayer = null;
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
