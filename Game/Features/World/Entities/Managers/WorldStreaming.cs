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
    // Script da raiz do World. Cuida do que existe DENTRO do mundo: quem e materializado,
    // quem sai da arvore, e o que vai pro arquivo.
    //
    // Nao ha registro: a arvore e o indice. Uma varredura recursiva a partir daqui encontra
    // tudo, e o criterio de participacao e ter campo marcado com [GodotDictionaryField] -
    // hoje so Prop e WorldItem tem. Player nao tem (os campos dele vivem no PlayerData, que
    // e Resource), entao ele fica de fora sozinho, sem precisar de excecao.
    //
    // A semantica do Godot ja expressa as tres operacoes:
    //
    //     AddChild     -> carregou
    //     RemoveChild  -> descarregou   (continua no save, volta quando o player chegar perto)
    //     QueueFree    -> esqueceu      (some das duas fontes, sai do save)
    //
    // Vive na raiz do World de proposito: ele nasce e morre com o mundo, entao nao existe
    // ResetState nem aviso de teardown.
    public partial class WorldStreaming : Node2D
    {
        #region Dinamic properties

        public bool Enabled { get; set; } = false;

        private static DimensionManager Dimensions => Game.Managers.DimensionManager.Node;

        #endregion

        #region World control

        // Quem esta fora da arvore. A arvore nao devolve mais esses, entao alguem tem que
        // segurar - e quem tirou. E dono ate devolver ou liberar.
        private readonly Dictionary<long, Node2D> _unloaded = new();

        // Quem tem cada no do lado do cliente. So usado no modo PeerOnly.
        private readonly Dictionary<long, HashSet<long>> _peers = new();


        private float _evaluateTimer;

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            // A arvore avisa toda entrada e saida. E o que faz "RemoveChild = descarregar"
            // valer venha de onde vier - reparent, codigo de gameplay, editor. Sem isso, um
            // RemoveChild feito por fora daqui sumia do save e vazava, porque ninguem segurava
            // a referencia.
            GetTree().NodeAdded += OnNodeAdded;
            GetTree().NodeRemoved += OnNodeRemoved;
        }

        // Quando o World morre, o que estava fora da arvore nao morre junto - ele nao e filho
        // de ninguem. Quem guardou, libera.
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

        // Uma varredura por tick decide tudo. So o servidor decide: o cliente recebe ordem de
        // spawn e despawn e obedece.
        public override void _Process(double delta)
        {
            if (!Enabled || !IsServerAuthoritative())
            {
                return;
            }

            _evaluateTimer += (float)delta;

            if (_evaluateTimer < ChunkStreamingConstants.EVALUATE_INTERVAL_SECONDS)
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

        // Entrou na arvore por qualquer caminho. Se estava guardado, deixa de estar - senao
        // ele apareceria duas vezes no save.
        private void OnNodeAdded(Node node)
        {
            if (node is Node2D node2D && GodotDictionaryParser.HasSerializableFields(node2D))
            {
                _unloaded.Remove(EnsureIdentity(node2D));
            }
        }

        // Saiu da arvore por qualquer caminho. IsQueuedForDeletion diz por que:
        //
        //     liberado  -> esqueceu. Nao entra no pool, e some das duas fontes do save.
        //     removido  -> descarregou. Alguem tem que segurar a referencia, ou vaza.
        private void OnNodeRemoved(Node node)
        {
            if (node is not Node2D node2D || !GodotDictionaryParser.HasSerializableFields(node2D))
            {
                return;
            }

            var instanceId = InstanceIdOf(node2D);

            if (node.IsQueuedForDeletion())
            {
                _unloaded.Remove(instanceId);
                _peers.Remove(instanceId);

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

            var perto = NearestPlayerDistance(node) <= ChunkStreamingConstants.UNLOAD_RADIUS_CHUNKS;

            if (perto && !dentroDaArvore)
            {
                Load(node, instanceId);
            }
            else if (!perto && dentroDaArvore)
            {
                Unload(node, instanceId);
            }
        }

        // O servidor mantem o no na arvore e continua simulando; cada peer ganha ou perde a
        // copia conforme chega perto ou se afasta.
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

                var perto = ChunkDistance(player.GlobalPosition, node.Position) <= ChunkStreamingConstants.UNLOAD_RADIUS_CHUNKS;

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
            var parent = Dimensions.ResolveParent(DimensionOf(node));

            if (parent == null)
            {
                return;
            }

            // Quem tira do _unloaded e o OnNodeAdded.
            parent.AddChild(node);

            Dimensions.SpawnRequest(BuildRecord(node));
        }

        private void Unload(Node2D node, long instanceId)
        {
            // RemoveChild, NAO QueueFree: o no sai da arvore mas continua sendo o dado.
            // Quem poe no _unloaded e o OnNodeRemoved.
            node.GetParent()?.RemoveChild(node);

            Dimensions.DespawnRequest(instanceId);
        }

        #endregion

        #region Core - Catch-up

        // Peer novo recebe o que esta na arvore perto dele.
        public void CatchUpPeer(long targetPeerId, Vector2 aroundPosition)
        {
            foreach (var node in Streamed())
            {
                if (ChunkDistance(aroundPosition, node.Position) > ChunkStreamingConstants.UNLOAD_RADIUS_CHUNKS)
                {
                    continue;
                }

                Dimensions.SpawnRequest(BuildRecord(node), targetPeerId);
            }
        }

        #endregion

        #region Core - Persistencia

        // Le do arquivo. Cada entidade e instanciada mas NAO entra na arvore: nasce
        // descarregada, e a varredura pendura as que estiverem perto de algum player.
        public void ImportState(string dimensionId, DimensionSaveData save)
        {
            if (save?.Entities == null)
            {
                return;
            }

            foreach (var record in save.Entities)
            {
                var node = Dimensions.Build(record);

                if (node == null)
                {
                    continue;
                }

                var instanceId = InstanceIdOf(node);

                _unloaded[instanceId] = node;
            }
        }

        // Mundo desenhado a mao nao tem streaming: pendura tudo de uma vez.
        public void MaterializeAll(string dimensionId)
        {
            foreach (var (instanceId, node) in _unloaded.ToList())
            {
                if (IsInstanceValid(node) && DimensionOf(node) == dimensionId)
                {
                    Load(node, instanceId);
                }
            }
        }

        // Salvar e o merge das duas fontes: o que esta pendurado e o que esta guardado.
        // Quem foi liberado nao aparece em nenhuma das duas - e e assim que ele sai do save.
        //
        // Recebe o objeto que o WorldManager esta montando pra gravar. Nada e guardado aqui:
        // a verdade sao os nos, e o DimensionSaveData vive so durante o save.
        public void ExportInto(string dimensionId, DimensionSaveData save)
        {
            foreach (var node in Streamed())
            {
                Append(dimensionId, save, node);
            }

            foreach (var node in _unloaded.Values)
            {
                if (IsInstanceValid(node))
                {
                    Append(dimensionId, save, node);
                }
            }
        }

        private void Append(string dimensionId, DimensionSaveData save, Node2D node)
        {
            if (DimensionOf(node) == dimensionId)
            {
                save.Entities.Add(BuildRecord(node));
            }
        }

        #endregion

        #region Utils

        // Tudo que esta na arvore do mundo e declara campo de save. Nao ha grupo nem lista:
        // ter campo marcado E a declaracao de "sou conteudo persistente".
        private IEnumerable<Node2D> Streamed()
        {
            return Descendants(this).Where(IsStreamed);
        }

        // Participa quem declara campo de save. Player fica de fora mesmo declarando: ele e
        // conteudo de SESSAO - quem o cria e destroi e o join, nao o mundo. Se o World o
        // salvasse e restaurasse, duas coisas mandariam no mesmo no.
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

        // O record e o no serializado: os campos marcados dele, mais o que ele nao consegue
        // declarar sozinho - a cena de onde veio, a identidade e a posicao, que e do Node2D.
        private Godot.Collections.Dictionary BuildRecord(Node2D node)
        {
            var record = GodotDictionaryParser.ToDictionary(node);

            record[DimensionManager.RECORD_SCENE] = node.SceneFilePath;
            record[DimensionManager.RECORD_INSTANCE] = InstanceIdOf(node);
            record[DimensionManager.RECORD_DIMENSION] = DimensionOf(node);
            record[DimensionManager.RECORD_POSITION] = DimensionManager.WriteVector(node.Position);

            return record;
        }

        // O nome do no e a identidade, e precisa ser deterministico: RPC do Godot resolve por
        // caminho, entao o mesmo no tem que ter o mesmo nome em todos os peers.
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

        // A politica vem do [Unload] da classe. O cache e por TIPO, nao por instancia: ele e
        // limitado pelo numero de classes de entidade e nunca cresce com o jogo rodando.
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
            return node.IsInsideTree() ? Dimensions.ResolveDimensionIdOf(node) : ChunkStreamingConstants.UPSIDEDOWN_ID;
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
