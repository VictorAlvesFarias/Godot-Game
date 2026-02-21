using Godot;
using System;
using Jogo25D.Characters;
using Jogo25D.Constants;

namespace Jogo25D.Systems
{
	public partial class NetworkManager : Node
{
	private const int MaxPlayers = 4;
	private const int DefaultPort = 9876;
	private const string DefaultAddress = "127.0.0.1";
	
	[Export] public PackedScene PlayerScene;
	
	private ENetMultiplayerPeer peer;
	private Node2D spawnParent;
	
	public static NetworkManager Instance { get; private set; }
	
	public override void _Ready()
	{
		Instance = this;
		
		PlayerScene = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn");
		
		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed += OnConnectionFailed;
		Multiplayer.ServerDisconnected += OnServerDisconnected;

		spawnParent = GetTree().Root.GetNode<Node2D>("Main/World/Levels/Overword");
	}

	public void CreateServer(int port = DefaultPort)
	{
		peer = new ENetMultiplayerPeer();
		Error error = peer.CreateServer(port, MaxPlayers);
		
		if (error != Error.Ok)
		{
			return;
		}
		
		Multiplayer.MultiplayerPeer = peer;
		
		RemoveInitialPlayer();
		
		SpawnPlayer(1, new Vector2(960, 300));
	}
	
	public void JoinServer(string address = DefaultAddress, int port = DefaultPort)
	{
		peer = new ENetMultiplayerPeer();
		Error error = peer.CreateClient(address, port);
		
		if (error != Error.Ok)
		{
			return;
		}
		
		Multiplayer.MultiplayerPeer = peer;
		
		RemoveInitialPlayer();
	}
	
	private void RemoveInitialPlayer()
	{
		var initialPlayer = spawnParent?.GetNodeOrNull<Player>("Player");

		if (initialPlayer != null)
		{
			initialPlayer.QueueFree();
		}
	}
	
	public void Disconnect()
	{
		if (peer != null)
		{
			peer.Close();
			peer = null;
		}
		
		var players = GetTree().GetNodesInGroup("players");
		foreach (Node player in players)
		{
			player.QueueFree();
		}
	}
	
	public bool IsConnected()
	{
		return peer != null && peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;
	}
	
	public bool IsServer()
	{
		return Multiplayer.IsServer();
	}
	
	private void SpawnPlayer(long peerId, Vector2 position)
	{
		if (PlayerScene == null || spawnParent == null)
			return;
			
		var player = PlayerScene.Instantiate<Player>();
		player.Name = $"Player{peerId}";
		player.Position = position;
		
		player.AddToGroup("players");
		
		player.SetMultiplayerAuthority((int)peerId);
		
		spawnParent.AddChild(player);
	}
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SpawnPlayerOnClient(long peerId, Vector2 position)
	{
		SpawnPlayer(peerId, position);
	}
	
	private void OnPeerConnected(long id)
	{
		if (Multiplayer.IsServer())
		{
			Vector2 spawnPos = new Vector2(
				GD.RandRange(400, 1520),
				300
			);
			
			SpawnPlayer(id, spawnPos);
			Rpc(nameof(SpawnPlayerOnClient), id, spawnPos);
			
			var players = GetTree().GetNodesInGroup("players");
			foreach (Node node in players)
			{
				if (node is Player player && player.Name != $"Player{id}")
				{
					string playerName = player.Name;
					long existingPlayerId = long.Parse(playerName.Replace("Player", ""));
					
					RpcId(id, nameof(SpawnPlayerOnClient), existingPlayerId, player.Position);
				}
			}
		}
	}
	
	private void OnPeerDisconnected(long id)
	{
		var playerNode = spawnParent.GetNodeOrNull($"Player{id}");
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
		peer = null;
	}
	
	private void OnServerDisconnected()
	{
		Disconnect();
	}
	}
}
