using Godot;
using System;
using Jogo25D.Characters;

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
		
		// Carregar cena do player
		PlayerScene = GD.Load<PackedScene>("res://Scenes/Characters/Player.tscn");
		
		// Conectar sinais do multiplayer
		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed += OnConnectionFailed;
		Multiplayer.ServerDisconnected += OnServerDisconnected;
		
		// Encontrar ou criar o nó de spawn
		spawnParent = GetTree().Root.GetNode<Node2D>("Main");
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
		
		// Remover player inicial da cena
		RemoveInitialPlayer();
		
		// Spawnar o player local
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
		
		// Remover player inicial da cena
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
		
		// Remover todos os players
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
		
		// Adicionar ao grupo de players
		player.AddToGroup("players");
		
		// Configurar autoridade de rede
		player.SetMultiplayerAuthority((int)peerId);
		
		spawnParent.AddChild(player);
	}
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SpawnPlayerOnClient(long peerId, Vector2 position)
	{
		// Este método é chamado nos clientes para spawnar players
		SpawnPlayer(peerId, position);
	}
	
	// Sinais do multiplayer
	private void OnPeerConnected(long id)
	{
		// Se for o servidor, spawnar um player para o novo peer
		if (Multiplayer.IsServer())
		{
			// Calcular posição aleatória
			Vector2 spawnPos = new Vector2(
				GD.RandRange(400, 1520),
				300
			);
			
			// Spawnar o player do novo peer em todos os clientes (inclusive servidor)
			SpawnPlayer(id, spawnPos);
			Rpc(nameof(SpawnPlayerOnClient), id, spawnPos);
			
			// Enviar informações sobre os players existentes para o novo cliente
			var players = GetTree().GetNodesInGroup("players");
			foreach (Node node in players)
			{
				if (node is Player player && player.Name != $"Player{id}")
				{
					// Extrair o ID do nome do player
					string playerName = player.Name;
					long existingPlayerId = long.Parse(playerName.Replace("Player", ""));
					
					// Enviar para o novo cliente
					RpcId(id, nameof(SpawnPlayerOnClient), existingPlayerId, player.Position);
				}
			}
		}
	}
	
	private void OnPeerDisconnected(long id)
	{
		// Remover o player do peer desconectado
		var playerNode = spawnParent.GetNodeOrNull($"Player{id}");
		if (playerNode != null)
		{
			playerNode.QueueFree();
		}
	}
	
	private void OnConnectedToServer()
	{
		// O servidor irá spawnar nosso player
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
