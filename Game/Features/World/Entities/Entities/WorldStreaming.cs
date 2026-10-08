using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Instances;
using Jogo25D.Save.Resources;
using Jogo25D.Systems;
using Jogo25D.Utils.Coordinates;
using Jogo25D.Utils.GodotDictionaryParser;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Jogo25D.Entities
{
    [Jogo25D.Save.SaveScene("world", "res://Scenes/World/World.tscn")]
    public partial class WorldStreaming : Node2D
    {
        #region Dinamic properties

        public bool Enabled { get; set; } = false;

        #endregion

        #region World control

        private readonly Dictionary<long, Node2D> _unloaded = new();

        private readonly Dictionary<long, HashSet<long>> _peers = new();

        private readonly Dictionary<long, string> _dimensionOf = new();

        private float _evaluateTimer;
        private float _tileEvaluateTimer;

        private static readonly Dictionary<System.Type, UnloadMode> _modeByType = new();

        #endregion

        #region Dinamic properties

        public static WorldStreaming Current { get; private set; }

        public long WorldSeed { get; private set; }

        public bool TileStreamingEnabled { get; set; }

        public bool AuthoredWorlds { get; private set; }

        public void SetWorldSeed(long seed)
        {
            WorldSeed = seed;

            PushConfig();
        }

        public void UseProceduralWorlds()
        {
            AuthoredWorlds = false;

            PushConfig();

            foreach (var dimension in Dimensions())
            {
                dimension.DropWorld();
            }
        }

        public void DropWorlds()
        {
            AuthoredWorlds = false;

            PushConfig();

            foreach (var dimension in Dimensions())
            {
                dimension.DropWorld();
            }
        }

        public void UseAuthoredWorlds()
        {
            AuthoredWorlds = true;

            PushConfig();

            foreach (var dimension in Dimensions())
            {
                dimension.LoadAuthoredWorld();
            }
        }

        private void PushConfig()
        {
            foreach (var dimension in Dimensions())
            {
                dimension.Seed = WorldSeed;
                dimension.Procedural = !AuthoredWorlds;
                dimension.TileStreaming = TileStreamingEnabled;
            }
        }

        public IEnumerable<Dimension> Dimensions()
        {
            foreach (var id in Dimension.Ids)
            {
                var dimension = Dimension.Get(id);

                if (dimension != null)
                {
                    yield return dimension;
                }
            }
        }

        public void CatchUpTiles(long targetPeerId, Vector2 aroundPosition)
        {
            RpcId(targetPeerId, nameof(SetWorldSeedReceive), WorldSeed);

            var aroundChunk = CoordinateUtilities.WorldToChunk(aroundPosition, Dimension.TileSize);

            foreach (var dimension in Dimensions())
            {
                dimension.CatchUpTo(targetPeerId, aroundChunk);
            }
        }

        public void RemovePeer(long peerId)
        {
            foreach (var dimension in Dimensions())
            {
                dimension.RemovePeer(peerId);
            }
        }

        public void SetStreamingEnabled(bool enabled)
        {
            TileStreamingEnabled = enabled;
            Enabled = enabled;

            PushConfig();
        }

        public void ResetTileStreaming()
        {
            TileStreamingEnabled = false;

            PushConfig();

            foreach (var dimension in Dimensions())
            {
                dimension.ResetState();
            }
        }

        #endregion

        #region Core - Streaming de tile

        private void EvaluateTileStreaming(double delta)
        {
            if (!TileStreamingEnabled)
            {
                return;
            }

            _tileEvaluateTimer += (float)delta;

            if (_tileEvaluateTimer < ChunkStreamingConstants.EVALUATE_INTERVAL_SECONDS)
            {
                return;
            }

            _tileEvaluateTimer = 0f;

            foreach (var dimension in Dimensions())
            {
                dimension.Evaluate();
            }
        }

        #endregion

        #region Core - Rpc - Mundo

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SetWorldSeedReceive(long seed)
        {
            SetWorldSeed(seed);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ClearLayersReceive()
        {
            Dimension.ClearAllEntities();
            Dimension.ClearAllLayers(discardBackground: true);
        }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            Current = this;

            SetWorldSeed((uint)GD.Randi());

            GetTree().NodeAdded += OnNodeAdded;
            GetTree().NodeRemoved += OnNodeRemoved;
        }

        public override void _ExitTree()
        {
            if (Current == this)
            {
                Current = null;
            }

            foreach (var node in _unloaded.Values)
            {
                if (IsInstanceValid(node))
                {
                    node.QueueFree();
                }
            }

            _unloaded.Clear();

            if (GetTree() != null)
            {
                GetTree().NodeAdded -= OnNodeAdded;
                GetTree().NodeRemoved -= OnNodeRemoved;
            }
        }

        public override void _Process(double delta)
        {
            EvaluateTileStreaming(delta);

            if (!Enabled || !IsServerAuthoritative())
            {
                return;
            }

            _evaluateTimer += (float)delta;

            if (_evaluateTimer < ChunkStreamingConstants.ENTITY_EVALUATE_INTERVAL_SECONDS)
            {
                return;
            }

            _evaluateTimer = 0f;

            foreach (var node in Streamed().ToList())
            {
                Evaluate(node, insideTree: true);
            }

            foreach (var node in _unloaded.Values.ToList())
            {
                Evaluate(node, insideTree: false);
            }
        }

        #endregion

        #region Core - Ciclo de vida observado pela arvore

        private void OnNodeAdded(Node node)
        {
            if (node is Node2D node2D && IsStreamed(node2D))
            {
                _unloaded.Remove(EnsureIdentity(node2D));
            }
        }

        private void OnNodeRemoved(Node node)
        {
            if (node is not Node2D node2D || !IsStreamed(node2D))
            {
                return;
            }

            var instanceId = EntityRecord.InstanceIdOf(node2D);

            DimensionOf(node2D);

            if (node.IsQueuedForDeletion())
            {
                _unloaded.Remove(instanceId);
                _peers.Remove(instanceId);
                _dimensionOf.Remove(instanceId);

                return;
            }

            _unloaded[instanceId] = node2D;
            _peers.Remove(instanceId);
        }

        #endregion

        #region Core - Politica

        private void Evaluate(Node2D node, bool insideTree)
        {
            if (!IsInstanceValid(node))
            {
                _unloaded.Remove(EntityRecord.InstanceIdOf(node));

                return;
            }

            var instanceId = EnsureIdentity(node);
            var mode = ReadMode(node);

            if (mode == UnloadMode.Never)
            {
                return;
            }

            if (mode == UnloadMode.PeerOnly)
            {
                EvaluatePeers(node, instanceId);

                return;
            }

            var near = NearestPlayerDistance(node) <= ChunkStreamingConstants.ENTITY_RADIUS_CHUNKS;

            if (near && !insideTree)
            {
                Load(node, instanceId);
            }
            else if (!near && insideTree)
            {
                Unload(node, instanceId);
            }
        }

        private void EvaluatePeers(Node2D node, long instanceId)
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || !node.IsInsideTree())
            {
                return;
            }

            if (!_peers.TryGetValue(instanceId, out var has))
            {
                has = new HashSet<long>();
                _peers[instanceId] = has;
            }

            foreach (var player in Players.InDimension(DimensionOf(node)))
            {
                if (player.PeerId <= 1)
                {
                    continue;
                }

                var near = ChunkDistance(player.GlobalPosition, node.Position) <= ChunkStreamingConstants.ENTITY_RADIUS_CHUNKS;

                if (near && has.Add(player.PeerId))
                {
                    EntitySpawner.SpawnRequest(BuildRecord(node), player.PeerId);
                }
                else if (!near && has.Remove(player.PeerId))
                {
                    EntitySpawner.DespawnForPeer(player.PeerId, instanceId);
                }
            }
        }

        private void Load(Node2D node, long instanceId)
        {
            var parent = Dimension.Get(DimensionOf(node))?.Entities;

            if (parent == null)
            {
                return;
            }

            parent.AddChild(node);

            EntitySpawner.SpawnRequest(BuildRecord(node));
        }

        private void Unload(Node2D node, long instanceId)
        {
            node.GetParent()?.RemoveChild(node);

            EntitySpawner.DespawnRequest(instanceId);
        }

        #endregion

        #region Core - Catch-up

        public void CatchUpPeer(long targetPeerId, Vector2 aroundPosition)
        {
            foreach (var node in Streamed())
            {
                if (ChunkDistance(aroundPosition, node.Position) > ChunkStreamingConstants.ENTITY_RADIUS_CHUNKS)
                {
                    continue;
                }

                EntitySpawner.SpawnRequest(BuildRecord(node), targetPeerId);
            }
        }

        #endregion

        #region Core - Persistencia

        public void Adopt(Node2D node, string dimensionId)
        {
            if (node == null)
            {
                return;
            }

            var instanceId = EnsureIdentity(node);

            _unloaded[instanceId] = node;
            _dimensionOf[instanceId] = dimensionId;
        }

        public void ResetState()
        {
            foreach (var node in _unloaded.Values)
            {
                if (IsInstanceValid(node))
                {
                    node.Free();
                }
            }

            _unloaded.Clear();
            _peers.Clear();
            _dimensionOf.Clear();
        }

        public IEnumerable<Node2D> Unloaded(string dimensionId)
        {
            foreach (var node in _unloaded.Values)
            {
                if (IsInstanceValid(node) && DimensionOf(node) == dimensionId)
                {
                    yield return node;
                }
            }
        }

        #endregion

        #region Utils

        private IEnumerable<Node2D> Streamed()
        {
            return Descendants(this).Where(IsStreamed);
        }

        private static bool IsStreamed(Node2D node)
        {
            return GodotDictionaryParser.HasSerializableFields(node) && !node.IsInGroup("players");
        }

        private static IEnumerable<Node2D> Descendants(Node root)
        {
            foreach (var child in root.GetChildren())
            {
                if (child is Node2D node2D)
                {
                    yield return node2D;
                }

                foreach (var grandchild in Descendants(child))
                {
                    yield return grandchild;
                }
            }
        }

        private Godot.Collections.Dictionary BuildRecord(Node2D node)
        {
            var record = GodotDictionaryParser.ToDictionary(node);

            record[EntityRecord.SCENE] = node.SceneFilePath;
            record[EntityRecord.INSTANCE] = EntityRecord.InstanceIdOf(node);
            record[EntityRecord.DIMENSION] = DimensionOf(node);
            record[EntityRecord.POSITION] = EntityRecord.WriteVector(node.Position);

            return record;
        }

        private long EnsureIdentity(Node2D node)
        {
            var instanceId = EntityRecord.InstanceIdOf(node);

            if (instanceId != 0)
            {
                return instanceId;
            }

            instanceId = InstanceIdGenerator.NextInstanceId();

            node.Name = EntityRecord.NameOf(instanceId);

            return instanceId;
        }

        private static UnloadMode ReadMode(Node2D node)
        {
            var type = node.GetType();

            if (_modeByType.TryGetValue(type, out var mode))
            {
                return mode;
            }

            mode = type.GetCustomAttribute<UnloadAttribute>()?.Mode ?? UnloadMode.Global;

            _modeByType[type] = mode;

            return mode;
        }

        private string DimensionOf(Node2D node)
        {
            var instanceId = EntityRecord.InstanceIdOf(node);

            if (node.IsInsideTree())
            {
                var dimensionId = Dimension.IdOf(node);

                _dimensionOf[instanceId] = dimensionId;

                return dimensionId;
            }

            return _dimensionOf.TryGetValue(instanceId, out var remembered) ? remembered : ChunkStreamingConstants.UPSIDEDOWN_ID;
        }

        private int NearestPlayerDistance(Node2D node)
        {
            var nearest = int.MaxValue;

            foreach (var player in Players.InDimension(DimensionOf(node)))
            {
                if (player.PeerId > 0)
                {
                    nearest = Mathf.Min(nearest, ChunkDistance(player.GlobalPosition, node.Position));
                }
            }

            return nearest;
        }

        private int ChunkDistance(Vector2 a, Vector2 b)
        {
            var tileSize = Dimension.TileSize;

            return CoordinateUtilities.ChunkDistance(
                CoordinateUtilities.WorldToChunk(a, tileSize),
                CoordinateUtilities.WorldToChunk(b, tileSize));
        }

        private bool IsServerAuthoritative()
        {
            return Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();
        }

        #endregion
    }
}
