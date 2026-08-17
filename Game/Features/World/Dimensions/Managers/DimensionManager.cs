using Godot;
using Jogo25D.Biomes;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
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

        public long NextPropId { get; set; }

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

        public SubViewportContainer ResolveContainer(string dimensionId)
        {
            var dimension = Resolve(dimensionId);

            return dimension?.Container != null && IsInstanceValid(dimension.Container) ? dimension.Container : null;
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

            NextPropId = 0;
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

        #region Core - Spawn de item no chao

        public void SpawnWorldItem(WorldItem item, string dimensionId)
        {
            ResolveParent(dimensionId)?.AddChild(item);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SpawnWorldItemReceive(long worldItemId, Godot.Collections.Dictionary data, Vector2 position, string dimensionId)
        {
            if (ResolveParent(dimensionId) == null || FindWorldItem(worldItemId) != null)
            {
                return;
            }

            var item = GD.Load<PackedScene>("res://Scenes/World/Items/WorldItem.tscn").Instantiate<WorldItem>();

            item.Name = $"WorldItem{worldItemId}";
            item.WorldItemId = worldItemId;
            item.Data = GodotDictionaryParser.ToResource<ItemData>(data);
            item.Position = position;

            SpawnWorldItem(item, dimensionId);
        }

        public long SpawnWorldItemRequest(ItemData itemData, Vector2 position, string dimensionId)
        {
            if (itemData == null)
            {
                return 0;
            }

            var item = GD.Load<PackedScene>("res://Scenes/World/Items/WorldItem.tscn").Instantiate<WorldItem>();
            var worldItemId = InstanceIdGenerator.NextInstanceId();

            item.Name = $"WorldItem{worldItemId}";
            item.WorldItemId = worldItemId;
            item.Data = itemData;
            item.Position = position;

            SpawnWorldItem(item, dimensionId);

            Rpc(nameof(SpawnWorldItemReceive), worldItemId, GodotDictionaryParser.ToDictionary(itemData), position, dimensionId);

            return worldItemId;
        }

        public void SpawnWorldItemRequest(WorldItem item, long targetPeerId)
        {
            var dimensionId = ResolveDimensionIdOf(item);

            RpcId(targetPeerId, nameof(SpawnWorldItemReceive), item.WorldItemId, GodotDictionaryParser.ToDictionary(item.Data), item.Position, dimensionId);
        }

        public WorldItem FindWorldItem(long worldItemId)
        {
            foreach (var parent in Parents)
            {
                var item = parent.GetNodeOrNull<WorldItem>($"WorldItem{worldItemId}");

                if (item != null)
                {
                    return item;
                }
            }

            return null;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void RemoveWorldItemReceive(long worldItemId)
        {
            FindWorldItem(worldItemId)?.QueueFree();
        }

        public void RemoveWorldItemRequest(long worldItemId)
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
            {
                RemoveWorldItemReceive(worldItemId);

                return;
            }

            Rpc(nameof(RemoveWorldItemReceive), worldItemId);
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

            SpawnProp(propId, parent, position, ++NextPropId);

            Rpc(nameof(SpawnPropBroadcast), propId, position, dimensionId, NextPropId);

            return true;
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SpawnPropBroadcast(string propId, Vector2 position, string dimensionId, long propInstanceId)
        {
            var parent = ResolveParent(dimensionId);

            if (parent == null)
            {
                return;
            }

            SpawnProp(propId, parent, position, propInstanceId);
        }

        public Prop SpawnProp(string propId, Node2D parent, Vector2 position, long propInstanceId)
        {
            var definition = PropDB.Get(propId);

            if (definition == null || parent == null)
            {
                return null;
            }

            if (definition.Spawn(parent, position) is not Prop prop)
            {
                return null;
            }

            prop.PropId = propId;
            prop.Name = $"{propId}{propInstanceId}";

            return prop;
        }

        public void RestoreProps(WorldSaveData save)
        {
            if (save?.Props == null)
            {
                return;
            }

            foreach (var propSave in save.Props)
            {
                var parent = ResolveParent(propSave.DimensionId);

                if (parent == null)
                {
                    continue;
                }

                SpawnProp(propSave.PropId, parent, new Vector2(propSave.PositionX, propSave.PositionY), ++NextPropId);
            }
        }

        public Godot.Collections.Array<PropSaveData> CollectProps()
        {
            var result = new Godot.Collections.Array<PropSaveData>();

            foreach (var dimensionId in _dimensions.Keys)
            {
                var parent = ResolveParent(dimensionId);

                if (parent == null)
                {
                    continue;
                }

                foreach (var prop in parent.GetChildren().OfType<Prop>())
                {
                    result.Add(new PropSaveData
                    {
                        PropId = prop.PropId,
                        PositionX = prop.Position.X,
                        PositionY = prop.Position.Y,
                        DimensionId = dimensionId,
                    });
                }
            }

            return result;
        }

        #endregion
    }
}
