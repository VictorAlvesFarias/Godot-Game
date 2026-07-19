using Godot;
using System;
using Jogo25D.Characters;
using Jogo25D.Features.World.Characters.Resources;
using Jogo25D.Features.World.Items.Resources;
using System.Linq;
using Jogo25D.Items;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Systems
{
	public partial class WorldManager : Node
	{
		#region Properties

		public static int MAX_PLAYER = 4;
		public static int DEFAULT_PORT = 9876;
		public static string DEFAULT_ADDRESS = "127.0.0.1";
		public static string DEFAULT_NODE_PATH = "/root/Main/Managers/WorldManager";

		public ENetMultiplayerPeer Peer { get; set; }
		public Node2D OverworldParent { get; set; }
		public Node2D UpsidedownParent { get; set; }
		public SubViewportContainer OverContainer { get; set; }
		public SubViewportContainer UpContainer { get; set; }
		private Node2D ProceduralParent { get; set; }
        private SubViewportContainer ProceduralContainer { get; set; }

        #endregion

        #region Systems

		private Inventory Inventory { get; set; } = new Inventory();

        #endregion

        #region Godot implementation

        public override void _Ready()
		{
			GD.Print("[WorldManager._Ready] _Ready()");

			Multiplayer.PeerConnected += OnPeerConnected;
			Multiplayer.PeerDisconnected += OnPeerDisconnected;
			Multiplayer.ConnectedToServer += OnConnectedToServer;
			Multiplayer.ConnectionFailed += OnConnectionFailed;
			Multiplayer.ServerDisconnected += OnServerDisconnected;

			// A cena World não faz mais parte de Main.tscn por padrão — ela só
			// existe depois que o jogador escolhe "Criar Mundo"/"Conectar" na
			// tela de seleção. Por isso não resolvemos os paths aqui.
		}

        #endregion

        #region Core - World spawning

		private void ResolveWorldReferences()
		{
			var overworldParentPath = "Main/World/Levels/OverworldViewportContainer/OverworldViewport/Overworld";

			OverworldParent = GetTree().Root.GetNodeOrNull<Node2D>(overworldParentPath);

			if (OverworldParent == null)
			{
				GD.Print($"[WorldManager.ResolveWorldReferences] GetNodeOrNull: OverworldParent not found at path {overworldParentPath}");
			}
			else
			{
				GD.Print($"[WorldManager.ResolveWorldReferences] OverworldParent found: {OverworldParent.Name}");
			}

			var upsidedownParentPath = "Main/World/Levels/UpsidedownViewportContainer/UpsidedownViewport/Upsidedown";

			UpsidedownParent = GetTree().Root.GetNodeOrNull<Node2D>(upsidedownParentPath);

			if (UpsidedownParent == null)
			{
				GD.Print($"[WorldManager.ResolveWorldReferences] GetNodeOrNull: UpsidedownParent not found at path {upsidedownParentPath}");
			}
			else
			{
				GD.Print($"[WorldManager.ResolveWorldReferences] UpsidedownParent found: {UpsidedownParent.Name}");
			}

            var proceduralParentPath = "Main/World/Levels/ProceduralViewportContainer/ProceduralViewport/Procedural";

            ProceduralParent = GetTree().Root.GetNodeOrNull<Node2D>(proceduralParentPath);

            if (ProceduralParent == null)
            {
                GD.Print($"[WorldManager.ResolveWorldReferences] GetNodeOrNull: ProceduralParent not found at path {proceduralParentPath}");
            }
            else
            {
                GD.Print($"[WorldManager.ResolveWorldReferences] ProceduralParent found: {ProceduralParent.Name}");
            }

            var overContainerPath = "Main/World/Levels/OverworldViewportContainer";

			OverContainer = GetTree().Root.GetNodeOrNull<SubViewportContainer>(overContainerPath);

			if (OverContainer == null)
			{
				GD.Print($"[WorldManager.ResolveWorldReferences] GetNodeOrNull: OverContainer not found at path {overContainerPath}");
			}
			else
			{
				GD.Print($"[WorldManager.ResolveWorldReferences] OverContainer found: {OverContainer.Name}");
			}

            var upContainerPath = "Main/World/Levels/UpsidedownViewportContainer";

			UpContainer = GetTree().Root.GetNodeOrNull<SubViewportContainer>(upContainerPath);

			if (UpContainer == null)
			{
				GD.Print($"[WorldManager.ResolveWorldReferences] GetNodeOrNull: UpContainer not found at path {upContainerPath}");
			}
			else
			{
				GD.Print($"[WorldManager.ResolveWorldReferences] UpContainer found: {UpContainer.Name}");
			}

            var proceduralContainerPath = "Main/World/Levels/ProceduralViewportContainer";

			ProceduralContainer = GetTree().Root.GetNodeOrNull<SubViewportContainer>(proceduralContainerPath);

            if (ProceduralContainer == null)
            {
                GD.Print($"[WorldManager.ResolveWorldReferences] GetNodeOrNull: ProceduralContainer not found at path {proceduralContainerPath}");
            }
            else
            {
                GD.Print($"[WorldManager.ResolveWorldReferences] UpContainer found: {ProceduralContainer.Name}");
            }
		}

		public void SpawnWorld()
		{
			if (OverworldParent != null)
			{
				return;
			}

			var main = GetTree().Root.GetNodeOrNull<Node>("Main");

			if (main == null || main.HasNode("World"))
			{
				ResolveWorldReferences();

				return;
			}

			var world = GD.Load<PackedScene>("res://Scenes/World/World.tscn").Instantiate<Node2D>();

			main.AddChild(world);

			ResolveWorldReferences();

			GD.Print("[WorldManager.SpawnWorld] world instantiated");
		}

		public void SpawnLocalWorldAndPlayer()
		{
			SpawnWorld();

			RespawnLocalSoloPlayer();
		}

		public string SpawnWorldAndJoin(string textAddress)
		{
			SpawnWorld();

			return JoinServer(textAddress);
		}

        private void SpawnTestNPC()
        {
            if (OverworldParent == null || OverworldParent.GetNodeOrNull("NPC_Dummy") != null)
            {
                return;
            }

            var npc = GD.Load<PackedScene>("res://Scenes/World/Characters/NPC.tscn").Instantiate<Player>();

            npc.Name = "NPC_Dummy";
            npc.Position = new Vector2(200, 0);

            npc.SetMultiplayerAuthority(1);

            OverworldParent.AddChild(npc);
        }

        #endregion

        #region Core - Connection

		public string CreateServer(string textPort)
		{
			var port = DEFAULT_PORT;

			if (!string.IsNullOrEmpty(textPort))
			{
				if (!int.TryParse(textPort, out port))
				{
					port = DEFAULT_PORT;
				}
			}

			GD.Print($"[WorldManager.CreateServer] CreateServer(port={port})");

			Peer = new ENetMultiplayerPeer();

			if (Peer.CreateServer(port, MAX_PLAYER) != Error.Ok)
			{
				GD.Print("[WorldManager.CreateServer] failed to create server");

				return "";
			}

			Multiplayer.MultiplayerPeer = Peer;

			var player = OverworldParent?.GetNodeOrNull<Player>("Player");

			if (player == null)
			{
				GD.Print("[WorldManager.CreateServer] local player not found");	
			}
			else
			{
				player.PeerId = 1; 
				player.Name = $"Player{player.PeerId}";

				player.SetMultiplayerAuthority((int)player.PeerId);
				player.AddToGroup("players");

				GD.Print($"[WorldManager.CreateServer] set authority to {player.PeerId} and renamed to {player.Name}");				
			}

			return port.ToString();
		}

		public string LastJoinError { get; private set; } = "";

		public string JoinServer(string textAddress)
		{
			LastJoinError = "";

            var ip = DEFAULT_ADDRESS;
            var port = DEFAULT_PORT;

            if (!string.IsNullOrWhiteSpace(textAddress))
            {
                var parts = textAddress.Split(':');

                if (parts.Length > 1)
                {
                    if (string.IsNullOrWhiteSpace(parts[0]) || !int.TryParse(parts[1], out port))
                    {
                        LastJoinError = "Formato de endereço inválido (esperado IP:Porta ou apenas Porta).";

                        return "";
                    }

                    ip = parts[0];
                }
                else if (!int.TryParse(parts[0], out port))
                {
                    LastJoinError = "Formato de endereço inválido (esperado IP:Porta ou apenas Porta).";

                    return "";
                }
            }

            GD.Print($"[WorldManager.JoinServer] JoinServer(address={ip}, port={port})");

			Peer = new ENetMultiplayerPeer();

			var createError = Peer.CreateClient(ip, port);

			if (createError != Error.Ok)
			{
				LastJoinError = $"ENetMultiplayerPeer.CreateClient retornou: {createError}";

				GD.Print($"[WorldManager.JoinServer] failed to create client: {createError}");

				return "";
			}

			Multiplayer.MultiplayerPeer = Peer;

			var localPlayer = OverworldParent?.GetNodeOrNull<Player>("Player");

			if (localPlayer != null)
			{
				localPlayer.QueueFree();

				GD.Print("[WorldManager.JoinServer] local player queued for free");
			}
			else
			{
				GD.Print("[WorldManager.JoinServer] no local player to remove");
			}

			var localNpc = OverworldParent?.GetNodeOrNull("NPC_Dummy");

			if (localNpc != null)
			{
				localNpc.QueueFree();

				GD.Print("[WorldManager.JoinServer] local NPC queued for free");
			}

			return $"{ip}:{port}";
		}
		
		public void Disconnect()
		{
			GD.Print("[WorldManager.Disconnect] Disconnect()");
			
			if (Peer != null)
			{
				Peer.Close();

				Peer = null;
				
				GD.Print("[WorldManager.Disconnect] peer closed");
			}

			var players = GetTree().GetNodesInGroup("players");

			foreach (Node player in players)
			{
				player.QueueFree();
			}

			GD.Print($"[WorldManager.Disconnect] freed {players.Count} player nodes");

			CallDeferred(nameof(RespawnLocalSoloPlayer));
		}

		public void LeaveWorld()
		{
			GD.Print("[WorldManager.LeaveWorld] LeaveWorld()");

			if (Peer != null)
			{
				Peer.Close();

				Peer = null;

				GD.Print("[WorldManager.LeaveWorld] peer closed");
			}

			var main = GetTree().Root.GetNodeOrNull<Node>("Main");
			var world = main?.GetNodeOrNull("World");

			if (world != null)
			{
				world.QueueFree();

				GD.Print("[WorldManager.LeaveWorld] world queued for free");
			}

			OverworldParent = null;
			UpsidedownParent = null;
			ProceduralParent = null;
			OverContainer = null;
			UpContainer = null;
			ProceduralContainer = null;
		}

		private void RespawnLocalSoloPlayer()
		{
			var localPlayer = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn").Instantiate<Player>();

			localPlayer.Name = "Player";
			localPlayer.PeerId = 1;

			SpawnPlayer(localPlayer);

			SpawnTestNPC();

			GD.Print("[WorldManager.Disconnect] respawned local solo player");
		}

        #endregion

        #region Core - Rpc - Player spawn

		public void SpawnPlayer(Player player)
		{
			GD.Print($"[WorldManager.SpawnPlayer] SpawnPlayer(peerId={player.PeerId}, position={player.Position}, equippedItemId={player.Data.EquippedItemId})");

			player.AddToGroup("players");
			player.SetMultiplayerAuthority(1);

			if (OverworldParent != null)
			{
				OverworldParent.AddChild(player);

				GD.Print($"[WorldManager.SpawnPlayer] spawned {player.Name}");
			}
			else
			{
				GD.Print($"[WorldManager.SpawnPlayer] WARNING: OverworldParent is null, cannot add {player.Name}");
			}
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void SpawnPlayerReceive(long peerId, Vector2 position, Godot.Collections.Dictionary data)
		{
			GD.Print($"[WorldManager.SpawnPlayerReceive] peerId={peerId} position={position}");

			var player = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn").Instantiate<Player>();

			player.Name = $"Player{peerId}";
			player.Position = position;
			player.PeerId = peerId;
			player.Data = GodotDictionaryParser.ToResource<PlayerData>(data);

			SpawnPlayer(player);
		}

		public void SpawnPlayerRequest(Player player)
		{
			var data = GodotDictionaryParser.ToDictionary(player.Data);

			Rpc(nameof(SpawnPlayerReceive), player.PeerId, player.Position, data);
		}

		public void SpawnPlayerRequest(Player player, long targetPeerId)
		{
			var data = GodotDictionaryParser.ToDictionary(player.Data);

			RpcId(targetPeerId, nameof(SpawnPlayerReceive), player.PeerId, player.Position, data);
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void SpawnNpcReceive(Vector2 position)
		{
			GD.Print($"[WorldManager.SpawnNpcReceive] position={position}");

			if (OverworldParent == null || OverworldParent.GetNodeOrNull("NPC_Dummy") != null)
			{
				return;
			}

			var npc = GD.Load<PackedScene>("res://Scenes/World/Characters/NPC.tscn").Instantiate<Player>();

			npc.Name = "NPC_Dummy";
			npc.Position = position;

			npc.AddToGroup("players");
			npc.SetMultiplayerAuthority(1);

			OverworldParent.AddChild(npc);
		}

		public void SpawnNpcRequest(Vector2 position, long targetPeerId)
		{
			RpcId(targetPeerId, nameof(SpawnNpcReceive), position);
		}

		#endregion

		#region Core - Rpc - World items

		public void SpawnWorldItem(WorldItem item)
		{
			if (OverworldParent != null)
			{
				OverworldParent.AddChild(item);
			}
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void SpawnWorldItemReceive(long worldItemId, Godot.Collections.Dictionary data, Vector2 position)
		{
			GD.Print($"[WorldManager.SpawnWorldItemReceive] worldItemId={worldItemId} position={position}");

			if (OverworldParent == null || FindWorldItem(worldItemId) != null)
			{
				return;
			}

			var worldItem = GD.Load<PackedScene>("res://Scenes/World/Items/WorldItem.tscn").Instantiate<WorldItem>();

			worldItem.Name = $"WorldItem{worldItemId}";
			worldItem.WorldItemId = worldItemId;
			worldItem.Data = GodotDictionaryParser.ToResource<ItemDefinitionData>(data);
			worldItem.Position = position;

			SpawnWorldItem(worldItem);
		}

		public long SpawnWorldItemRequest(ItemDefinitionData item, Vector2 position)
		{
			var worldItemId = ItemDB.NextInstanceId();

			var worldItem = GD.Load<PackedScene>("res://Scenes/World/Items/WorldItem.tscn").Instantiate<WorldItem>();

			worldItem.Name = $"WorldItem{worldItemId}";
			worldItem.WorldItemId = worldItemId;
			worldItem.Data = item;
			worldItem.Position = position;

			SpawnWorldItem(worldItem);

			var data = GodotDictionaryParser.ToDictionary(item);

			Rpc(nameof(SpawnWorldItemReceive), worldItemId, data, position);

			return worldItemId;
		}

		public void SpawnWorldItemRequest(WorldItem item, long targetPeerId)
		{
			var data = GodotDictionaryParser.ToDictionary(item.Data);

			RpcId(targetPeerId, nameof(SpawnWorldItemReceive), item.WorldItemId, data, item.Position);
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

		public WorldItem FindWorldItem(long worldItemId)
		{
			return OverworldParent?.GetNodeOrNull<WorldItem>($"WorldItem{worldItemId}");
		}

		#endregion

		#region Core - Player lookup

		public Player GetLocalPlayer()
		{
			GD.Print("[WorldManager.GetLocalPlayer] GetLocalPlayer()");

			var localPeerId = 1;

			if (
				Multiplayer != null &&
				Multiplayer.MultiplayerPeer != null &&
				Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected
			)
			{
				localPeerId = Multiplayer.GetUniqueId();
				GD.Print($"[WorldManager.GetLocalPlayer] localPeerId={localPeerId}");
			}

			return FindPlayerByPeerId(localPeerId);
		}

		public Player FindPlayerByPeerId(long peerId)
		{
			var players = GetTree().GetNodesInGroup("players").OfType<Player>();
			var found = players.FirstOrDefault(p => p.PeerId == peerId);

			GD.Print($"[WorldManager.FindPlayerByPeerId] peerId={peerId} found={(found != null)}");

			return found;
		}

        #endregion

        #region Core - Rpc - Player state

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void ResetPlayer(long peerId, Vector2 position)
		{
			var player = GetTree().GetNodesInGroup("players").OfType<Player>().FirstOrDefault(e => e.PeerId == peerId);

			if (player == null)
			{
				GD.Print("[WorldManager.ResetPlayer] player is null");

				return;
			}

			player.GlobalPosition = Vector2.Zero;
			player.Velocity = Vector2.Zero;
			player.Data.CurrentHealth = player.GetMaxHealth();
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void ResetPlayerServerReceive()
		{
			GD.Print("[WorldManager.ResetPlayerServerReceive] received trade request");

			if (!Multiplayer.IsServer())
			{

				GD.Print("[WorldManager.ResetPlayerServerReceive] not the server, ignoring request");

				return;
			}

			long senderId = Multiplayer.GetRemoteSenderId();

			GD.Print($"[WorldManager.ResetPlayerServerReceive] SenderId={senderId}, sending SyncDimensionTrade RPC");

			Rpc(nameof(ResetPlayer), senderId, Vector2.Zero);
		}

		public void ResetPlayerClientRequest()
		{
			GD.Print("[WorldManager.RequestLocalPlayerTradeDimension] sending trade request to server (Peer 1)");

			RpcId(1, nameof(ResetPlayerServerReceive));
		}

        #endregion

        #region Core - Rpc - Dimension trade

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void TradeDimension(long targetPeerId)
        {
            GD.Print($"[WorldManager.TradeDimension] targetPeerId={targetPeerId}");

            var playerNode = GetTree().GetNodesInGroup("players").OfType<Player>().FirstOrDefault(p => p.PeerId == targetPeerId);

            if (playerNode == null) return;

            Node2D currentParent = playerNode.GetParent<Node2D>();
            Node2D nextParent;

            if (currentParent == OverworldParent)
            {
                nextParent = UpsidedownParent;
            }
            else if (currentParent == UpsidedownParent)
            {
                nextParent = ProceduralParent;
            }
            else
            {
                nextParent = OverworldParent;
            }

            GD.Print($"[WorldManager] Moving player from {currentParent.Name} to {nextParent.Name}");

            playerNode.Reparent(nextParent, true);

			var equippedItemId = playerNode.Data.EquippedItemId;

			if (equippedItemId > 0)
			{
				playerNode.EquipItemRequest(equippedItemId);
			}

            if (targetPeerId == Multiplayer.GetUniqueId())
            {
                OverContainer.Visible = (nextParent == OverworldParent);
                UpContainer.Visible = (nextParent == UpsidedownParent);
                ProceduralContainer.Visible = (nextParent == ProceduralParent);
            }
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void TradeDimensionServerReceive()
		{
			GD.Print("[WorldManager.ServerReceiveTradeRequest] received trade request");

			if (!Multiplayer.IsServer())
			{
				
				GD.Print("[WorldManager.ServerReceiveTradeRequest] not the server, ignoring request");
				
				return;
			}

			long senderId = Multiplayer.GetRemoteSenderId();
			
			GD.Print($"[WorldManager.ServerReceiveTradeRequest] SenderId={senderId}, sending SyncDimensionTrade RPC");
			
			Rpc(nameof(TradeDimension), senderId);
		}

		public void TradeDimensionClientRequest()
		{
			GD.Print("[WorldManager.RequestLocalPlayerTradeDimension] sending trade request to server (Peer 1)");
			
			RpcId(1, nameof(TradeDimensionServerReceive));
		}

        #endregion

        #region Core - Peer events

		public void OnPeerConnected(long id)
		{
			GD.Print($"[WorldManager.OnPeerConnected] OnPeerConnected(id={id})");
			
			if (!Multiplayer.IsServer())
			{
				return;
			}

            var player = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn").Instantiate<Player>();

            player.Name = $"Player{id}";
			player.Position = Godot.Vector2.Zero;
            player.PeerId = id;

            var startingWeapon = ItemDB.CreateInstance("bow_starting2");

			player.GiveItem(startingWeapon);

            player.Data.EquippedItemId = startingWeapon.InstanceId;

			SpawnPlayer(player);

            SpawnPlayerRequest(player);

			var players = GetTree().GetNodesInGroup("players");

			foreach (Node node in players)
			{
				if (node is NPC)
				{
					continue;
				}

				if (node is Player existingPlayer && existingPlayer.PeerId != id)
				{
					GD.Print($"[WorldManager.OnPeerConnected] informing {id} about {existingPlayer.Name}");

					SpawnPlayerRequest(existingPlayer, id);
				}
			}

			var npc = OverworldParent?.GetNodeOrNull<Player>("NPC_Dummy");

			if (npc != null)
			{
				GD.Print($"[WorldManager.OnPeerConnected] informing {id} about NPC_Dummy");

				SpawnNpcRequest(npc.Position, id);
			}

			var worldItems = OverworldParent?.GetChildren().OfType<WorldItem>() ?? Enumerable.Empty<WorldItem>();

			foreach (var worldItem in worldItems)
			{
				GD.Print($"[WorldManager.OnPeerConnected] informing {id} about {worldItem.Name}");

				SpawnWorldItemRequest(worldItem, id);
			}
		}

		public void OnPeerDisconnected(long id)
		{
			GD.Print($"[WorldManager.OnPeerDisconnected] OnPeerDisconnected(id={id})");

			var playerNode = FindPlayerByPeerId(id);

			if (playerNode == null)
			{
				GD.Print($"[WorldManager.OnPeerDisconnected] Player{id} not found");
			}

			if (playerNode != null)
			{
				playerNode.QueueFree();

				GD.Print($"[WorldManager.OnPeerDisconnected] removed Player{id}");
			}
		}

		public event Action ConnectionSucceeded;
		public event Action ConnectionAttemptFailed;

		public void OnConnectedToServer()
		{
			GD.Print("[WorldManager.OnConnectedToServer] OnConnectedToServer()");

			ConnectionSucceeded?.Invoke();
		}

		public void OnConnectionFailed()
		{
			GD.Print("[WorldManager.OnConnectionFailed] OnConnectionFailed()");

			Peer = null;

			GD.Print("[WorldManager.OnConnectionFailed] peer reset");

			ConnectionAttemptFailed?.Invoke();
		}

		public void OnServerDisconnected()
		{
			GD.Print("[WorldManager.OnServerDisconnected] OnServerDisconnected()");
			
			Disconnect();
		}

        #endregion

        #region Utils

		public bool IsConnected()
		{
			var connected = Peer != null && Peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;

			return connected;
		}

		public bool IsServer()
		{
			var isServer = Multiplayer.IsServer();

			return isServer;
		}

		public SubViewportContainer GetContainerForParent(Node2D parent)
		{
			if (parent == OverworldParent)
			{
				return OverContainer;
			}

			if (parent == UpsidedownParent)
			{
				return UpContainer;
			}

			if (parent == ProceduralParent)
			{
				return ProceduralContainer;
			}

			return null;
		}

        #endregion

	}
}
