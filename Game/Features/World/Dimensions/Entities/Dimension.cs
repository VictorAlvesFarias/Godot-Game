using Godot;
using Jogo25D.Biomes;
using Jogo25D.Blocks;
using Jogo25D.Characters;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Entities;
using Jogo25D.Features.World.Chunks.Resources;
using Jogo25D.Light;
using Jogo25D.Save;
using Jogo25D.Systems;
using Jogo25D.Utils.Coordinates;
using Jogo25D.Utils.GodotDictionaryParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jogo25D.Dimensions
{
    public partial class Dimension : Node2D, ISaveState
    {
        #region Events

        public event Action<Vector2I> ChunkLoaded;
        public event Action<Vector2I> ChunkUnloaded;

        #endregion

        #region Dinamic properties

        private const int TerrainBatchLimit = 3072;
        private const int BackgroundBatchLimit = 2048;

        public virtual string DimensionId => ChunkStreamingConstants.UPSIDEDOWN_ID;

        private readonly HashSet<Vector2I> _loaded = new();
        private readonly Dictionary<Vector2I, ChunkStateData> _state = new();
        private readonly Dictionary<Vector2I, HashSet<long>> _loadedPeers = new();
        private readonly ChunkGeneratorSystem _generator = new();
        private readonly MinimapSystem _minimap = new();

        private LogicalLightWorld _world;
        private bool _evaluating;

        public LogicalLightWorld World => _world;

        private static readonly Dictionary<string, Dimension> Registry = new();

        public static IEnumerable<string> Ids => Registry.Keys;

        public static IEnumerable<Dimension> All => Registry.Values;

        public static bool IsResolved => Registry.Count > 0 && Registry.Values.All(IsInstanceValid);

        public static int TileSize => Get(ChunkStreamingConstants.OVERWORLD_ID)?.Layer?.TileSet?.TileSize.X
            ?? Get(ChunkStreamingConstants.UPSIDEDOWN_ID)?.Layer?.TileSet?.TileSize.X
            ?? ChunkStreamingConstants.REFERENCE_TILE_SIZE;

        #endregion

        #region Configuracao vinda de fora

        public long Seed { get; set; }

        public bool Procedural { get; set; } = true;

        public bool TileStreaming { get; set; }

        #endregion

        #region Node references

        private TerrainLayer _layer;
        private TerrainLayer _baseLayer;
        private BackgroundWallLayer _walls;
        private Node2D _entities;
        private SubViewportContainer _container;

        public TerrainLayer Layer => _layer;

        public TerrainLayer BaseLayer => _baseLayer;

        public BackgroundWallLayer Walls => _walls;

        public Node2D Entities => _entities;

        private void ResolveChildren()
        {
            _layer = GetNodeOrNull<TerrainLayer>(ChunkStreamingConstants.PROCEDURAL_LAYER_NAME);
            _baseLayer = GetNodeOrNull<TerrainLayer>(ChunkStreamingConstants.PROCEDURAL_BASE_LAYER_NAME);
            _walls = GetNodeOrNull<BackgroundWallLayer>("BackgroundWalls");
            _entities = GetNodeOrNull<Node2D>("Entities");
            _container = GetParent()?.GetParent() as SubViewportContainer;

            if (_layer == null)
            {
                GD.PushError($"[Dimension.ResolveChildren] '{DimensionId}' sem {ChunkStreamingConstants.PROCEDURAL_LAYER_NAME}");
            }

            if (_entities == null)
            {
                GD.PushError($"[Dimension.ResolveChildren] '{DimensionId}' sem Entities");
            }
        }

        #endregion

        #region Godot implementation

        public override void _EnterTree()
        {
            Registry[DimensionId] = this;
        }

        public override void _Ready()
        {
            ResolveChildren();
        }

        public override void _ExitTree()
        {
            if (Registry.TryGetValue(DimensionId, out var current) && current == this)
            {
                Registry.Remove(DimensionId);
            }
        }

        #endregion

        #region Core - Registro

        public static Dimension Get(string dimensionId)
        {
            return dimensionId != null && Registry.TryGetValue(dimensionId, out var dimension) && IsInstanceValid(dimension)
                ? dimension
                : null;
        }

        public static string IdOf(Node node)
        {
            if (node != null)
            {
                foreach (var (dimensionId, dimension) in Registry)
                {
                    if (IsInstanceValid(dimension) && dimension.IsAncestorOf(node))
                    {
                        return dimensionId;
                    }
                }
            }

            return ChunkStreamingConstants.UPSIDEDOWN_ID;
        }

        public static void ShowOnly(string dimensionId)
        {
            foreach (var (currentId, dimension) in Registry)
            {
                if (dimension._container != null && IsInstanceValid(dimension._container))
                {
                    dimension._container.Visible = currentId == dimensionId;
                }
            }
        }

        #endregion

        #region Core - Limpeza da cena

        public static void ClearAllEntities()
        {
            foreach (var dimension in Registry.Values)
            {
                dimension.ClearEntities();
            }
        }

        public static void ClearAllLayers(bool discardBackground = false)
        {
            foreach (var dimension in Registry.Values)
            {
                dimension.ClearLayers(discardBackground);
            }
        }

        public void ClearEntities()
        {
            if (_entities == null)
            {
                return;
            }

            RemoveChild(_entities);

            _entities.Name = "EntitiesDiscarded";
            _entities.QueueFree();

            _entities = new Node2D { Name = "Entities" };

            AddChild(_entities);
        }

        public void ClearLayers(bool discardBackground = false)
        {
            if (discardBackground)
            {
                _walls?.ResetForNewWorld();
            }
            else
            {
                _walls?.ClearRenderedForStreaming();
            }

            _baseLayer?.Clear();
            _layer?.Clear();
        }

        #endregion

        #region Core - Posicionamento

        public Vector2 FindGroundSpawnPosition(float worldX, float halfBodyHeight = 15f)
        {
            if (_layer == null || _layer.TileSet == null)
            {
                return new Vector2(worldX, 0f);
            }

            var tileSize = _layer.TileSet.TileSize.X;
            var startCell = _layer.LocalToMap(_layer.ToLocal(new Vector2(worldX, -2000f)));
            var endCell = _layer.LocalToMap(_layer.ToLocal(new Vector2(worldX, 4000f)));

            for (int y = startCell.Y; y <= endCell.Y; y++)
            {
                var cell = new Vector2I(startCell.X, y);

                if (_layer.GetCellSourceId(cell) == -1)
                {
                    continue;
                }

                var cellTop = _layer.ToGlobal(_layer.MapToLocal(cell)).Y - tileSize / 2f;

                return new Vector2(worldX, cellTop - halfBodyHeight);
            }

            return new Vector2(worldX, 0f);
        }

        #endregion

        #region Core - Mundo logico

        public LogicalLightWorld EnsureWorld()
        {
            var seed = Seed;
            var referenceSize = ChunkStreamingConstants.REFERENCE_TILE_SIZE;
            var scale = Mathf.Max(1, Mathf.RoundToInt((float)referenceSize / TileSize));
            var procedural = Procedural;

            if (_world != null && _world.Seed == seed && _world.WorldScale == scale)
            {
                return _world;
            }

            _world = new LogicalLightWorld(seed, DimensionId, scale, procedural);

            if (_walls != null)
            {
                foreach (var cell in _walls.LogicalCells)
                {
                    _world.SetBackground(cell.X, cell.Y, true);
                }
            }

            return _world;
        }

        public void ReplaceWorld(bool procedural)
        {
            var current = EnsureWorld();

            _world = new LogicalLightWorld(current.Seed, DimensionId, current.WorldScale, procedural);
        }

        public void LoadAuthoredWorld()
        {
            _world = null;

            var world = EnsureWorld();

            LoadAuthoredLayer(world, Layer);
            LoadAuthoredLayer(world, BaseLayer);
        }

        public void DropWorld()
        {
            _world = null;
        }

        public void ApplyLightMutation(Vector2I cell, string type, string blockId = "")
        {
            EnsureWorld().ApplyMutation(cell.X, cell.Y, type, blockId);
        }

        private static void LoadAuthoredLayer(LogicalLightWorld world, TileMapLayer layer)
        {
            if (layer == null)
            {
                return;
            }

            foreach (var cell in layer.GetUsedCells())
            {
                world.SetTerrain(cell.X, cell.Y, LogicalLightWorld.TileTerrain(layer, cell));
            }
        }

        #endregion

        #region Core - Avaliacao

        public void Evaluate()
        {
            if (_evaluating || !IsServerAuthoritative())
            {
                return;
            }

            _evaluating = true;

            _ = EvaluateAsync();
        }

        private async Task EvaluateAsync()
        {
            try
            {
                await EvaluateCore();
            }
            finally
            {
                _evaluating = false;
            }
        }

        private async Task EvaluateCore()
        {
            var playersHere = Players
                .InDimension(DimensionId)
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
                .Where(c => !_loaded.Contains(c))
                .OrderBy(c => playerChunks.Min(pc => CoordinateUtilities.ChunkDistance(c, pc)))
                .Take(ChunkStreamingConstants.MAX_CHUNK_LOADS_PER_TICK)
                .ToList();

            foreach (var chunkCoord in missing)
            {
                var requestingPeers = neededByPeer.TryGetValue(chunkCoord, out var peers) ? peers : new HashSet<long>();

                await LoadChunkAsync(chunkCoord, requestingPeers);
            }

            SendPendingChunksToPeers(needed, neededByPeer);

            var toUnload = new List<Vector2I>();

            foreach (var chunkCoord in _loaded)
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
                await UnloadChunkAsync(chunkCoord);
            }
        }

        private void SendPendingChunksToPeers(HashSet<Vector2I> needed, Dictionary<Vector2I, HashSet<long>> neededByPeer)
        {
            var ownPeerId = OwnPeerId();

            foreach (var chunkCoord in needed)
            {
                if (!_loaded.Contains(chunkCoord) || !neededByPeer.TryGetValue(chunkCoord, out var wanted))
                {
                    continue;
                }

                if (!_loadedPeers.TryGetValue(chunkCoord, out var have))
                {
                    have = new HashSet<long>();
                    _loadedPeers[chunkCoord] = have;
                }

                Godot.Collections.Dictionary stateDict = null;

                foreach (var peerId in wanted)
                {
                    if (peerId == ownPeerId || have.Contains(peerId))
                    {
                        continue;
                    }

                    stateDict ??= GodotDictionaryParser.ToDictionary(
                        _state.TryGetValue(chunkCoord, out var chunkState) ? chunkState : new ChunkStateData());

                    LoadChunkRequest(peerId, chunkCoord, stateDict);

                    have.Add(peerId);
                }
            }
        }

        public async Task PreloadSpawnAreaAsync(Vector2 worldPosition)
        {
            var centerChunk = CoordinateUtilities.WorldToChunk(worldPosition, TileSize);
            var ownPeerId = OwnPeerId();

            for (int dx = -ChunkStreamingConstants.LOAD_RADIUS_CHUNKS; dx <= ChunkStreamingConstants.LOAD_RADIUS_CHUNKS; dx++)
            {
                for (int dy = -ChunkStreamingConstants.LOAD_RADIUS_CHUNKS; dy <= ChunkStreamingConstants.LOAD_RADIUS_CHUNKS; dy++)
                {
                    var chunkCoord = centerChunk + new Vector2I(dx, dy);

                    if (!_loaded.Contains(chunkCoord))
                    {
                        await LoadChunkAsync(chunkCoord, new HashSet<long> { ownPeerId });
                    }
                }
            }
        }

        #endregion

        #region Core - Carga e descarga

        private async Task LoadChunkAsync(Vector2I chunkCoord, HashSet<long> requestingPeers)
        {
            var layer = Layer;

            _loaded.Add(chunkCoord);

            await _generator.PaintTilesAsync(layer, BaseLayer, Seed, DimensionId, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);

            if (!_state.TryGetValue(chunkCoord, out var chunkState))
            {
                chunkState = new ChunkStateData();
                _state[chunkCoord] = chunkState;
            }

            ApplyMutations(layer, chunkState);

            _minimap.RecordChunk(DimensionId, layer, chunkCoord);

            _loadedPeers[chunkCoord] = new HashSet<long>(requestingPeers);

            var stateDict = GodotDictionaryParser.ToDictionary(chunkState);
            var ownPeerId = OwnPeerId();

            foreach (var peerId in requestingPeers)
            {
                if (peerId != ownPeerId)
                {
                    LoadChunkRequest(peerId, chunkCoord, stateDict);
                }
            }

            ChunkLoaded?.Invoke(chunkCoord);
        }

        private async Task UnloadChunkAsync(Vector2I chunkCoord)
        {
            if (!_loaded.Remove(chunkCoord))
            {
                return;
            }

            await _generator.EraseTilesAsync(Layer, BaseLayer, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);

            if (_loadedPeers.TryGetValue(chunkCoord, out var peers))
            {
                var ownPeerId = OwnPeerId();

                foreach (var peerId in peers)
                {
                    if (peerId != ownPeerId)
                    {
                        UnloadChunkRequest(peerId, chunkCoord);
                    }
                }

                _loadedPeers.Remove(chunkCoord);
            }

            ChunkUnloaded?.Invoke(chunkCoord);
        }

        private static void ApplyMutations(TerrainLayer layer, ChunkStateData chunkState)
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

        #endregion

        #region Core - Mutacoes

        public void RecordMutation(Vector2I cell, string type, string extraData)
        {
            ApplyLightMutation(cell, type, extraData);

            var chunkCoord = CoordinateUtilities.CellToChunk(cell);

            if (!_state.TryGetValue(chunkCoord, out var chunkState))
            {
                chunkState = new ChunkStateData();
                _state[chunkCoord] = chunkState;
            }

            chunkState.Mutations.Add(new ChunkMutationData
            {
                Type = type,
                Position = new Vector2(cell.X, cell.Y),
                ExtraData = extraData ?? "",
            });
        }

        public void WriteState(Godot.Collections.Dictionary state)
        {
            state["mutations"] = ExportMutations();
        }

        public void ReadState(Godot.Collections.Dictionary state)
        {
            if (state.TryGetValue("mutations", out var value))
            {
                ImportMutations(value.AsGodotArray());
            }
        }

        private Godot.Collections.Array ExportMutations()
        {
            var list = new Godot.Collections.Array();

            foreach (var (_, chunkState) in _state)
            {
                foreach (var mutation in chunkState.Mutations)
                {
                    list.Add(new Godot.Collections.Dictionary
                    {
                        { "type", mutation.Type },
                        { "x", (int)mutation.Position.X },
                        { "y", (int)mutation.Position.Y },
                        { "blockId", mutation.ExtraData },
                    });
                }
            }

            return list;
        }

        private void ImportMutations(Godot.Collections.Array list)
        {
            _state.Clear();

            if (list == null)
            {
                return;
            }

            foreach (var raw in list)
            {
                var mutation = raw.AsGodotDictionary();
                var cell = new Vector2I(mutation["x"].AsInt32(), mutation["y"].AsInt32());
                var chunk = CoordinateUtilities.CellToChunk(cell);

                ApplyLightMutation(cell, mutation["type"].AsString(),
                    mutation.TryGetValue("blockId", out var opticalBlock) ? opticalBlock.AsString() : "");

                if (!_state.TryGetValue(chunk, out var chunkState))
                {
                    chunkState = new ChunkStateData();
                    _state[chunk] = chunkState;
                }

                var restoredMutation = new ChunkMutationData
                {
                    Type = mutation["type"].AsString(),
                    Position = new Vector2(cell.X, cell.Y),
                    ExtraData = mutation.TryGetValue("blockId", out var b) ? b.AsString() : "",
                };

                chunkState.Mutations.Add(restoredMutation);

                if (restoredMutation.Type is "wall_place" or "wall_break")
                {
                    _walls?.ApplyMutation(restoredMutation);
                }
            }
        }

        #endregion

        #region Core - Consulta

        public BiomeDefinition ResolveBiome(int worldX, int worldY)
        {
            return BiomeDB.Get(_generator.GetBiomeIdAtPosition(Seed, DimensionId, worldX, worldY));
        }

        public Texture2D GetDiscoveredTexture(out Vector2I origin)
        {
            return _minimap.GetTexture(DimensionId, out origin);
        }

        #endregion

        #region Core - Peers

        public void CatchUpTo(long targetPeerId, Vector2I aroundChunk)
        {
            SendLightWorld(targetPeerId);

            foreach (var chunkCoord in _loaded)
            {
                if (CoordinateUtilities.ChunkDistance(chunkCoord, aroundChunk) > ChunkStreamingConstants.UNLOAD_RADIUS_CHUNKS)
                {
                    continue;
                }

                var chunkState = _state.TryGetValue(chunkCoord, out var s) ? s : new ChunkStateData();

                LoadChunkRequest(targetPeerId, chunkCoord, GodotDictionaryParser.ToDictionary(chunkState));

                if (!_loadedPeers.TryGetValue(chunkCoord, out var peers))
                {
                    peers = new HashSet<long>();
                    _loadedPeers[chunkCoord] = peers;
                }

                peers.Add(targetPeerId);
            }
        }

        public void RemovePeer(long peerId)
        {
            foreach (var peers in _loadedPeers.Values)
            {
                peers.Remove(peerId);
            }
        }

        public void ResetState()
        {
            _loaded.Clear();
            _state.Clear();
            _loadedPeers.Clear();
            _minimap.Reset();
        }

        private void SendLightWorld(long peerId)
        {
            var world = EnsureWorld();
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

                RpcId(peerId, nameof(ReceiveLightWorld), world.Procedural, reset, batch.ToArray());

                reset = false;

                batch.Clear();
            }

            if (reset || batch.Count > 0)
            {
                RpcId(peerId, nameof(ReceiveLightWorld), world.Procedural, reset, batch.ToArray());
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

                RpcId(peerId, nameof(ReceiveBackgroundLight), batch.ToArray());

                batch.Clear();
            }

            if (batch.Count > 0)
            {
                RpcId(peerId, nameof(ReceiveBackgroundLight), batch.ToArray());
            }
        }

        private void LoadChunkRequest(long peerId, Vector2I chunkCoord, Godot.Collections.Dictionary stateDict)
        {
            if (IsPeerConnected(peerId))
            {
                RpcId(peerId, nameof(LoadChunkReceive), chunkCoord, stateDict);
            }
        }

        private void UnloadChunkRequest(long peerId, Vector2I chunkCoord)
        {
            if (IsPeerConnected(peerId))
            {
                RpcId(peerId, nameof(UnloadChunkReceive), chunkCoord);
            }
        }

        private bool IsServerAuthoritative()
        {
            return Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();
        }

        private long OwnPeerId()
        {
            return Multiplayer != null && Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 1;
        }

        private bool IsPeerConnected(long peerId)
        {
            var multiplayer = Multiplayer;

            if (multiplayer == null || !multiplayer.HasMultiplayerPeer())
            {
                return false;
            }

            if (peerId == multiplayer.GetUniqueId())
            {
                return true;
            }

            foreach (var connectedId in multiplayer.GetPeers())
            {
                if (connectedId == peerId)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Core - Rpc - Chunks

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public async void LoadChunkReceive(Vector2I chunkCoord, Godot.Collections.Dictionary stateDict)
        {
            if (_loaded.Contains(chunkCoord))
            {
                return;
            }

            var layer = Layer;

            _loaded.Add(chunkCoord);

            var chunkState = GodotDictionaryParser.ToResource<ChunkStateData>(stateDict);

            foreach (var mutation in chunkState.Mutations)
            {
                ApplyLightMutation(
                    new Vector2I((int)mutation.Position.X, (int)mutation.Position.Y),
                    mutation.Type,
                    mutation.ExtraData);
            }

            await _generator.PaintTilesAsync(layer, BaseLayer, Seed, DimensionId, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);

            ApplyMutations(layer, chunkState);

            _minimap.RecordChunk(DimensionId, layer, chunkCoord);

            _state[chunkCoord] = chunkState;

            ChunkLoaded?.Invoke(chunkCoord);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public async void UnloadChunkReceive(Vector2I chunkCoord)
        {
            if (!_loaded.Remove(chunkCoord))
            {
                return;
            }

            await _generator.EraseTilesAsync(Layer, BaseLayer, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);

            ChunkUnloaded?.Invoke(chunkCoord);
        }

        #endregion

        #region Core - Rpc - Luz

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ReceiveLightWorld(bool procedural, bool reset, int[] edits)
        {
            if (reset)
            {
                ReplaceWorld(procedural);
            }

            var world = EnsureWorld();

            for (int i = 0; i + 2 < edits.Length; i += 3)
            {
                world.SetTerrain(edits[i], edits[i + 1], edits[i + 2]);
            }
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ReceiveBackgroundLight(int[] cells)
        {
            var world = EnsureWorld();

            for (int i = 0; i + 1 < cells.Length; i += 2)
            {
                world.SetBackground(cells[i], cells[i + 1], true);
            }
        }

        #endregion

        #region Core - Rpc - Entidades

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SpawnPlayerReceive(long peerId, Vector2 position, Godot.Collections.Dictionary data)
        {
            Players.ApplySpawn(peerId, position, data);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SpawnNpcReceive(Vector2 position)
        {
            Players.SpawnNpc(position);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SpawnReceive(Godot.Collections.Dictionary record)
        {
            EntitySpawner.Spawn(record);
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void DespawnReceive(long instanceId)
        {
            EntitySpawner.ApplyDespawn(instanceId);
        }

        #endregion
    }
}
