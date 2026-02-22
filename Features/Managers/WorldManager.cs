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
			Peer = new ENetMultiplayerPeer();

			if (Peer.CreateServer(port, MaxPlayers) != Error.Ok)
			{
				return;
			}

			Multiplayer.MultiplayerPeer = Peer;

			var player = OverwordParent?.GetNodeOrNull<Player>("Player");

			player.SetMultiplayerAuthority((int)player.PeerId);
		}

		public void JoinServer(string address = DefaultAddress, int port = DefaultPort)
		{
			Peer = new ENetMultiplayerPeer();

			if (Peer.CreateClient(address, port) != Error.Ok)
			{
				return;
			}

			Multiplayer.MultiplayerPeer = Peer;

			OverwordParent?.GetNodeOrNull<Player>("Player")?.QueueFree();
		}
		
		public void Disconnect()
		{
			if (Peer != null)
			{
				Peer.Close();

				Peer = null;
			}

			var players = GetTree().GetNodesInGroup("players");

			foreach (Node player in players)
			{
				player.QueueFree();
			}
		}

		#endregion

		#region Private player mananger 

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
		private void SpawnPlayer(long peerId, Vector2 position)
		{
			var player = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn").Instantiate<Player>();

			player.Name = $"Player{peerId}";
			player.Position = position;
			player.PeerId = peerId;

			player.AddToGroup("players");

			OverwordParent.AddChild(player);
		}

		private Player GetLocalPlayer()
		{
			var players = GetTree().GetNodesInGroup("players").OfType<Player>();
			var localPeerId = 1;

			if (
				Multiplayer != null &&
				Multiplayer.MultiplayerPeer != null &&
				Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected
			)
			{
				localPeerId = Multiplayer.GetUniqueId(); 
			}

			return players.FirstOrDefault(p => p.PeerId == localPeerId);
		}

		#endregion

		#region Dimension Trade System

		public void RequestLocalPlayerTradeDimension()
		{
			GD.Print($"[RequestLocalPlayerTradeDimension] Sending trade request to server (Peer 1)");
			RpcId(1, nameof(ServerReceiveTradeRequest));
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void ServerReceiveTradeRequest()
		{
			GD.Print($"[ServerReceiveTradeRequest] Received trade request");

			if (!Multiplayer.IsServer())
			{
				GD.Print("[ServerReceiveTradeRequest] Not the server, ignoring request");
				return;
			}

			long senderId = Multiplayer.GetRemoteSenderId();
			GD.Print($"[ServerReceiveTradeRequest] SenderId={senderId}, sending SyncDimensionTrade RPC");

			Rpc(nameof(SyncDimensionTrade), senderId);
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void SyncDimensionTrade(long targetPeerId)
		{
			GD.Print($"[SyncDimensionTrade] Starting for Peer {targetPeerId}");

			var playerNode = OverwordParent.GetChildren().OfType<Player>().FirstOrDefault(e => e.PeerId == targetPeerId);
			bool isCurrentlyInOverworld = playerNode != null;
			GD.Print($"[SyncDimensionTrade] Found player in Overworld? {isCurrentlyInOverworld}");

			if (playerNode == null)
			{
				playerNode = UpsidedownParent.GetChildren().OfType<Player>().FirstOrDefault(e => e.PeerId == targetPeerId);
				isCurrentlyInOverworld = false;
				GD.Print($"[SyncDimensionTrade] Found player in Upsidedown? {playerNode != null}");
			}

			if (playerNode == null)
			{
				GD.Print($"[SyncDimensionTrade] Player {targetPeerId} not found in any world!");
				return;
			}

			Node2D newParent = isCurrentlyInOverworld ? UpsidedownParent : OverwordParent;
			GD.Print($"[SyncDimensionTrade] Moving player {playerNode.Name} to {newParent.Name}");

			playerNode.Reparent(newParent, true);
			GD.Print($"[SyncDimensionTrade] Player {playerNode.Name} reparented successfully!");

			if (targetPeerId == Multiplayer.GetUniqueId() || (!IsConnected() && targetPeerId == 1))
			{
				OverContainer.Visible = !isCurrentlyInOverworld;
				UpContainer.Visible = isCurrentlyInOverworld;
				GD.Print($"[SyncDimensionTrade] Updating local UI: OverContainer.Visible={OverContainer.Visible}, UpContainer.Visible={UpContainer.Visible}");
			}

			GD.Print($"[SyncDimensionTrade] Finished for Peer {targetPeerId}");
		}

		#endregion

		#region Multplayer manager events

		private void OnPeerConnected(long id)
		{
			if (!Multiplayer.IsServer())
			{
				return;
			}

			var spawnPos = Vector2.Zero;
			
			RpcId(id, nameof(SpawnPlayer), id, spawnPos);

			var players = GetTree().GetNodesInGroup("players");

			foreach (Node node in players)
			{
				if (node is Player player)
				{
					var playerName = player.Name;

					RpcId(id, nameof(SpawnPlayer), player.PeerId, player.Position);
				}
			}
		}

		private void OnPeerDisconnected(long id)
		{
			var playerNode = UpsidedownParent.GetNodeOrNull($"Player{id}");

			if (playerNode != null)
			{
				playerNode.QueueFree();
			}
		}

		private void OnConnectedToServer()
		{
		}

		private void OnConnectionFailed()
		{
			Peer = null;
		}

		private void OnServerDisconnected()
		{
			Disconnect();
		}

		#endregion

		#region Helpers 

		public bool IsConnected()
		{
			return Peer != null && Peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;
		}

		public bool IsServer()
		{
			return Multiplayer.IsServer();
		}

		#endregion
	}
}
