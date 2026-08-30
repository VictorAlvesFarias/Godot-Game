using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Instances;
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

        private static DimensionManager Dimensions => Game.Managers.DimensionManager.Node;

        #endregion

        #region World control

        private readonly Dictionary<long, Node2D> _unloaded = new();

        private readonly Dictionary<long, HashSet<long>> _peers = new();

        private readonly Dictionary<long, string> _dimensionOf = new();

        private float _evaluateTimer;

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            GetTree().NodeAdded += OnNodeAdded;
            GetTree().NodeRemoved += OnNodeRemoved;
        }

        public override void _ExitTree()
        {
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
                Evaluate(node, dentroDaArvore: true);
            }

            foreach (var node in _unloaded.Values.ToList())
            {
                Evaluate(node, dentroDaArvore: false);
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

            var instanceId = InstanceIdOf(node2D);

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

        private void Evaluate(Node2D node, bool dentroDaArvore)
        {
            if (!IsInstanceValid(node))
            {
                _unloaded.Remove(InstanceIdOf(node));

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

            var perto = NearestPlayerDistance(node) <= ChunkStreamingConstants.ENTITY_RADIUS_CHUNKS;

            if (perto && !dentroDaArvore)
            {
                Load(node, instanceId);
            }
            else if (!perto && dentroDaArvore)
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

            if (!_peers.TryGetValue(instanceId, out var tem))
            {
                tem = new HashSet<long>();
                _peers[instanceId] = tem;
            }

            foreach (var player in Game.Managers.WorldManager.Node.GetPlayersInDimension(DimensionOf(node)))
            {
                if (player.PeerId <= 1)
                {
                    continue;
                }

                var perto = ChunkDistance(player.GlobalPosition, node.Position) <= ChunkStreamingConstants.ENTITY_RADIUS_CHUNKS;

                if (perto && tem.Add(player.PeerId))
                {
                    Dimensions.SpawnRequest(BuildRecord(node), player.PeerId);
                }
                else if (!perto && tem.Remove(player.PeerId))
                {
                    Dimensions.DespawnForPeer(player.PeerId, instanceId);
                }
            }
        }

        private void Load(Node2D node, long instanceId)
        {
            var parent = Dimensions.ResolveEntities(DimensionOf(node));

            if (parent == null)
            {
                return;
            }

            parent.AddChild(node);

            Dimensions.SpawnRequest(BuildRecord(node));
        }

        private void Unload(Node2D node, long instanceId)
        {
            node.GetParent()?.RemoveChild(node);

            Dimensions.DespawnRequest(instanceId);
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

                Dimensions.SpawnRequest(BuildRecord(node), targetPeerId);
            }
        }

        #endregion

        #region Core - Persistencia

        public void Adotar(Node2D node, string dimensionId)
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

        public IEnumerable<Node2D> Descarregados(string dimensionId)
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

        private static IEnumerable<Node2D> Descendants(Node raiz)
        {
            foreach (var child in raiz.GetChildren())
            {
                if (child is Node2D node2D)
                {
                    yield return node2D;
                }

                foreach (var neto in Descendants(child))
                {
                    yield return neto;
                }
            }
        }

        private Godot.Collections.Dictionary BuildRecord(Node2D node)
        {
            var record = GodotDictionaryParser.ToDictionary(node);

            record[DimensionManager.RECORD_SCENE] = node.SceneFilePath;
            record[DimensionManager.RECORD_INSTANCE] = InstanceIdOf(node);
            record[DimensionManager.RECORD_DIMENSION] = DimensionOf(node);
            record[DimensionManager.RECORD_POSITION] = DimensionManager.WriteVector(node.Position);

            return record;
        }

        private long EnsureIdentity(Node2D node)
        {
            var instanceId = InstanceIdOf(node);

            if (instanceId != 0)
            {
                return instanceId;
            }

            instanceId = InstanceIdGenerator.NextInstanceId();

            node.Name = DimensionManager.EntityNameOf(instanceId);

            return instanceId;
        }

        private static long InstanceIdOf(Node node)
        {
            return DimensionManager.InstanceIdOf(node);
        }

        private static readonly Dictionary<System.Type, UnloadMode> _modeByType = new();

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
            var instanceId = InstanceIdOf(node);

            if (node.IsInsideTree())
            {
                var dimensionId = Dimensions.ResolveDimensionIdOf(node);

                _dimensionOf[instanceId] = dimensionId;

                return dimensionId;
            }

            return _dimensionOf.TryGetValue(instanceId, out var lembrado) ? lembrado : ChunkStreamingConstants.UPSIDEDOWN_ID;
        }

        private int NearestPlayerDistance(Node2D node)
        {
            var menor = int.MaxValue;

            foreach (var player in Game.Managers.WorldManager.Node.GetPlayersInDimension(DimensionOf(node)))
            {
                if (player.PeerId > 0)
                {
                    menor = Mathf.Min(menor, ChunkDistance(player.GlobalPosition, node.Position));
                }
            }

            return menor;
        }

        private int ChunkDistance(Vector2 a, Vector2 b)
        {
            var tileSize = Dimensions.TileSize;

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
