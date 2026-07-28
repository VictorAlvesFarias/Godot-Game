using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jogo25D.Characters;
using Jogo25D.Features.World.Chunks.Resources;
using Jogo25D.Systems;
using Jogo25D.TileEntities;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Chunks
{
    public partial class ChunkStreamingManager : Node
    {
        public static string DEFAULT_NODE_PATH = "/root/Main/Managers/ChunkStreamingManager";

        public const int ChunkSize = 32;
        public const int LoadRadiusChunks = 2;
        public const int UnloadRadiusChunks = 4;
        public const float EvaluateIntervalSeconds = 0.75f;

        public const int MaxChunkLoadsPerTick = 2;

        public const string OverworldId = "overworld";
        public const string UpsidedownId = "upsidedown";
        private const string ProceduralLayerName = "ProceduralTiles";

        [Export] public bool Enabled { get; set; } = false;
        [Export] public int TileSize { get; set; } = 32;

        private WorldManager _worldManager;
        private float _evaluateTimer;
        private long _worldSeed;

        private TileMapLayer _overworldLayer;
        private TileMapLayer _upsidedownLayer;

        private readonly HashSet<Vector2I> _loadedOverworld = new();
        private readonly HashSet<Vector2I> _loadedUpsidedown = new();
        private readonly Dictionary<Vector2I, ChunkStateData> _overworldState = new();
        private readonly Dictionary<Vector2I, ChunkStateData> _upsidedownState = new();

        #region Godot implementation

        public override void _Ready()
        {
            _worldManager = GetTree().Root.GetNodeOrNull<WorldManager>(WorldManager.DEFAULT_NODE_PATH);

            if (IsServerAuthoritative())
            {
                _worldSeed = (uint)GD.Randi();
            }
        }

        public override void _Process(double delta)
        {
            if (!Enabled || !IsServerAuthoritative() || _worldManager == null)
            {
                return;
            }

            _evaluateTimer += (float)delta;

            if (_evaluateTimer < EvaluateIntervalSeconds)
            {
                return;
            }

            _evaluateTimer = 0f;

            Evaluate(OverworldId, _worldManager.OverworldParent, _loadedOverworld, _overworldState);
            Evaluate(UpsidedownId, _worldManager.UpsidedownParent, _loadedUpsidedown, _upsidedownState);
        }

        #endregion

        #region Core - Evaluation (load/unload decision)

        private bool IsServerAuthoritative()
        {
            return Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();
        }

        private void Evaluate(string dimensionId, Node2D dimensionParent, HashSet<Vector2I> loaded, Dictionary<Vector2I, ChunkStateData> state)
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

            foreach (var playerChunk in playerChunks)
            {
                for (int dx = -LoadRadiusChunks; dx <= LoadRadiusChunks; dx++)
                {
                    for (int dy = -LoadRadiusChunks; dy <= LoadRadiusChunks; dy++)
                    {
                        needed.Add(playerChunk + new Vector2I(dx, dy));
                    }
                }
            }

            var missing = needed
                .Where(c => !loaded.Contains(c))
                .OrderBy(c => playerChunks.Min(pc => Mathf.Max(Mathf.Abs(c.X - pc.X), Mathf.Abs(c.Y - pc.Y))))
                .Take(MaxChunkLoadsPerTick);

            foreach (var chunkCoord in missing)
            {
                LoadChunk(dimensionId, dimensionParent, chunkCoord, loaded, state);
            }

            var toUnload = new List<Vector2I>();

            foreach (var chunkCoord in loaded)
            {
                var withinUnloadRadius = playerChunks.Any(playerChunk =>
                {
                    var distance = Mathf.Max(Mathf.Abs(chunkCoord.X - playerChunk.X), Mathf.Abs(chunkCoord.Y - playerChunk.Y));

                    return distance <= UnloadRadiusChunks;
                });

                if (!withinUnloadRadius)
                {
                    toUnload.Add(chunkCoord);
                }
            }

            foreach (var chunkCoord in toUnload)
            {
                UnloadChunk(dimensionId, dimensionParent, chunkCoord, loaded, state);
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
            var centerChunk = CellToChunk(WorldToCell(worldPosition));

            for (int dx = -LoadRadiusChunks; dx <= LoadRadiusChunks; dx++)
            {
                for (int dy = -LoadRadiusChunks; dy <= LoadRadiusChunks; dy++)
                {
                    var chunkCoord = centerChunk + new Vector2I(dx, dy);

                    if (!loaded.Contains(chunkCoord))
                    {
                        LoadChunk(dimensionId, dimensionParent, chunkCoord, loaded, state);

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
                Mathf.FloorToInt(cell.X / (float)ChunkSize),
                Mathf.FloorToInt(cell.Y / (float)ChunkSize));
        }

        #endregion

        #region Core - Load/Unload (server-side)

        private void LoadChunk(string dimensionId, Node2D dimensionParent, Vector2I chunkCoord, HashSet<Vector2I> loaded, Dictionary<Vector2I, ChunkStateData> state)
        {
            var layer = GetOrCreateLayer(dimensionId, dimensionParent);

            ChunkGenerator.Paint(layer, _worldSeed, dimensionId, chunkCoord, ChunkSize);

            loaded.Add(chunkCoord);

            dimensionParent.GetNodeOrNull<TileEntityManager>("TileEntityManager")?.RegisterChunk(layer, chunkCoord, ChunkSize);

            if (!state.TryGetValue(chunkCoord, out var chunkState))
            {
                chunkState = new ChunkStateData();
                state[chunkCoord] = chunkState;
            }

            BroadcastLoadChunk(dimensionId, chunkCoord, GodotDictionaryParser.ToDictionary(chunkState));
        }

        private void UnloadChunk(string dimensionId, Node2D dimensionParent, Vector2I chunkCoord, HashSet<Vector2I> loaded, Dictionary<Vector2I, ChunkStateData> state)
        {
            if (!loaded.Remove(chunkCoord))
            {
                return;
            }

            state.Remove(chunkCoord);

            var layer = GetOrCreateLayer(dimensionId, dimensionParent);

            ChunkGenerator.Erase(layer, chunkCoord, ChunkSize);

            dimensionParent.GetNodeOrNull<TileEntityManager>("TileEntityManager")?.UnregisterChunk(chunkCoord, ChunkSize);

            BroadcastUnloadChunk(dimensionId, chunkCoord);
        }

        private TileMapLayer GetOrCreateLayer(string dimensionId, Node2D dimensionParent)
        {
            var existing = dimensionId == OverworldId ? _overworldLayer : _upsidedownLayer;

            if (existing != null && IsInstanceValid(existing))
            {
                return existing;
            }

            var layer = new TileMapLayer
            {
                Name = ProceduralLayerName,
                TileSet = ChunkGenerator.GetTileSet(dimensionId),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };

            dimensionParent.AddChild(layer);

            if (dimensionId == OverworldId)
            {
                _overworldLayer = layer;
            }
            else
            {
                _upsidedownLayer = layer;
            }

            return layer;
        }

        private void BroadcastLoadChunk(string dimensionId, Vector2I chunkCoord, Godot.Collections.Dictionary stateDict)
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
            {
                return;
            }

            Rpc(nameof(LoadChunkReceive), dimensionId, chunkCoord, stateDict);
        }

        private void BroadcastUnloadChunk(string dimensionId, Vector2I chunkCoord)
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
            {
                return;
            }

            Rpc(nameof(UnloadChunkReceive), dimensionId, chunkCoord);
        }

        #endregion

        #region Core - Rpc

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SetWorldSeedReceive(long seed)
        {
            _worldSeed = seed;
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

            ChunkGenerator.Paint(layer, _worldSeed, dimensionId, chunkCoord, ChunkSize);

            loaded.Add(chunkCoord);

            ResolveState(dimensionId)[chunkCoord] = GodotDictionaryParser.ToResource<ChunkStateData>(stateDict);
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

            ChunkGenerator.Erase(layer, chunkCoord, ChunkSize);
        }

        #endregion

        #region Core - Peer catch-up

        public void CatchUpPeer(long targetPeerId)
        {
            RpcId(targetPeerId, nameof(SetWorldSeedReceive), _worldSeed);

            CatchUpDimension(OverworldId, _loadedOverworld, _overworldState, targetPeerId);
            CatchUpDimension(UpsidedownId, _loadedUpsidedown, _upsidedownState, targetPeerId);
        }

        private void CatchUpDimension(string dimensionId, HashSet<Vector2I> loaded, Dictionary<Vector2I, ChunkStateData> state, long targetPeerId)
        {
            foreach (var chunkCoord in loaded)
            {
                var chunkState = state.TryGetValue(chunkCoord, out var s) ? s : new ChunkStateData();

                RpcId(targetPeerId, nameof(LoadChunkReceive), dimensionId, chunkCoord, GodotDictionaryParser.ToDictionary(chunkState));
            }
        }

        #endregion

        #region Core - Reset

        public void ResetState()
        {
            _loadedOverworld.Clear();
            _loadedUpsidedown.Clear();
            _overworldState.Clear();
            _upsidedownState.Clear();
            _overworldLayer = null;
            _upsidedownLayer = null;
            _evaluateTimer = 0f;
        }

        #endregion

        #region Utils

        private Node2D ResolveDimensionParent(string dimensionId)
        {
            return dimensionId == OverworldId ? _worldManager?.OverworldParent : _worldManager?.UpsidedownParent;
        }

        private HashSet<Vector2I> ResolveLoaded(string dimensionId)
        {
            return dimensionId == OverworldId ? _loadedOverworld : _loadedUpsidedown;
        }

        private Dictionary<Vector2I, ChunkStateData> ResolveState(string dimensionId)
        {
            return dimensionId == OverworldId ? _overworldState : _upsidedownState;
        }

        #endregion
    }
}
