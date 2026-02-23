using Godot;
using System;
using Jogo25D.Characters;
using Jogo25D.Constants;
using System.Linq;

namespace Jogo25D.Systems
{
	public partial class WorldManager : Node
	{
		public static int MAX_PLAYER = 4;
		public static int DEFAULT_PORT = 9876;
		public static string DEFAULT_ADDRESS = "127.0.0.1";
		public static string DEFAULT_NODE_PATH = "/root/Main/Managers/WorldManager";

		private ENetMultiplayerPeer Peer { get; set; }
		private Node2D OverwordParent { get; set; }
		private Node2D UpsidedownParent { get; set; }
		private SubViewportContainer OverContainer { get; set; }
		private SubViewportContainer UpContainer { get; set; }

		#region Node methods

		public override void _Ready()
		{
			GD.Print("[WorldManager._Ready] _Ready()");

			Multiplayer.PeerConnected += OnPeerConnected;
			Multiplayer.PeerDisconnected += OnPeerDisconnected;
			Multiplayer.ConnectedToServer += OnConnectedToServer;
			Multiplayer.ConnectionFailed += OnConnectionFailed;
			Multiplayer.ServerDisconnected += OnServerDisconnected;

			var overwordParentPath = "Main/World/Levels/OverwordViewportContainer/OverwordViewport/Overword";

			OverwordParent = GetTree().Root.GetNodeOrNull<Node2D>(overwordParentPath);

			if (OverwordParent == null)
			{
				GD.Print($"[WorldManager._Ready] GetNodeOrNull: OverwordParent not found at path {overwordParentPath}");
			}
			else
			{
				GD.Print($"[WorldManager._Ready] OverwordParent found: {OverwordParent.Name}");
			}

			var upsidedownParentPath = "Main/World/Levels/UpsidedownViewportContainer/UpsidedownViewport/Upsidedown";

			UpsidedownParent = GetTree().Root.GetNodeOrNull<Node2D>(upsidedownParentPath);

			if (UpsidedownParent == null)
			{
				GD.Print($"[WorldManager._Ready] GetNodeOrNull: UpsidedownParent not found at path {upsidedownParentPath}");
			}
			else
			{
				GD.Print($"[WorldManager._Ready] UpsidedownParent found: {UpsidedownParent.Name}");
			}

			var overContainerPath = "Main/World/Levels/OverwordViewportContainer";

			OverContainer = GetTree().Root.GetNodeOrNull<SubViewportContainer>(overContainerPath);

			if (OverContainer == null)
			{
				GD.Print($"[WorldManager._Ready] GetNodeOrNull: OverContainer not found at path {overContainerPath}");
			}
			else
			{
				GD.Print($"[WorldManager._Ready] OverContainer found: {OverContainer.Name}");
			}

			var upContainerPath = "Main/World/Levels/UpsidedownViewportContainer";

			UpContainer = GetTree().Root.GetNodeOrNull<SubViewportContainer>(upContainerPath);

			if (UpContainer == null)
			{
				GD.Print($"[WorldManager._Ready] GetNodeOrNull: UpContainer not found at path {upContainerPath}");
			}
			else
			{
				GD.Print($"[WorldManager._Ready] UpContainer found: {UpContainer.Name}");
			}
		}

		#endregion

		#region Lobby methods

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

			var player = OverwordParent?.GetNodeOrNull<Player>("Player");

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

