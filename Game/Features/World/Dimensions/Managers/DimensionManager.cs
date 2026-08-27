using Godot;
using Jogo25D.Biomes;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Entities;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Features.World.Characters.Resources;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Instances;
using Jogo25D.Items;
using Jogo25D.Utils.GodotDictionaryParser;
using Jogo25D.Props;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Dimensions
{
    // Dono unico das dimensoes: os parents (Overworld/Upsidedown), os SubViewportContainer de cada
    // uma, as TileMapLayer Base/Compose, e a criacao de tudo que nasce dentro delas (player, NPC,
    // item no chao, prop). Ninguem mais guarda essas referencias - quem precisa pergunta aqui.
    //
    // Por que manager e nao script na raiz de Overworld.tscn: as raizes nascem e morrem junto com
    // o World.tscn, entao um script nelas ficaria fora do registro Game. Este no e estatico,
    // sobrevive a destruicao do mundo e re-resolve as referencias quando o mundo volta.
    public partial class DimensionManager : Node
    {
        #region Dinamic properties

        public int TileSize => ResolveLayer(ChunkStreamingConstants.OVERWORLD_ID)?.TileSet?.TileSize.X
            ?? ResolveLayer(ChunkStreamingConstants.UPSIDEDOWN_ID)?.TileSet?.TileSize.X
            ?? ChunkStreamingConstants.REFERENCE_TILE_SIZE;


        public IEnumerable<string> Ids => _dimensions.Keys;

        public IEnumerable<Node2D> Parents => _dimensions.Keys.Select(ResolveParent).Where(parent => parent != null);

        public bool IsResolved => _dimensions.Values.All(dimension => dimension.Parent != null && IsInstanceValid(dimension.Parent));

        #endregion

        #region World control

        private readonly Dictionary<string, DimensionData> _dimensions = new()
        {
            [ChunkStreamingConstants.OVERWORLD_ID] = new DimensionData("Main/World/Levels/OverworldViewportContainer", "OverworldViewport/Overworld"),
            [ChunkStreamingConstants.UPSIDEDOWN_ID] = new DimensionData("Main/World/Levels/UpsidedownViewportContainer", "UpsidedownViewport/Upsidedown"),
        };

        // Estado de uma dimensao. Privado de proposito: quem consome usa os Resolve* passando o
        // dimensionId, nunca o par de campos solto.
        private class DimensionData
        {
            public DimensionData(string containerPath, string parentSubPath)
            {
                ContainerPath = containerPath;
                ParentPath = $"{containerPath}/{parentSubPath}";
            }

            public string ContainerPath { get; }
            public string ParentPath { get; }

            public Node2D Parent { get; set; }
            public SubViewportContainer Container { get; set; }
            public TerrainLayer Layer { get; set; }
            public TerrainLayer BaseLayer { get; set; }
        }

        #endregion

        #region Core - Resolucao

        // Chamado depois que o World.tscn entra na arvore. Resolve parent e container das duas
        // dimensoes; as layers ficam pro primeiro ResolveLayer, que ja sabe se revalidar.
        public void ResolveReferences()
        {
            foreach (var (dimensionId, dimension) in _dimensions)
            {
                dimension.Parent = GetTree().Root.GetNodeOrNull<Node2D>(dimension.ParentPath);
                dimension.Container = GetTree().Root.GetNodeOrNull<SubViewportContainer>(dimension.ContainerPath);
                dimension.Layer = null;
                dimension.BaseLayer = null;

                if (dimension.Parent == null)
                {
                    GD.PushError($"[DimensionManager.ResolveReferences] parent de '{dimensionId}' nao encontrado em {dimension.ParentPath}");
                }

                if (dimension.Container == null)
                {
                    GD.PushError($"[DimensionManager.ResolveReferences] container de '{dimensionId}' nao encontrado em {dimension.ContainerPath}");
                }
            }
        }

        public Node2D ResolveParent(string dimensionId)
        {
            var dimension = Resolve(dimensionId);

            return dimension?.Parent != null && IsInstanceValid(dimension.Parent) ? dimension.Parent : null;
        }

        public TerrainLayer ResolveLayer(string dimensionId)
        {
            var dimension = Resolve(dimensionId);

            ResolveLayers(dimension);

            return dimension?.Layer;
        }

        public TerrainLayer ResolveBaseLayer(string dimensionId)
        {
            var dimension = Resolve(dimensionId);

            ResolveLayers(dimension);

            return dimension?.BaseLayer;
        }

        // Dimensao a que um no pertence, pelo parent em que ele esta pendurado.
        public string ResolveDimensionIdOf(Node node)
        {
            var parent = node?.GetParent();

            foreach (var (dimensionId, dimension) in _dimensions)
            {
                if (parent != null && parent == dimension.Parent)
                {
                    return dimensionId;
                }
            }

            return ChunkStreamingConstants.UPSIDEDOWN_ID;
        }

        // Deixa visivel so o container da dimensao informada.
        public void ShowOnly(string dimensionId)
        {
            foreach (var (currentId, dimension) in _dimensions)
            {
                if (dimension.Container != null && IsInstanceValid(dimension.Container))
                {
                    dimension.Container.Visible = currentId == dimensionId;
                }
            }
        }

        private DimensionData Resolve(string dimensionId)
        {
            return _dimensions.TryGetValue(dimensionId, out var dimension) ? dimension : null;
        }

        private void ResolveLayers(DimensionData dimension)
        {
            if (dimension == null || (dimension.Layer != null && IsInstanceValid(dimension.Layer)))
            {
                return;
            }

            var parent = dimension.Parent != null && IsInstanceValid(dimension.Parent) ? dimension.Parent : null;

            dimension.Layer = parent?.GetNodeOrNull<TerrainLayer>(ChunkStreamingConstants.PROCEDURAL_LAYER_NAME);
            dimension.BaseLayer = parent?.GetNodeOrNull<TerrainLayer>(ChunkStreamingConstants.PROCEDURAL_BASE_LAYER_NAME);
        }

        #endregion

        #region Core - Limpeza

        // Zera o terreno desenhado das duas dimensoes. O que o cliente tinha por padrao sai daqui
        // antes de receber os chunks do servidor.
        public void ClearLayers()
        {
            foreach (var dimensionId in _dimensions.Keys)
            {
                ResolveBaseLayer(dimensionId)?.Clear();
                ResolveLayer(dimensionId)?.Clear();
            }
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ClearLayersReceive()
        {
            ClearLayers();
        }

        public void Reset()
        {
            foreach (var dimension in _dimensions.Values)
            {
                dimension.Parent = null;
                dimension.Container = null;
                dimension.Layer = null;
                dimension.BaseLayer = null;
            }

        }

        #endregion

        #region Core - Posicionamento

        // Varre a coluna de cima pra baixo ate achar chao solido; devolve a posicao em que um corpo
        // com halfBodyHeight fica apoiado nele.
        public Vector2 FindGroundSpawnPosition(string dimensionId, float worldX, float halfBodyHeight = 15f)
        {
            var layer = ResolveLayer(dimensionId);

            if (layer == null || layer.TileSet == null)
            {
                return new Vector2(worldX, 0f);
            }

            var tileSize = layer.TileSet.TileSize.X;
            var startCell = layer.LocalToMap(layer.ToLocal(new Vector2(worldX, -2000f)));
            var endCell = layer.LocalToMap(layer.ToLocal(new Vector2(worldX, 4000f)));

            for (int y = startCell.Y; y <= endCell.Y; y++)
            {
                var cell = new Vector2I(startCell.X, y);

                if (layer.GetCellSourceId(cell) == -1)
                {
                    continue;
                }

                var cellTop = layer.ToGlobal(layer.MapToLocal(cell)).Y - tileSize / 2f;

                return new Vector2(worldX, cellTop - halfBodyHeight);
            }

            return new Vector2(worldX, 0f);
        }

        #endregion

        #region Core - Spawn de player e NPC

        public void SpawnPlayer(Player player)
        {
            player.AddToGroup("players");
            player.SetMultiplayerAuthority(1);

            var parent = ResolveParent(ChunkStreamingConstants.UPSIDEDOWN_ID);

            if (parent == null)
            {
                GD.PushError($"[DimensionManager.SpawnPlayer] parent nulo, nao da pra adicionar {player.Name}");

                return;
            }

            parent.AddChild(player);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SpawnPlayerReceive(long peerId, Vector2 position, Godot.Collections.Dictionary data)
        {
            var player = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn").Instantiate<Player>();

            player.Name = $"Player{peerId}";
            player.Position = position;
            player.PeerId = peerId;
            player.Data = GodotDictionaryParser.ToResource<PlayerData>(data);

            SpawnPlayer(player);
        }

        public void SpawnPlayerRequest(Player player)
        {
            Rpc(nameof(SpawnPlayerReceive), player.PeerId, player.Position, GodotDictionaryParser.ToDictionary(player.Data));
        }

        public void SpawnPlayerRequest(Player player, long targetPeerId)
        {
            RpcId(targetPeerId, nameof(SpawnPlayerReceive), player.PeerId, player.Position, GodotDictionaryParser.ToDictionary(player.Data));
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SpawnNpcReceive(Vector2 position)
        {
            var parent = ResolveParent(ChunkStreamingConstants.UPSIDEDOWN_ID);

            if (parent == null || parent.GetNodeOrNull("NPC_Dummy") != null)
            {
                return;
            }

            var npc = GD.Load<PackedScene>("res://Scenes/World/Characters/NPC.tscn").Instantiate<Player>();

            npc.Name = "NPC_Dummy";
            npc.Position = position;

            npc.AddToGroup("players");
            npc.SetMultiplayerAuthority(1);

            parent.AddChild(npc);
        }

        public void SpawnNpcRequest(Vector2 position, long targetPeerId)
        {
            RpcId(targetPeerId, nameof(SpawnNpcReceive), position);
        }

        public void SpawnTestNPC()
        {
            var parent = ResolveParent(ChunkStreamingConstants.UPSIDEDOWN_ID);

            if (parent == null || parent.GetNodeOrNull("NPC_Dummy") != null)
            {
                return;
            }

            var npc = GD.Load<PackedScene>("res://Scenes/World/Characters/NPC.tscn").Instantiate<Player>();

            npc.Name = "NPC_Dummy";
            npc.Position = FindGroundSpawnPosition(ChunkStreamingConstants.UPSIDEDOWN_ID, 200f);

            npc.AddToGroup("players");
            npc.SetMultiplayerAuthority(1);

            parent.AddChild(npc);
        }

        #endregion

        #region Core - Spawn generico

        // Um caminho para qualquer entidade de mundo. O EntityData descreve tudo: a cena a
        // instanciar, onde, e o estado - que continua vivo depois do node morrer.
        //
        // Entidade nova nao precisa de metodo nem de RPC aqui: precisa de uma cena e de um
        // EntityData. Player e NPC ficam de fora, sao conteudo de sessao.
        // Chaves do record. Sao o que o node nao consegue declarar sozinho: a cena de onde ele
        // veio, a identidade, a dimensao e a posicao (que e do Node2D, nao dele).
        public const string RECORD_SCENE = "ScenePath";
        public const string RECORD_DIMENSION = "DimensionId";
        public const string RECORD_INSTANCE = "InstanceId";
        public const string RECORD_POSITION = "Position";

        // O NOME do no e a identidade. Deterministico de proposito: RPC do Godot resolve por
        // caminho, entao o mesmo no precisa ter o mesmo nome em todos os peers.
        public static string EntityNameOf(long instanceId)
        {
            return $"E{instanceId}";
        }

        public static long InstanceIdOf(Node node)
        {
            var name = node?.Name.ToString();

            return name != null && name.Length > 1 && name[0] == 'E' && long.TryParse(name[1..], out var id)
                ? id
                : 0;
        }

        public static Godot.Collections.Dictionary WriteVector(Vector2 value)
        {
            return new Godot.Collections.Dictionary { { "x", value.X }, { "y", value.Y } };
        }

        public static Vector2 ReadVector(Godot.Collections.Dictionary record, string key)
        {
            if (record == null || !record.TryGetValue(key, out var raw))
            {
                return Vector2.Zero;
            }

            var dict = raw.AsGodotDictionary();

            return new Vector2(
                dict.TryGetValue("x", out var x) ? x.AsSingle() : 0f,
                dict.TryGetValue("y", out var y) ? y.AsSingle() : 0f);
        }

        // Instancia e coloca no lugar. Se o node ja existir com essa identidade, nao duplica.
        public Node2D Spawn(Godot.Collections.Dictionary record)
        {
            var node = Build(record);

            if (node == null)
            {
                return null;
            }

            var parent = ResolveParent(record[RECORD_DIMENSION].AsString());

            if (parent == null)
            {
                node.QueueFree();

                return null;
            }

            parent.AddChild(node);

            return node;
        }

        // Instancia SEM anexar na arvore. E o que o streaming usa pra carregar o mundo do disco:
        // o node existe e guarda o proprio estado, mas nao processa ate ser pendurado.
        public Node2D Build(Godot.Collections.Dictionary record)
        {
            if (record == null || !record.ContainsKey(RECORD_SCENE))
            {
                return null;
            }

            var instanceId = record.TryGetValue(RECORD_INSTANCE, out var id) ? id.AsInt64() : 0;

            if (instanceId != 0 && FindByInstanceId(instanceId) != null)
            {
                return null;
            }

            var scene = GD.Load<PackedScene>(record[RECORD_SCENE].AsString());

            if (scene == null)
            {
                return null;
            }

            var node = scene.Instantiate<Node2D>();

            GodotDictionaryParser.ApplyTo(node, record);

            node.Position = ReadVector(record, RECORD_POSITION);
            node.Name = EntityNameOf(instanceId);

            return node;
        }

        // Cria no lado autoritativo e replica. targetPeerId != 0 manda so pra um (catch-up).
        public void SpawnRequest(Godot.Collections.Dictionary record, long targetPeerId = 0)
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
            {
                return;
            }

            if (targetPeerId == 0)
            {
                Rpc(nameof(SpawnReceive), record);
            }
            else
            {
                RpcId(targetPeerId, nameof(SpawnReceive), record);
            }
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SpawnReceive(Godot.Collections.Dictionary record)
        {
            Spawn(record);
        }

        public void DespawnRequest(long instanceId)
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
            {
                DespawnReceive(instanceId);

                return;
            }

            Rpc(nameof(DespawnReceive), instanceId);
        }

        // Tira o no de UM peer so. O servidor continua com o dele e continua simulando.
        public void DespawnForPeer(long targetPeerId, long instanceId)
        {
            if (Multiplayer != null && Multiplayer.HasMultiplayerPeer())
            {
                RpcId(targetPeerId, nameof(DespawnReceive), instanceId);
            }
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void DespawnReceive(long instanceId)
        {
            FindByInstanceId(instanceId)?.QueueFree();
        }

        public Node2D FindByInstanceId(long instanceId)
        {
            if (instanceId == 0)
            {
                return null;
            }

            var name = EntityNameOf(instanceId);

            foreach (var parent in Parents)
            {
                var node = parent?.GetNodeOrNull<Node2D>(name);

                if (node != null)
                {
                    return node;
                }
            }

            return null;
        }

        #endregion

        #region Core - Item no chao

        // Dropar item: instancia, poe no mundo e deixa o streaming cuidar do resto. O
        // registro acontece sozinho no _EnterTree.
        public long SpawnWorldItemRequest(ItemData itemData, Vector2 position, string dimensionId)
        {
            if (itemData == null)
            {
                return 0;
            }

            var instanceId = InstanceIdGenerator.NextInstanceId();

            var record = new Godot.Collections.Dictionary
            {
                { RECORD_SCENE, "res://Scenes/World/Items/WorldItem.tscn" },
                { RECORD_DIMENSION, dimensionId },
                { RECORD_INSTANCE, instanceId },
                { RECORD_POSITION, WriteVector(position) },
                { "Item", GodotDictionaryParser.ToDictionary(itemData) },
            };

            Spawn(record);
            SpawnRequest(record);

            return instanceId;
        }

        // Recolher e ESQUECER: QueueFree, nao RemoveChild. O node sai do save e nao volta.
        public void RemoveWorldItemRequest(long worldItemId)
        {
            DespawnRequest(worldItemId);
        }

        #endregion

        #region Core - Spawn de prop

        public bool SpawnPropAuthoritative(string propId, Vector2 position, string dimensionId)
        {
            var layer = ResolveLayer(dimensionId);
            var parent = ResolveParent(dimensionId);

            if (layer == null || parent == null)
            {
                return false;
            }

            var cell = layer.LocalToMap(layer.ToLocal(position));

            if (layer.GetCellSourceId(cell) != -1 || layer.GetCellSourceId(cell + Vector2I.Down) == -1)
            {
                return false;
            }

            var definition = PropDB.Get(propId);

            if (definition == null)
            {
                return false;
            }

            var record = new Godot.Collections.Dictionary
            {
                { RECORD_SCENE, definition.ScenePath },
                { RECORD_DIMENSION, dimensionId },
                { RECORD_INSTANCE, InstanceIdGenerator.NextInstanceId() },
                { RECORD_POSITION, WriteVector(position) },
                { "PropId", propId },
            };

            Spawn(record);
            SpawnRequest(record);

            return true;
        }

        #endregion
    }
}
