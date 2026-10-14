using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Instances;
using Jogo25D.Save;
using Jogo25D.Save.Resources;
using Jogo25D.Systems;
using Jogo25D.Utils.Coordinates;
using Jogo25D.Utils.GodotDictionaryParser;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Jogo25D.Entities
{
    public partial class WorldStreaming : Node2D
    {
        #region Dinamic properties

        public bool Enabled { get; set; } = false;

        #endregion

        #region World control

        // Entidades sendo liberadas pelo streaming (gravadas no banco): a remocao delas nao e destruicao.
        private readonly HashSet<long> _releasing = new();

        // Chunks por dimensao cujas entidades do banco ja foram trazidas (perto de algum jogador).
        private readonly Dictionary<string, HashSet<Vector2I>> _entityChunks = new();

        private readonly Dictionary<long, HashSet<long>> _peers = new();

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
                dimension.DropTerrain();
            }
        }

        public void DropWorlds()
        {
            AuthoredWorlds = false;

            PushConfig();

            foreach (var dimension in Dimensions())
            {
                dimension.DropTerrain();
            }
        }

        public void UseAuthoredWorlds()
        {
            AuthoredWorlds = true;

            PushConfig();

            foreach (var dimension in Dimensions())
            {
                dimension.LoadAuthoredTerrain();
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
            RpcId(targetPeerId, nameof(SetWorldModeReceive), AuthoredWorlds);

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

        // O cliente monta o proprio cache do terreno (autoral a partir da cena dele, procedural pela seed); as
        // edicoes chegam depois, por chunk.
        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SetWorldModeReceive(bool authored)
        {
            if (authored)
            {
                UseAuthoredWorlds();
            }
            else
            {
                UseProceduralWorlds();
            }
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
                Evaluate(node);
            }

            LoadNearbyEntities();
        }

        #endregion

        #region Core - Ciclo de vida observado pela arvore

        private void OnNodeAdded(Node node)
        {
            if (node is Node2D node2D && IsStreamed(node2D))
            {
                EnsureIdentity(node2D);
            }
        }

        private void OnNodeRemoved(Node node)
        {
            if (node is not Node2D node2D || !IsStreamed(node2D))
            {
                return;
            }

            var instanceId = EntityRecord.InstanceIdOf(node2D);

            if (_releasing.Contains(instanceId))
            {
                return;
            }

            _peers.Remove(instanceId);

            // Destruida no jogo (item coletado, por exemplo): sai do banco no proximo save.
            if (node.IsQueuedForDeletion())
            {
                WorldStore.ForgetEntity(node2D.Name);
            }
        }

        #endregion

        #region Core - Politica

        private void Evaluate(Node2D node)
        {
            if (!IsInstanceValid(node))
            {
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

            if (NearestPlayerDistance(node) > ChunkStreamingConstants.ENTITY_RADIUS_CHUNKS)
            {
                Release(node, instanceId);
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

        // Longe de todos os jogadores: grava a entidade no banco e libera o no. Sem banco aberto ela fica na arvore.
        private void Release(Node2D node, long instanceId)
        {
            if (!WorldStore.SaveEntity(DimensionOf(node), node))
            {
                return;
            }

            _releasing.Add(instanceId);

            node.GetParent()?.RemoveChild(node);

            EntitySpawner.DespawnRequest(instanceId);

            _releasing.Remove(instanceId);
            _peers.Remove(instanceId);

            node.QueueFree();
        }

        // Traz do banco as entidades dos chunks que entraram no raio de algum jogador.
        private void LoadNearbyEntities()
        {
            if (!WorldStore.IsOpen)
            {
                return;
            }

            var radius = ChunkStreamingConstants.ENTITY_RADIUS_CHUNKS;
            HashSet<string> alive = null;

            foreach (var dimension in Dimensions())
            {
                var dimensionId = dimension.DimensionId;
                var needed = new HashSet<Vector2I>();

                foreach (var player in Players.InDimension(dimensionId))
                {
                    if (player.PeerId <= 0)
                    {
                        continue;
                    }

                    var center = CoordinateUtilities.WorldToChunk(player.GlobalPosition, Dimension.TileSize);

                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        for (var dy = -radius; dy <= radius; dy++)
                        {
                            needed.Add(center + new Vector2I(dx, dy));
                        }
                    }
                }

                if (!_entityChunks.TryGetValue(dimensionId, out var loaded))
                {
                    _entityChunks[dimensionId] = loaded = new HashSet<Vector2I>();
                }

                foreach (var chunk in needed)
                {
                    // So com o chao do chunk pintado: sem colisao a entidade cairia.
                    if (!dimension.IsChunkPainted(chunk) || !loaded.Add(chunk))
                    {
                        continue;
                    }

                    foreach (var row in WorldStore.ReadEntities(dimensionId, chunk))
                    {
                        // As que nunca saem da arvore ja entraram com o mundo.
                        if (!StreamsClass(SaveSerializer.ClassOf(row.Type)))
                        {
                            continue;
                        }

                        alive ??= Streamed().Select(node => node.Name.ToString()).ToHashSet();

                        if (!alive.Add(row.EntityId))
                        {
                            continue;
                        }

                        var node = WorldStore.Build(row);

                        if (node == null || dimension.Entities == null)
                        {
                            node?.QueueFree();

                            continue;
                        }

                        dimension.Entities.AddChild(node);

                        EntitySpawner.SpawnRequest(BuildRecord(node));
                    }
                }

                loaded.IntersectWith(needed);
            }
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

        public void ResetState()
        {
            _releasing.Clear();
            _peers.Clear();
            _entityChunks.Clear();
        }

        #endregion

        #region Utils

        private IEnumerable<Node2D> Streamed()
        {
            return Descendants(this).Where(IsStreamed);
        }

        private static bool IsStreamed(Node2D node)
        {
            return StreamsClass(node.GetType());
        }

        // Classe que o streaming descarrega para o banco longe dos jogadores: precisa ser persistivel (SaveScene),
        // para nunca ficar viva fora do mundo. As outras ficam sempre na arvore.
        public static bool StreamsClass(System.Type type)
        {
            return type != null
                && type.GetCustomAttribute<SaveSceneAttribute>(inherit: false) != null
                && GodotDictionaryParser.HasSerializableFields(type)
                && ModeOf(type) == UnloadMode.Global;
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
            return ModeOf(node.GetType());
        }

        private static UnloadMode ModeOf(System.Type type)
        {
            if (_modeByType.TryGetValue(type, out var mode))
            {
                return mode;
            }

            mode = type.GetCustomAttribute<UnloadAttribute>()?.Mode ?? UnloadMode.Global;

            _modeByType[type] = mode;

            return mode;
        }

        // Entidades so existem dentro da arvore: a dimensao e a do pai.
        private static string DimensionOf(Node2D node)
        {
            return Dimension.IdOf(node);
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
