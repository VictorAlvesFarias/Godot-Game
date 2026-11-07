using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Items;
using Jogo25D.Save.Resources;
using Jogo25D.Utils.GodotDictionaryParser;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Characters
{
    public static class Players
    {
        #region Properties

        private const ulong LocalMissRetryMsec = 250;

        private static Player _local;
        private static ulong _localMissUntil;
        private static ulong _peerFrame = ulong.MaxValue;
        private static long _peerId;

        #endregion

        #region Core - Consulta

        public static List<Player> All()
        {
            return GameLoop.Tree?.GetNodesInGroup("players").OfType<Player>().ToList() ?? new List<Player>();
        }

        // Jogador local guardado: HUD, menus e minimapa perguntam a cada quadro. So varre o grupo quando o guardado deixa
        // de valer (saiu da arvore, foi liberado ou o peer mudou), e uma busca sem resultado nao se repete por
        // LocalMissRetryMsec (um jogador entrando na arvore ou trocando de peer limpa isso na hora: ForgetLocal).
        public static Player GetLocal()
        {
            var peerId = LocalPeerId();

            if (_local != null && GodotObject.IsInstanceValid(_local) && _local.IsInsideTree() && _local.PeerId == peerId)
            {
                return _local;
            }

            if (Time.GetTicksMsec() < _localMissUntil)
            {
                return null;
            }

            _local = FindByPeerId(peerId);
            _localMissUntil = _local == null ? Time.GetTicksMsec() + LocalMissRetryMsec : 0;

            return _local;
        }

        public static void ForgetLocal()
        {
            _local = null;
            _localMissUntil = 0;
        }

        public static Player FindByPeerId(long peerId)
        {
            return All().FirstOrDefault(p => p.PeerId == peerId);
        }

        public static List<Player> InDimension(string dimensionId)
        {
            var entities = Dimension.Get(dimensionId)?.Entities;

            if (entities == null)
            {
                return new List<Player>();
            }

            return All().Where(p => p.GetParent() == entities).ToList();
        }

        #endregion

        #region Core - Spawn

        public static void Spawn(Player player)
        {
            player.AddToGroup("players");
            player.SetMultiplayerAuthority(1);

            var entities = Dimension.Get(ChunkStreamingConstants.UPSIDEDOWN_ID)?.Entities;

            if (entities == null)
            {
                GD.PushError($"[Players.Spawn] Entities nulo, nao da pra adicionar {player.Name}");

                return;
            }

            entities.AddChild(player);
        }

        public static void ApplySpawn(long peerId, Vector2 position, Godot.Collections.Dictionary data)
        {
            var player = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn").Instantiate<Player>();

            player.Name = $"Player{peerId}";
            player.Position = position;
            player.PeerId = peerId;

            GodotDictionaryParser.ApplyTo(player, data);

            Spawn(player);
        }

        public static void SpawnLocal(CharacterSaveData character)
        {
            var spawnDimension = Dimension.Get(ChunkStreamingConstants.UPSIDEDOWN_ID);
            var localPlayer = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn").Instantiate<Player>();

            localPlayer.Name = "Player";
            localPlayer.PeerId = 1;
            localPlayer.Position = spawnDimension?.FindGroundSpawnPosition(0f) ?? Vector2.Zero;

            if (character != null)
            {
                localPlayer.CharacterId = character.CharacterId;

                GodotDictionaryParser.ApplyTo(localPlayer, character.State);

                localPlayer.Loaded = true;
            }
            else
            {
                localPlayer.GiveItem(ItemFactory.CreateInstance("portal"));
            }

            Spawn(localPlayer);
            SpawnNpc(spawnDimension?.FindGroundSpawnPosition(200f) ?? Vector2.Zero);

            GD.Print("[Players.SpawnLocal] respawned local solo player");
        }

        public static void SpawnNpc(Vector2 position)
        {
            var entities = Dimension.Get(ChunkStreamingConstants.UPSIDEDOWN_ID)?.Entities;

            if (entities == null || entities.GetNodeOrNull("NPC_Dummy") != null)
            {
                return;
            }

            var npc = GD.Load<PackedScene>("res://Scenes/World/Characters/NPC.tscn").Instantiate<Player>();

            npc.Name = "NPC_Dummy";
            npc.Position = position;

            npc.AddToGroup("players");
            npc.SetMultiplayerAuthority(1);

            entities.AddChild(npc);
        }

        #endregion

        #region Core - Troca de sessao

        public static void PromoteLocalToHost()
        {
            var player = Dimension.Get(ChunkStreamingConstants.UPSIDEDOWN_ID)?.Entities?.GetNodeOrNull<Player>("Player");

            if (player == null)
            {
                GD.Print("[Players.PromoteLocalToHost] local player not found");

                return;
            }

            player.PeerId = 1;
            player.Name = $"Player{player.PeerId}";

            player.SetMultiplayerAuthority((int)player.PeerId);
            player.AddToGroup("players");

            GD.Print($"[Players.PromoteLocalToHost] set authority to {player.PeerId} and renamed to {player.Name}");
        }

        public static void ClearLocalBeforeJoin()
        {
            var entities = Dimension.Get(ChunkStreamingConstants.UPSIDEDOWN_ID)?.Entities;

            if (entities == null)
            {
                return;
            }

            var localPlayer = entities.GetNodeOrNull<Player>("Player");

            if (localPlayer != null)
            {
                localPlayer.QueueFree();

                GD.Print("[Players.ClearLocalBeforeJoin] local player queued for free");
            }

            var localNpc = entities.GetNodeOrNull("NPC_Dummy");

            if (localNpc != null)
            {
                localNpc.QueueFree();

                GD.Print("[Players.ClearLocalBeforeJoin] local NPC queued for free");
            }
        }

        #endregion

        #region Core - Rpc

        public static void SpawnRequest(Player player)
        {
            Dimension.Get(ChunkStreamingConstants.UPSIDEDOWN_ID)?.Rpc(
                nameof(Dimension.SpawnPlayerReceive), player.PeerId, player.Position, GodotDictionaryParser.ToDictionary(player));
        }

        public static void SpawnRequest(Player player, long targetPeerId)
        {
            Dimension.Get(ChunkStreamingConstants.UPSIDEDOWN_ID)?.RpcId(
                targetPeerId, nameof(Dimension.SpawnPlayerReceive), player.PeerId, player.Position, GodotDictionaryParser.ToDictionary(player));
        }

        public static void SpawnNpcRequest(Vector2 position, long targetPeerId)
        {
            Dimension.Get(ChunkStreamingConstants.UPSIDEDOWN_ID)?.RpcId(targetPeerId, nameof(Dimension.SpawnNpcReceive), position);
        }

        #endregion

        #region Utils - Peer local

        // O peer local muda no maximo entre quadros: lido uma vez por quadro.
        private static long LocalPeerId()
        {
            var frame = Engine.GetProcessFrames();

            if (frame != _peerFrame)
            {
                _peerFrame = frame;
                _peerId = ReadLocalPeerId();
            }

            return _peerId;
        }

        private static long ReadLocalPeerId()
        {
            var multiplayer = GameLoop.Multiplayer;

            if (
                multiplayer != null &&
                multiplayer.MultiplayerPeer != null &&
                multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected
            )
            {
                return multiplayer.GetUniqueId();
            }

            return 1;
        }

        #endregion
    }
}
