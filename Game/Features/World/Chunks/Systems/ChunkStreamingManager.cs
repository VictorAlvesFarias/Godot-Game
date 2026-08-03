using Godot;
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
        public int TileSize { get; set; } = 32;
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

        #region Node references

        public WorldManager WorldManager { get; set; }

        #endregion

        #region Node children references

        public TileMapLayer OverworldLayer { get; set; }
        public TileMapLayer UpsidedownLayer { get; set; }
        public TileMapLayer OverworldEdgeFillLayer { get; set; }
        public TileMapLayer UpsidedownEdgeFillLayer { get; set; }

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

            Evaluate(ChunkStreamingConstants.OVERWORLD_ID, WorldManager.OverworldParent, _loadedOverworld, _overworldState, _overworldLoadedPeers);
            Evaluate(ChunkStreamingConstants.UPSIDEDOWN_ID, WorldManager.UpsidedownParent, _loadedUpsidedown, _upsidedownState, _upsidedownLoadedPeers);
        }

        #endregion

        #region Core - Evaluation (load/unload decision)

        private bool IsServerAuthoritative()
        {
            return Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();
        }

        private void Evaluate(string dimensionId, Node2D dimensionParent, HashSet<Vector2I> loaded, Dictionary<Vector2I, ChunkStateData> state, Dictionary<Vector2I, HashSet<long>> loadedPeers)
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

            foreach (var chunkCoord in missing)
            {
                var requestingPeers = neededByPeer.TryGetValue(chunkCoord, out var peers) ? peers : new HashSet<long>();

                LoadChunk(dimensionId, dimensionParent, chunkCoord, loaded, state, loadedPeers, requestingPeers);
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
                UnloadChunk(dimensionId, dimensionParent, chunkCoord, loaded, state, loadedPeers);
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
            var centerChunk = CellToChunk(WorldToCell(worldPosition));
            var ownPeerId = Multiplayer != null && Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 1;

            for (int dx = -ChunkStreamingConstants.LOAD_RADIUS_CHUNKS; dx <= ChunkStreamingConstants.LOAD_RADIUS_CHUNKS; dx++)
            {
                for (int dy = -ChunkStreamingConstants.LOAD_RADIUS_CHUNKS; dy <= ChunkStreamingConstants.LOAD_RADIUS_CHUNKS; dy++)
                {
                    var chunkCoord = centerChunk + new Vector2I(dx, dy);

                    if (!loaded.Contains(chunkCoord))
                    {
                        LoadChunk(dimensionId, dimensionParent, chunkCoord, loaded, state, loadedPeers, new HashSet<long> { ownPeerId });

                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    }
                }
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

        #endregion

        #region Core - Load/Unload (server-side)

        private void LoadChunk(string dimensionId, Node2D dimensionParent, Vector2I chunkCoord, HashSet<Vector2I> loaded, Dictionary<Vector2I, ChunkStateData> state, Dictionary<Vector2I, HashSet<long>> loadedPeers, HashSet<long> requestingPeers)
        {
            var layer = GetOrCreateLayer(dimensionId, dimensionParent);
            var edgeFillLayer = GetEdgeFillLayer(dimensionId);

            ChunkGenerator.Paint(layer, edgeFillLayer, WorldSeed, dimensionId, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);

            if (!state.TryGetValue(chunkCoord, out var chunkState))
            {
                chunkState = new ChunkStateData();
                state[chunkCoord] = chunkState;
            }

            ApplyMutations(layer, chunkState, dimensionId);
            RecordDiscovered(dimensionId, layer, chunkCoord);

            loaded.Add(chunkCoord);
            loadedPeers[chunkCoord] = new HashSet<long>(requestingPeers);

            var stateDict = GodotDictionaryParser.ToDictionary(chunkState);
            var ownPeerId = Multiplayer != null && Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 1;

            foreach (var peerId in requestingPeers)
            {
                if (peerId == ownPeerId)
                {
                    continue;
                }

                BroadcastLoadChunk(peerId, dimensionId, chunkCoord, stateDict);
            }
        }

        private void UnloadChunk(string dimensionId, Node2D dimensionParent, Vector2I chunkCoord, HashSet<Vector2I> loaded, Dictionary<Vector2I, ChunkStateData> state, Dictionary<Vector2I, HashSet<long>> loadedPeers)
        {
            if (!loaded.Remove(chunkCoord))
            {
                return;
            }

            // "state" (mutacoes de bloco quebrado/colocado) NAO e limpo
            // aqui de proposito - precisa sobreviver ao descarregamento pra
            // ser reaplicado da proxima vez que esse chunk carregar (seja
            // pelo mesmo player voltando, seja por outro peer chegando
            // perto), senao um buraco cavado sumia assim que o chunk saia
            // de raio e voltava do jeito gerado original.
            var layer = GetOrCreateLayer(dimensionId, dimensionParent);
            var edgeFillLayer = GetEdgeFillLayer(dimensionId);

            ChunkGenerator.Erase(layer, edgeFillLayer, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);

            if (loadedPeers.TryGetValue(chunkCoord, out var peers))
            {
                var ownPeerId = Multiplayer != null && Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 1;

                foreach (var peerId in peers)
                {
                    if (peerId != ownPeerId)
                    {
                        BroadcastUnloadChunk(peerId, dimensionId, chunkCoord);
                    }
                }

                loadedPeers.Remove(chunkCoord);
            }
        }

        private void ApplyMutations(TileMapLayer layer, ChunkStateData chunkState, string dimensionId)
        {
            foreach (var mutation in chunkState.Mutations)
            {
                WorldManager?.ApplyChunkMutation(layer, mutation, dimensionId);
            }
        }

        // Chamado pelo WorldManager sempre que um bloco quebra/e colocado
        // de verdade (autoritativo) - guarda a mutacao no ChunkStateData
        // do chunk correspondente, pra ser reaplicada (ver ApplyMutations)
        // toda vez que esse chunk for (re)pintado, incluindo pra peers que
        // entram depois da mutacao ja ter acontecido.
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

        private TileMapLayer GetOrCreateLayer(string dimensionId, Node2D dimensionParent)
        {
            var existing = dimensionId == ChunkStreamingConstants.OVERWORLD_ID ? OverworldLayer : UpsidedownLayer;

            if (existing != null && IsInstanceValid(existing))
            {
                return existing;
            }

            var layer = new TileMapLayer
            {
                Name = ChunkStreamingConstants.PROCEDURAL_LAYER_NAME,
                TileSet = ChunkGenerator.GetTileSet(),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };

            dimensionParent.AddChild(layer);

            // Camada so pros tiles que encostam em um tileset DIFERENTE - fica por cima da
            // camada de chao (adicionada depois, como irmao seguinte) com o shader de
            // preenchimento de buraco. So essa celula-copia leva o shader, o resto do chao fica
            // intocado.
            var edgeFillLayer = new TileMapLayer
            {
                Name = ChunkStreamingConstants.PROCEDURAL_EDGE_FILL_LAYER_NAME,
                TileSet = ChunkGenerator.GetTileSet(),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Material = GD.Load<ShaderMaterial>(Textures.Tiles.TILE_EDGE_FILL_MATERIAL),
            };

            dimensionParent.AddChild(edgeFillLayer);

            if (dimensionId == ChunkStreamingConstants.OVERWORLD_ID)
            {
                OverworldLayer = layer;
                OverworldEdgeFillLayer = edgeFillLayer;
            }
            else
            {
                UpsidedownLayer = layer;
                UpsidedownEdgeFillLayer = edgeFillLayer;
            }

            return layer;
        }

        private TileMapLayer GetEdgeFillLayer(string dimensionId)
        {
            return dimensionId == ChunkStreamingConstants.OVERWORLD_ID ? OverworldEdgeFillLayer : UpsidedownEdgeFillLayer;
        }

        private void BroadcastLoadChunk(long peerId, string dimensionId, Vector2I chunkCoord, Godot.Collections.Dictionary stateDict)
        {
            if (!IsPeerConnected(peerId))
            {
                return;
            }

            RpcId(peerId, nameof(LoadChunkReceive), dimensionId, chunkCoord, stateDict);
        }

        private void BroadcastUnloadChunk(long peerId, string dimensionId, Vector2I chunkCoord)
        {
            if (!IsPeerConnected(peerId))
            {
                return;
            }

            RpcId(peerId, nameof(UnloadChunkReceive), dimensionId, chunkCoord);
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

        #endregion

        #region Core - Rpc - Chunks

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SetWorldSeedReceive(long seed)
        {
            WorldSeed = seed;
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void LoadChunkReceive(string dimensionId, Vector2I chunkCoord, Godot.Collections.Dictionary stateDict)
        {
            var dimensionParent = ResolveDimensionParent(dimensionId);
            var loaded = ResolveLoaded(dimensionId);

            if (dimensionParent == null || loaded.Contains(chunkCoord))
            {
                return;
            }

            var layer = GetOrCreateLayer(dimensionId, dimensionParent);
            var edgeFillLayer = GetEdgeFillLayer(dimensionId);

            ChunkGenerator.Paint(layer, edgeFillLayer, WorldSeed, dimensionId, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);

            var chunkState = GodotDictionaryParser.ToResource<ChunkStateData>(stateDict);

            ApplyMutations(layer, chunkState, dimensionId);
            RecordDiscovered(dimensionId, layer, chunkCoord);

            loaded.Add(chunkCoord);

            ResolveState(dimensionId)[chunkCoord] = chunkState;
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void UnloadChunkReceive(string dimensionId, Vector2I chunkCoord)
        {
            var loaded = ResolveLoaded(dimensionId);

            if (!loaded.Remove(chunkCoord))
            {
                return;
            }

            var dimensionParent = ResolveDimensionParent(dimensionId);

            if (dimensionParent == null)
            {
                return;
            }

            var layer = GetOrCreateLayer(dimensionId, dimensionParent);
            var edgeFillLayer = GetEdgeFillLayer(dimensionId);

            ChunkGenerator.Erase(layer, edgeFillLayer, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);
        }

        #endregion

        #region Core - Peer catch-up

        public void CatchUpPeer(long targetPeerId)
        {
            RpcId(targetPeerId, nameof(SetWorldSeedReceive), WorldSeed);

            CatchUpDimension(ChunkStreamingConstants.OVERWORLD_ID, _loadedOverworld, _overworldState, _overworldLoadedPeers, targetPeerId);
            CatchUpDimension(ChunkStreamingConstants.UPSIDEDOWN_ID, _loadedUpsidedown, _upsidedownState, _upsidedownLoadedPeers, targetPeerId);
        }

        private void CatchUpDimension(string dimensionId, HashSet<Vector2I> loaded, Dictionary<Vector2I, ChunkStateData> state, Dictionary<Vector2I, HashSet<long>> loadedPeers, long targetPeerId)
        {
            foreach (var chunkCoord in loaded)
            {
                var chunkState = state.TryGetValue(chunkCoord, out var s) ? s : new ChunkStateData();

                RpcId(targetPeerId, nameof(LoadChunkReceive), dimensionId, chunkCoord, GodotDictionaryParser.ToDictionary(chunkState));

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

        private Node2D ResolveDimensionParent(string dimensionId)
        {
            return dimensionId == ChunkStreamingConstants.OVERWORLD_ID ? WorldManager?.OverworldParent : WorldManager?.UpsidedownParent;
        }

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
