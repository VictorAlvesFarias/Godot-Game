using Godot;
using Jogo25D.Biomes;
using Jogo25D.Blocks;
using Jogo25D.Characters;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Entities;
using Jogo25D.Light;
using Jogo25D.Save;
using Jogo25D.Save.Structures;
using Jogo25D.Save.Types;
using Jogo25D.Systems;
using Jogo25D.Utils.Coordinates;
using Jogo25D.Utils.GodotDictionaryParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jogo25D.Dimensions
{
    public partial class Dimension : Node2D
    {
        #region Events

        public event Action<Vector2I> ChunkLoaded;
        public event Action<Vector2I> ChunkUnloaded;

        #endregion

        #region Dinamic properties

        public virtual string DimensionId => ChunkStreamingConstants.UPSIDEDOWN_ID;

        private readonly HashSet<Vector2I> _loaded = new();

        // Chunks ja pintados (com colisao): as entidades so voltam do banco para eles.
        private readonly HashSet<Vector2I> _painted = new();
        private readonly Dictionary<Vector2I, HashSet<long>> _loadedPeers = new();

        // Peers que tem so as edicoes do chunk (sem tiles): a margem da luz em volta dos chunks deles.
        private readonly Dictionary<Vector2I, HashSet<long>> _edgePeers = new();
        private readonly ChunkGeneratorSystem _generator = new();
        private readonly MinimapSystem _minimap = new();

        private TerrainCache _terrain;
        private bool _evaluating;

        public TerrainCache Terrain => _terrain;

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

        #region Core - Cache de terreno

        public TerrainCache EnsureTerrain()
        {
            var seed = Seed;
            var referenceSize = ChunkStreamingConstants.REFERENCE_TILE_SIZE;
            var scale = Mathf.Max(1, Mathf.RoundToInt((float)referenceSize / TileSize));
            var procedural = Procedural;

            if (_terrain != null && _terrain.Seed == seed && _terrain.WorldScale == scale)
            {
                return _terrain;
            }

            _terrain = new TerrainCache(seed, DimensionId, scale, procedural);

            if (_walls != null)
            {
                foreach (var cell in _walls.LogicalCells)
                {
                    _terrain.SetBackground(cell.X, cell.Y, true);
                }
            }

            return _terrain;
        }

        public void LoadAuthoredTerrain()
        {
            _terrain = null;

            var terrain = EnsureTerrain();

            LoadAuthoredLayer(terrain, Layer);
            LoadAuthoredLayer(terrain, BaseLayer);
        }

        public void DropTerrain()
        {
            _terrain = null;
        }

        // Edicao numa celula (blockId nulo = removido): vai para o cache, que marca o chunk como sujo para o save.
        public void ApplyEdit(Vector2I cell, TerrainLayerType layer, string blockId)
        {
            EnsureTerrain().ApplyEdit(cell.X, cell.Y, layer, blockId);
        }

        private static void LoadAuthoredLayer(TerrainCache terrain, TileMapLayer layer)
        {
            if (layer == null)
            {
                return;
            }

            foreach (var cell in layer.GetUsedCells())
            {
                terrain.SetTerrain(cell.X, cell.Y, TerrainCache.TileTerrain(layer, cell));
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

            var ownPeerId = OwnPeerId();

            foreach (var player in playersHere)
            {
                if (player.PeerId != ownPeerId)
                {
                    SendEdgeChunks(player.PeerId);
                }
            }

            ReleaseFarChunkData(playerChunks);
        }

        // Envia ao peer as edicoes (sem tiles) dos chunks da margem em volta dos chunks que ele tem, e esquece os que
        // sairam dela. O cliente aplica a mesma regra ao descarregar (ReleaseFarClientData).
        private void SendEdgeChunks(long peerId)
        {
            if (!Procedural || !IsPeerConnected(peerId))
            {
                return;
            }

            var tiles = _loadedPeers.Where(pair => pair.Value.Contains(peerId)).Select(pair => pair.Key).ToHashSet();
            var edge = new HashSet<Vector2I>();
            var margin = ChunkStreamingConstants.DATA_MARGIN_CHUNKS;

            foreach (var chunk in tiles)
            {
                for (var dx = -margin; dx <= margin; dx++)
                {
                    for (var dy = -margin; dy <= margin; dy++)
                    {
                        var neighbor = chunk + new Vector2I(dx, dy);

                        if (!tiles.Contains(neighbor))
                        {
                            edge.Add(neighbor);
                        }
                    }
                }
            }

            foreach (var (chunk, peers) in _edgePeers)
            {
                if (!edge.Contains(chunk))
                {
                    peers.Remove(peerId);
                }
            }

            foreach (var chunk in edge)
            {
                if (!_edgePeers.TryGetValue(chunk, out var peers))
                {
                    _edgePeers[chunk] = peers = new HashSet<long>();
                }

                if (!peers.Add(peerId))
                {
                    continue;
                }

                EnsureChunkData(chunk);

                RpcId(peerId, nameof(ReceiveChunkEdits), chunk, ChunkEditsPayload(chunk));
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

                Godot.Collections.Array payload = null;

                foreach (var peerId in wanted)
                {
                    if (peerId == ownPeerId || have.Contains(peerId))
                    {
                        continue;
                    }

                    payload ??= ChunkEditsPayload(chunkCoord);

                    LoadChunkRequest(peerId, chunkCoord, payload);

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

        #region Core - Residencia de dados

        // Edicoes do chunk e da margem da luz em volta dele, lidas do banco antes da pintura (servidor ou jogo solo).
        private void EnsureChunkDataAround(Vector2I chunkCoord)
        {
            if (!Procedural || !WorldStore.IsOpen || !IsServerAuthoritative())
            {
                return;
            }

            var margin = ChunkStreamingConstants.DATA_MARGIN_CHUNKS;

            for (var dx = -margin; dx <= margin; dx++)
            {
                for (var dy = -margin; dy <= margin; dy++)
                {
                    EnsureChunkData(chunkCoord + new Vector2I(dx, dy));
                }
            }
        }

        public void EnsureChunkData(Vector2I chunk)
        {
            if (!Procedural || !WorldStore.IsOpen)
            {
                return;
            }

            var terrain = EnsureTerrain();

            if (terrain.IsChunkLoaded(chunk))
            {
                return;
            }

            // Edicao feita antes do chunk vir do banco (rara: fora da area carregada) fica por cima do que estava salvo.
            var pending = terrain.HasChunkEdits(chunk) ? terrain.CellEditsOf(chunk) : null;

            terrain.LoadChunkEdits(chunk, WorldStore.ReadEdits(DimensionId, chunk));

            if (pending == null)
            {
                return;
            }

            foreach (var edit in pending)
            {
                terrain.ApplyEdit(edit.X, edit.Y, edit.Layer, edit.BlockId);
            }
        }

        // Solta da memoria (gravando antes, se mudou) as edicoes dos chunks alem da area e da margem de todos os jogadores.
        private void ReleaseFarChunkData(List<Vector2I> playerChunks)
        {
            if (!Procedural || !WorldStore.IsOpen || _terrain == null)
            {
                return;
            }

            var radius = ChunkStreamingConstants.UNLOAD_RADIUS_CHUNKS + ChunkStreamingConstants.DATA_MARGIN_CHUNKS;
            var far = _terrain.LoadedChunks
                .Where(chunk => playerChunks.All(playerChunk => CoordinateUtilities.ChunkDistance(chunk, playerChunk) > radius))
                .ToList();

            foreach (var chunk in far)
            {
                if (_terrain.IsDirty(chunk))
                {
                    WorldStore.SaveChunkEdits(DimensionId, _terrain, chunk);
                }

                _terrain.UnloadChunkEdits(chunk);
            }
        }

        #endregion

        #region Core - Carga e descarga

        private async Task LoadChunkAsync(Vector2I chunkCoord, HashSet<long> requestingPeers)
        {
            var layer = Layer;

            _loaded.Add(chunkCoord);

            EnsureChunkDataAround(chunkCoord);

            // O cache ja tem as edicoes do chunk: a pintura sai com elas, sem repetir mutacoes por cima.
            await _generator.PaintTilesAsync(layer, BaseLayer, Seed, DimensionId, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);

            if (_loaded.Contains(chunkCoord))
            {
                _painted.Add(chunkCoord);
            }

            _minimap.RecordChunk(DimensionId, layer, chunkCoord);

            _loadedPeers[chunkCoord] = new HashSet<long>(requestingPeers);

            var payload = ChunkEditsPayload(chunkCoord);
            var ownPeerId = OwnPeerId();

            foreach (var peerId in requestingPeers)
            {
                if (peerId != ownPeerId)
                {
                    LoadChunkRequest(peerId, chunkCoord, payload);
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

            _painted.Remove(chunkCoord);

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

        #endregion

        #region Core - Mutacoes

        // Edicoes do chunk como vao pela rede: [x, y, camada, blockId ("" = removido)] por celula.
        private Godot.Collections.Array ChunkEditsPayload(Vector2I chunkCoord)
        {
            var payload = new Godot.Collections.Array();

            foreach (var edit in EnsureTerrain().CellEditsOf(chunkCoord))
            {
                payload.Add(new Godot.Collections.Array { edit.X, edit.Y, (int)edit.Layer, edit.BlockId ?? "" });
            }

            return payload;
        }

        private static List<CellEdit> ParseChunkEdits(Godot.Collections.Array payload)
        {
            var edits = new List<CellEdit>();

            foreach (var raw in payload)
            {
                var entry = raw.AsGodotArray();
                var blockId = entry[3].AsString();

                edits.Add(new CellEdit(entry[0].AsInt32(), entry[1].AsInt32(), (TerrainLayerType)entry[2].AsInt32(), blockId == "" ? null : blockId));
            }

            return edits;
        }

        // Mundo autoral: as camadas vem da cena, entao as edicoes salvas sao aplicadas direto nos tilemaps.
        public void ApplyEditsToAuthoredLayers(IEnumerable<CellEdit> edits)
        {
            foreach (var edit in edits)
            {
                var cell = new Vector2I(edit.X, edit.Y);

                if (edit.Layer == TerrainLayerType.Wall)
                {
                    _walls?.ApplyStoredEdit(cell, edit.BlockId);
                }
                else if (edit.BlockId == null)
                {
                    Layer?.EraseBlockAndReconnect(cell);
                }
                else if (BlockDB.TryGet(edit.BlockId, out var block))
                {
                    Layer?.PlaceBlock(cell, block);
                }
            }
        }

        #endregion

        #region Core - Consulta

        public bool IsChunkPainted(Vector2I chunk)
        {
            return _painted.Contains(chunk);
        }

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
            // Mundo autoral: finito e todo residente; o cliente recebe todas as edicoes e aplica na cena dele.
            if (!Procedural)
            {
                var terrain = EnsureTerrain();

                foreach (var chunk in terrain.EditedChunks.ToList())
                {
                    RpcId(targetPeerId, nameof(ReceiveChunkEdits), chunk, ChunkEditsPayload(chunk));
                }

                return;
            }

            foreach (var chunkCoord in _loaded)
            {
                if (CoordinateUtilities.ChunkDistance(chunkCoord, aroundChunk) > ChunkStreamingConstants.UNLOAD_RADIUS_CHUNKS)
                {
                    continue;
                }

                LoadChunkRequest(targetPeerId, chunkCoord, ChunkEditsPayload(chunkCoord));

                if (!_loadedPeers.TryGetValue(chunkCoord, out var peers))
                {
                    peers = new HashSet<long>();
                    _loadedPeers[chunkCoord] = peers;
                }

                peers.Add(targetPeerId);
            }

            SendEdgeChunks(targetPeerId);
        }

        public void RemovePeer(long peerId)
        {
            foreach (var peers in _loadedPeers.Values)
            {
                peers.Remove(peerId);
            }

            foreach (var peers in _edgePeers.Values)
            {
                peers.Remove(peerId);
            }
        }

        public void ResetState()
        {
            _loaded.Clear();
            _painted.Clear();
            _loadedPeers.Clear();
            _edgePeers.Clear();
            _minimap.Reset();
        }

        private void LoadChunkRequest(long peerId, Vector2I chunkCoord, Godot.Collections.Array payload)
        {
            if (IsPeerConnected(peerId))
            {
                RpcId(peerId, nameof(LoadChunkReceive), chunkCoord, payload);
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
        public async void LoadChunkReceive(Vector2I chunkCoord, Godot.Collections.Array payload)
        {
            if (_loaded.Contains(chunkCoord))
            {
                return;
            }

            var layer = Layer;

            _loaded.Add(chunkCoord);

            EnsureTerrain().LoadChunkEdits(chunkCoord, ParseChunkEdits(payload));

            await _generator.PaintTilesAsync(layer, BaseLayer, Seed, DimensionId, chunkCoord, ChunkStreamingConstants.CHUNK_SIZE);

            _minimap.RecordChunk(DimensionId, layer, chunkCoord);

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

            ReleaseFarClientData();

            ChunkUnloaded?.Invoke(chunkCoord);
        }

        #endregion

        #region Core - Rpc - Terreno

        // Edicoes de um chunk sem tiles: a margem da luz no procedural, ou as edicoes do mundo autoral (aplicadas
        // nos tilemaps da cena do cliente).
        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ReceiveChunkEdits(Vector2I chunkCoord, Godot.Collections.Array payload)
        {
            var edits = ParseChunkEdits(payload);

            EnsureTerrain().LoadChunkEdits(chunkCoord, edits);

            if (!Procedural)
            {
                ApplyEditsToAuthoredLayers(edits);
            }
        }

        // Cliente: solta as edicoes dos chunks que nao estao a ate a margem de nenhum chunk carregado (mesma regra
        // do SendEdgeChunks no servidor).
        private void ReleaseFarClientData()
        {
            if (!Procedural || _terrain == null)
            {
                return;
            }

            var margin = ChunkStreamingConstants.DATA_MARGIN_CHUNKS;
            var far = _terrain.EditedChunks
                .Concat(_terrain.LoadedChunks)
                .Distinct()
                .Where(chunk => !_loaded.Any(loaded => CoordinateUtilities.ChunkDistance(chunk, loaded) <= margin))
                .ToList();

            foreach (var chunk in far)
            {
                _terrain.UnloadChunkEdits(chunk);
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
