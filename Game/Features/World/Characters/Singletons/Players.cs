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
        #region Core - Consulta

        public static List<Player> All()
        {
            return GameLoop.Tree?.GetNodesInGroup("players").OfType<Player>().ToList() ?? new List<Player>();
        }

        public static Player GetLocal()
        {
            return FindByPeerId(LocalPeerId());
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

        private static long LocalPeerId()
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
