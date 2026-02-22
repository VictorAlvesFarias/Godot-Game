using Godot;
using System;
using Jogo25D.Characters;
using Jogo25D.Constants;
using System.Linq;

namespace Jogo25D.Systems
{
	public partial class WorldManager : Node
	{
		private const int MaxPlayers = 4;
		private const int DefaultPort = 9876;
		private const string DefaultAddress = "127.0.0.1";

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

			OverwordParent = GetTree().Root.GetNode<Node2D>("Main/World/Levels/OverwordViewportContainer/OverwordViewport/Overword");
			UpsidedownParent = GetTree().Root.GetNode<Node2D>("Main/World/Levels/UpsidedownViewportContainer/UpsidedownViewport/Upsidedown");
			OverContainer = GetTree().Root.GetNode<SubViewportContainer>("Main/World/Levels/OverwordViewportContainer");
			UpContainer = GetTree().Root.GetNode<SubViewportContainer>("Main/World/Levels/UpsidedownViewportContainer");
		}

		#endregion

		#region Lobby methods

		public void CreateServer(int port = DefaultPort)
		{
			GD.Print($"[WorldManager.CreateServer] CreateServer(port={port})");
			Peer = new ENetMultiplayerPeer();

			if (Peer.CreateServer(port, MaxPlayers) != Error.Ok)
			{
				GD.Print("[WorldManager.CreateServer] failed to create server");
				return;
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
		}

		public void JoinServer(string address = DefaultAddress, int port = DefaultPort)
		{
			GD.Print($"[WorldManager.JoinServer] JoinServer(address={address}, port={port})");
			Peer = new ENetMultiplayerPeer();

			if (Peer.CreateClient(address, port) != Error.Ok)
			{
				GD.Print("[WorldManager.JoinServer] failed to create client");
				return;
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

			OverwordParent.AddChild(player);
			GD.Print($"[WorldManager.SpawnPlayer] spawned {player.Name}");
		}

		private Player GetLocalPlayer()
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

		#region Dimension Trade System

		public void RequestLocalPlayerTradeDimension()
		{
			GD.Print("[WorldManager.RequestLocalPlayerTradeDimension] sending trade request to server (Peer 1)");
			RpcId(1, nameof(ServerReceiveTradeRequest));
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void ServerReceiveTradeRequest()
		{
			GD.Print("[WorldManager.ServerReceiveTradeRequest] received trade request");

			if (!Multiplayer.IsServer())
			{
				GD.Print("[WorldManager.ServerReceiveTradeRequest] not the server, ignoring request");
				return;
			}

			long senderId = Multiplayer.GetRemoteSenderId();
			GD.Print($"[WorldManager.ServerReceiveTradeRequest] SenderId={senderId}, sending SyncDimensionTrade RPC");

			Rpc(nameof(SyncDimensionTrade), senderId);
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void SyncDimensionTrade(long targetPeerId)
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
