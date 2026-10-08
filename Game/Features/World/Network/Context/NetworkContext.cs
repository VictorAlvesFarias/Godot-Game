using Godot;
using Jogo25D.Characters;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Entities;
using Jogo25D.Items;
using Jogo25D.Save.Resources;
using Jogo25D.Save.Types;
using Jogo25D.Systems;
using Jogo25D.Utils.GodotDictionaryParser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Network
{
    public static class NetworkContext
    {
        #region Events

        public static event Action<long, Player> PeerLeft;
        public static event Action Disconnecting;
        public static event Action ConnectionAttemptFailed;

        #endregion

        #region Dinamic properties

        public static ENetMultiplayerPeer Peer { get; set; }

        public static string CurrentPort { get; private set; } = "";

        public static string LastJoinError { get; private set; } = "";

        private static bool _bound;

        #endregion

        #region Core - Sessao de rede

        public static void CloseSession()
        {
            if (Peer == null)
            {
                return;
            }

            Peer.Close();

            Peer = null;
            GameLoop.Multiplayer.MultiplayerPeer = null;

            GD.Print("[NetworkContext.CloseSession] peer closed");
        }

        #endregion

        #region Core - Ciclo

        private static void EnsureBound()
        {
            if (_bound || GameLoop.Multiplayer == null)
            {
                return;
            }

            _bound = true;

            GameLoop.Multiplayer.PeerDisconnected += OnPeerDisconnected;
            GameLoop.Multiplayer.ConnectionFailed += OnConnectionFailed;
        }

        #endregion

        #region Core - Connection

        public static string CreateServer(string textPort)
        {
            EnsureBound();

            var port = NetworkingConstants.DEFAULT_PORT;

            if (!string.IsNullOrEmpty(textPort))
            {
                if (!int.TryParse(textPort, out port))
                {
                    port = NetworkingConstants.DEFAULT_PORT;
                }
            }

            GD.Print($"[NetworkContext.CreateServer] CreateServer(port={port})");

            Peer = new ENetMultiplayerPeer();

            if (Peer.CreateServer(port, NetworkingConstants.MAX_PLAYER) != Error.Ok)
            {
                GD.Print("[NetworkContext.CreateServer] failed to create server");

                return "";
            }

            GameLoop.Multiplayer.MultiplayerPeer = Peer;

            CurrentPort = port.ToString();

            return port.ToString();
        }

        public static string JoinServer(string textAddress)
        {
            EnsureBound();

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

            GD.Print($"[NetworkContext.JoinServer] JoinServer(address={ip}, port={port})");

            Peer = new ENetMultiplayerPeer();

            var createError = Peer.CreateClient(ip, port);

            if (createError != Error.Ok)
            {
                LastJoinError = $"ENetMultiplayerPeer.CreateClient retornou: {createError}";

                GD.Print($"[NetworkContext.JoinServer] failed to create client: {createError}");

                return "";
            }

            GameLoop.Multiplayer.MultiplayerPeer = Peer;

            return $"{ip}:{port}";
        }

        public static void Disconnect()
        {
            GD.Print("[NetworkContext.Disconnect] Disconnect()");

            Disconnecting?.Invoke();

            if (Peer != null)
            {
                Peer.Close();

                Peer = null;
                GameLoop.Multiplayer.MultiplayerPeer = null;

                GD.Print("[NetworkContext.Disconnect] peer closed");
            }

            var players = GameLoop.Tree.GetNodesInGroup("players");

            foreach (Node player in players)
            {
                player.QueueFree();
            }

            GD.Print($"[NetworkContext.Disconnect] freed {players.Count} player nodes");
        }

        #endregion

        #region Core - Peer events

        public static void OnPeerDisconnected(long id)
        {
            GD.Print($"[NetworkContext.OnPeerDisconnected] OnPeerDisconnected(id={id})");

            var playerNode = Players.FindByPeerId(id);

            if (playerNode == null)
            {
                GD.Print($"[NetworkContext.OnPeerDisconnected] Player{id} not found");
            }

            PeerLeft?.Invoke(id, playerNode);

            if (playerNode != null)
            {
                playerNode.QueueFree();

                GD.Print($"[NetworkContext.OnPeerDisconnected] removed Player{id}");
            }

            WorldStreaming.Current?.RemovePeer(id);
        }

        public static void OnConnectionFailed()
        {
            GD.Print("[NetworkContext.OnConnectionFailed] OnConnectionFailed()");

            Peer = null;
            GameLoop.Multiplayer.MultiplayerPeer = null;

            GD.Print("[NetworkContext.OnConnectionFailed] peer reset");

            ConnectionAttemptFailed?.Invoke();
        }

        #endregion

        #region Utils - StateOf da connection

        public static bool IsConnected()
        {
            var connected = Peer != null && Peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;

            return connected;
        }

        #endregion
    }
}