		public string JoinServer(string textAddress)
		{
			var ip = textAddress.Split(":")[0];
			var textPort = textAddress.Split(":")[1];
			var port = 0;

			if (string.IsNullOrEmpty(ip))
			{
				ip = DEFAULT_ADDRESS;
			}

			if (!string.IsNullOrEmpty(textPort))
			{
				if (!int.TryParse(textPort, out port))
				{
					port = DEFAULT_PORT;
				}
			}

			GD.Print($"[WorldManager.JoinServer] JoinServer(address={ip}, port={port})");
			
			Peer = new ENetMultiplayerPeer();

			if (Peer.CreateClient(ip, port) != Error.Ok)
			{
				GD.Print("[WorldManager.JoinServer] failed to create client");
				
				return "";
			}

			Multiplayer.MultiplayerPeer = Peer;

			var localPlayer = OverwordParent?.GetNodeOrNull<Player>("Player");

			if (localPlayer != null)
			{
				localPlayer.QueueFree();
				
				GD.Print("[WorldManager.JoinServer] local player queued for free");
			}
			else
			{	
				GD.Print("[WorldManager.JoinServer] no local player to remove");	
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
		}

		#endregion

		#region Private player mananger 

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
		public void SpawnPlayer(long peerId, Vector2 position)
		{	
			GD.Print($"[WorldManager.SpawnPlayer] SpawnPlayer(peerId={peerId}, position={position})");
			
			var player = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn").Instantiate<Player>();

			player.Name = $"Player{peerId}";
			player.Position = position;
			player.PeerId = peerId;

			player.AddToGroup("players");
			player.SetMultiplayerAuthority(1); 

			if (peerId != 1)
			{
				player.SetMultiplayerAuthority((int)peerId);
			}

			if (OverwordParent != null)
			{
				OverwordParent.AddChild(player);
				
				GD.Print($"[WorldManager.SpawnPlayer] spawned {player.Name}");	
			}
			else
			{
				GD.Print($"[WorldManager.SpawnPlayer] WARNING: OverwordParent is null, cannot add {player.Name}");
			}
		}

		public Player GetLocalPlayer()
		{
			GD.Print("[WorldManager.GetLocalPlayer] GetLocalPlayer()");
			
			var players = GetTree().GetNodesInGroup("players").OfType<Player>();
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

			var found = players.FirstOrDefault(p => p.PeerId == localPeerId);
			
			GD.Print($"[WorldManager.GetLocalPlayer] found={(found!=null)}");
			
			return found;
		}

		#endregion

		#region  Reset player

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
			player.CurrentHealth = player.MaxHealth;
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

		#region Dimension trade system

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void TradeDimension(long targetPeerId)
		{	
			GD.Print($"[WorldManager.SyncDimensionTrade] SyncDimensionTrade(targetPeerId={targetPeerId}) starting");	

			var playerNode = OverwordParent.GetChildren().OfType<Player>().FirstOrDefault(e => e.PeerId == targetPeerId);
			bool isCurrentlyInOverworld = playerNode != null;
			
			GD.Print($"[WorldManager.SyncDimensionTrade] Found player in Overworld? {isCurrentlyInOverworld}");

			if (playerNode == null)
			{
				playerNode = UpsidedownParent.GetChildren().OfType<Player>().FirstOrDefault(e => e.PeerId == targetPeerId);
				isCurrentlyInOverworld = false;
				
				GD.Print($"[WorldManager.SyncDimensionTrade] Found player in Upsidedown? {playerNode != null}");
			}

			if (playerNode == null)
			{
				
				GD.Print($"[WorldManager.SyncDimensionTrade] Player {targetPeerId} not found in any world!");
				
				return;
			}

			Node2D newParent = isCurrentlyInOverworld ? UpsidedownParent : OverwordParent;
			
			GD.Print($"[WorldManager.SyncDimensionTrade] Moving player {playerNode.Name} to {newParent.Name}");
			
			playerNode.Reparent(newParent, true);
			
			GD.Print($"[WorldManager.SyncDimensionTrade] Player {playerNode.Name} reparented successfully!");
			
			if (targetPeerId == Multiplayer.GetUniqueId() || (!IsConnected() && targetPeerId == 1))
			{
				GD.Print($"[WorldManager.SyncDimensionTrade] updating local UI for target {targetPeerId}");
				
				OverContainer.Visible = !isCurrentlyInOverworld;
				UpContainer.Visible = isCurrentlyInOverworld;
				
				GD.Print($"[WorldManager.SyncDimensionTrade] OverContainer.Visible={OverContainer.Visible}, UpContainer.Visible={UpContainer.Visible}");
			}
			
			GD.Print($"[WorldManager.SyncDimensionTrade] finished for Peer {targetPeerId}");
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

		#region Multplayer manager events

		private void OnPeerConnected(long id)
		{
			GD.Print($"[WorldManager.OnPeerConnected] OnPeerConnected(id={id})");
			
			if (!Multiplayer.IsServer())
			{
				return;
			}

			var spawnPos = Vector2.Zero;

			SpawnPlayer(id, spawnPos);

			Rpc(nameof(SpawnPlayer), id, spawnPos);

			var players = GetTree().GetNodesInGroup("players");

			foreach (Node node in players)
			{
				if (node is Player player && player.PeerId != id)
				{
					var playerName = player.Name;
					
					GD.Print($"[WorldManager.OnPeerConnected] informing {id} about {playerName}");
					

					RpcId(id, nameof(SpawnPlayer), player.PeerId, player.Position);
				}
			}
		}

		private void OnPeerDisconnected(long id)
		{	
			GD.Print($"[WorldManager.OnPeerDisconnected] OnPeerDisconnected(id={id})");
			
			var playerNode = UpsidedownParent.GetNodeOrNull($"Player{id}");

			if (playerNode == null)
			{
				GD.Print($"[WorldManager.OnPeerDisconnected] Player{id} not found in UpsidedownParent");
			}

			if (playerNode != null)
			{
				playerNode.QueueFree();
				
				GD.Print($"[WorldManager.OnPeerDisconnected] removed Player{id}");
			}
		}

		private void OnConnectedToServer()
		{
			GD.Print("[WorldManager.OnConnectedToServer] OnConnectedToServer()");	
		}

		private void OnConnectionFailed()
		{	
			GD.Print("[WorldManager.OnConnectionFailed] OnConnectionFailed()");
			
			Peer = null;
			
			GD.Print("[WorldManager.OnConnectionFailed] peer reset");	
		}

		private void OnServerDisconnected()
		{
			GD.Print("[WorldManager.OnServerDisconnected] OnServerDisconnected()");
			
			Disconnect();
		}

		#endregion

		#region Helpers 

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

		#endregion
	}
}
