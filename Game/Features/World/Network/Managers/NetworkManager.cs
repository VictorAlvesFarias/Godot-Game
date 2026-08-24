using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Features.World.Characters.Resources;
using Jogo25D.Items;
using Jogo25D.Systems;
using Jogo25D.UI;
using Jogo25D.Utils.GodotDictionaryParser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Network
{
    // Tudo que e peer: abrir servidor, entrar, cair, e a negociacao de personagem no join.
    // Existe como manager porque peer nao e no da arvore - nao ha entidade pra receber esses RPC.
    public partial class NetworkManager : Node
    {
		#region Events

		// O SaveManager assina pra persistir o personagem de quem caiu - o NetworkManager
		// so avisa, nao sabe o que e personagem.
		public event Action<long, Player> PeerLeft;

		// Emitido antes de fechar a conexao. Quem tem o que persistir assina - o canal nao sabe
		// o que e save nem o que e personagem.
		public event Action Disconnecting;

		// Servidor caiu: quem decide pra onde a UI vai e a sessao.
		public event Action ServerDisconnected;
		public event Action ConnectionSucceeded;
		public event Action ConnectionAttemptFailed;

		#endregion

		#region Dinamic properties

		public ENetMultiplayerPeer Peer { get; set; }


		private static DimensionManager Dimensions => Game.Managers.DimensionManager.Node;

		#endregion

		#region Core - Sessao de rede

		// Encerra a sessao de rede: usado por quem sai do mundo, sem precisar conhecer o peer.
		public void CloseSession()
		{
			if (Peer == null)
			{
				return;
			}

			Peer.Close();

			Peer = null;
			Multiplayer.MultiplayerPeer = null;

			GD.Print("[NetworkManager.CloseSession] peer closed");
		}

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			Multiplayer.PeerConnected += OnPeerConnected;
			Multiplayer.PeerDisconnected += OnPeerDisconnected;
			Multiplayer.ConnectedToServer += OnConnectedToServer;
			Multiplayer.ConnectionFailed += OnConnectionFailed;
			Multiplayer.ServerDisconnected += OnServerDisconnected;
		}

		#endregion

		#region Core - Connection

		public string CreateServer(string textPort)
		{
			var port = NetworkingConstants.DEFAULT_PORT;

			if (!string.IsNullOrEmpty(textPort))
			{
				if (!int.TryParse(textPort, out port))
				{
					port = NetworkingConstants.DEFAULT_PORT;
				}
			}

			GD.Print($"[NetworkManager.CreateServer] CreateServer(port={port})");

			Peer = new ENetMultiplayerPeer();

			if (Peer.CreateServer(port, NetworkingConstants.MAX_PLAYER) != Error.Ok)
			{
				GD.Print("[NetworkManager.CreateServer] failed to create server");

				return "";
			}

			Multiplayer.MultiplayerPeer = Peer;

			var player = Dimensions.ResolveParent(ChunkStreamingConstants.UPSIDEDOWN_ID)?.GetNodeOrNull<Player>("Player");

			if (player == null)
			{
				GD.Print("[NetworkManager.CreateServer] local player not found");
			}
			else
			{
				player.PeerId = 1;
				player.Name = $"Player{player.PeerId}";

				player.SetMultiplayerAuthority((int)player.PeerId);
				player.AddToGroup("players");

				GD.Print($"[NetworkManager.CreateServer] set authority to {player.PeerId} and renamed to {player.Name}");
			}

			return port.ToString();
		}

		public string LastJoinError { get; private set; } = "";

		public string JoinServer(string textAddress)
		{
			LastJoinError = "";

			var ip = NetworkingConstants.DEFAULT_ADDRESS;
			var port = NetworkingConstants.DEFAULT_PORT;

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

			GD.Print($"[NetworkManager.JoinServer] JoinServer(address={ip}, port={port})");

			Peer = new ENetMultiplayerPeer();

			var createError = Peer.CreateClient(ip, port);

			if (createError != Error.Ok)
			{
				LastJoinError = $"ENetMultiplayerPeer.CreateClient retornou: {createError}";

				GD.Print($"[NetworkManager.JoinServer] failed to create client: {createError}");

				return "";
			}

			Multiplayer.MultiplayerPeer = Peer;

			var localPlayer = Dimensions.ResolveParent(ChunkStreamingConstants.UPSIDEDOWN_ID)?.GetNodeOrNull<Player>("Player");

			if (localPlayer != null)
			{
				localPlayer.QueueFree();

				GD.Print("[NetworkManager.JoinServer] local player queued for free");
			}
			else
			{
				GD.Print("[NetworkManager.JoinServer] no local player to remove");
			}

			var localNpc = Dimensions.ResolveParent(ChunkStreamingConstants.UPSIDEDOWN_ID)?.GetNodeOrNull("NPC_Dummy");

			if (localNpc != null)
			{
				localNpc.QueueFree();

				GD.Print("[NetworkManager.JoinServer] local NPC queued for free");
			}

			return $"{ip}:{port}";
		}

		public void Disconnect()
		{
			GD.Print("[NetworkManager.Disconnect] Disconnect()");

			Disconnecting?.Invoke();

			if (Peer != null)
			{
				Peer.Close();

				Peer = null;
				Multiplayer.MultiplayerPeer = null;

				GD.Print("[NetworkManager.Disconnect] peer closed");
			}

			var players = GetTree().GetNodesInGroup("players");

			foreach (Node player in players)
			{
				player.QueueFree();
			}

			GD.Print($"[NetworkManager.Disconnect] freed {players.Count} player nodes");

			Game.Managers.WorldManager.Node.CallDeferred("RespawnLocalSoloPlayer");
		}

		#endregion

		#region Core - Peer events

		public void OnPeerConnected(long id)
		{
			GD.Print($"[NetworkManager.OnPeerConnected] OnPeerConnected(id={id})");
		}


		public void OnPeerDisconnected(long id)
		{
			GD.Print($"[NetworkManager.OnPeerDisconnected] OnPeerDisconnected(id={id})");

			var playerNode = Game.Managers.WorldManager.Node.FindPlayerByPeerId(id);

			if (playerNode == null)
			{
				GD.Print($"[NetworkManager.OnPeerDisconnected] Player{id} not found");
			}

			PeerLeft?.Invoke(id, playerNode);

			if (playerNode != null)
			{
				playerNode.QueueFree();

				GD.Print($"[NetworkManager.OnPeerDisconnected] removed Player{id}");
			}


			Game.Managers.ChunkStreamingManager.Node?.RemovePeer(id);
		}

		public void OnConnectedToServer()
		{
			GD.Print("[NetworkManager.OnConnectedToServer] OnConnectedToServer()");

			ConnectionSucceeded?.Invoke();
		}

		public void OnConnectionFailed()
		{
			GD.Print("[NetworkManager.OnConnectionFailed] OnConnectionFailed()");

			Peer = null;
			Multiplayer.MultiplayerPeer = null;

			GD.Print("[NetworkManager.OnConnectionFailed] peer reset");

			ConnectionAttemptFailed?.Invoke();
		}

		public void OnServerDisconnected()
		{
			GD.Print("[NetworkManager.OnServerDisconnected] OnServerDisconnected()");

			ServerDisconnected?.Invoke();
		}

		#endregion

		#region Utils - Estado da conexao

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
