using Godot;
using Jogo25D.Characters;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Entities;
using Jogo25D.Items;
using Jogo25D.Network;
using Jogo25D.Save;
using Jogo25D.Save.Resources;
using Jogo25D.Save.Types;
using Jogo25D.Systems;
using Jogo25D.Utils.GodotDictionaryParser;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Session
{
    public static class SessionContext
    {
        #region Dinamic properties

        public static WorldSaveData PendingWorld { get; private set; }

        public static CharacterSaveData PendingCharacter { get; set; }

        public static WorldSaveData CurrentWorldSave { get; set; }

        public static WorldCharacterMode CharacterMode { get; set; } = WorldCharacterMode.LocalCharacters;

        public static Godot.Collections.Array ServerCharacterSummaries { get; private set; } = new();

        public static event System.Action CharacterSelectionRequired;

        public static event System.Action SessionEnded;

        public static event System.Action WorldEntered;

        public static event System.Action LoadingStarted;

        public static event System.Action LoadingFinished;

        private static readonly Dictionary<long, CharacterSaveData> _peerCharacters = new();
        private static readonly Dictionary<long, string> _pendingProfileByPeer = new();

        private static bool _bound;

        public static IReadOnlyDictionary<long, CharacterSaveData> PeerCharacters => _peerCharacters;

        #endregion

        #region Core - Selecao de personagem

        public static void SetPendingWorld(WorldSaveData world)
        {
            PendingWorld = world;
            CharacterMode = world?.CharacterMode ?? WorldCharacterMode.LocalCharacters;
        }

        #endregion

        #region Core - Entrada

        public static void EnterWorldWith(CharacterSaveData character)
        {
            if (character == null)
            {
                return;
            }

            PendingCharacter = character;

            var save = PendingWorld ?? SaveStorage.CreateWorld("Mundo sem nome", (long)GD.Randi(), WorldCharacterMode.LocalCharacters, "", SavesConstants.DEFAULT_AUTOSAVE_INTERVAL_MINUTES);

            CurrentWorldSave = save;

            if (!save.IsProcedural)
            {
                SpawnAuthoredWorld(save, PendingCharacter);
            }
            else
            {
                SpawnProceduralWorld(save, PendingCharacter);
            }

            SaveContext.Register(save);
            SaveContext.Register(PendingCharacter);
            SaveContext.StartAutosave(save.AutosaveIntervalMinutes);

            SetPendingWorld(null);
        }

        public static string HostWorld(string textPort)
        {
            var port = NetworkContext.CreateServer(textPort);

            Players.PromoteLocalToHost();

            return port;
        }

        public static string SpawnWorldAndJoin(string textAddress)
        {
            SpawnWorld();

            var address = NetworkContext.JoinServer(textAddress);

            Players.ClearLocalBeforeJoin();

            return address;
        }

        private static void SpawnWorld()
        {
            if (Dimension.IsResolved)
            {
                return;
            }

            var main = GameLoop.Tree?.CurrentScene;

            if (main == null || main.HasNode("World"))
            {
                return;
            }

            var world = GD.Load<PackedScene>("res://Scenes/World/World.tscn").Instantiate<Node2D>();

            main.AddChild(world);

            GD.Print("[SessionContext.SpawnWorld] world instantiated");
        }

        private static void SpawnAuthoredWorld(WorldSaveData save, CharacterSaveData character)
        {
            SpawnWorld();

            var streaming = WorldStreaming.Current;

            streaming?.SetStreamingEnabled(false);
            streaming?.UseAuthoredWorlds();

            WorldDocument.Load(save);

            Players.SpawnLocal(character);

            WorldEntered?.Invoke();
        }

        private static async void SpawnProceduralWorld(WorldSaveData save, CharacterSaveData character)
        {
            SpawnWorld();

            Dimension.ClearAllEntities();
            Dimension.ClearAllLayers(discardBackground: true);

            var streaming = WorldStreaming.Current;

            if (streaming != null)
            {
                streaming.SetWorldSeed(save.Seed);

                streaming.UseProceduralWorlds();
            }

            WorldDocument.Load(save);

            streaming?.SetStreamingEnabled(true);

            LoadingStarted?.Invoke();

            var spawnDimension = Dimension.Get(ChunkStreamingConstants.UPSIDEDOWN_ID);

            if (spawnDimension != null)
            {
                await spawnDimension.PreloadSpawnAreaAsync(Vector2.Zero);
            }

            Players.SpawnLocal(character);

            LoadingFinished?.Invoke();

            WorldEntered?.Invoke();
        }

        #endregion

        #region Core - Saida

        public static void LeaveWorld()
        {
            SaveContext.SaveAll();
            SaveContext.StopAutosave();
            SaveContext.ClearRegistry();

            ResetState();

            NetworkContext.CloseSession();

            DespawnWorld();

            SessionEnded?.Invoke();
        }

        private static void DespawnWorld()
        {
            var streaming = WorldStreaming.Current;

            streaming?.ResetState();

            Dimension.ClearAllEntities();
            Dimension.ClearAllLayers();

            streaming?.ResetTileStreaming();
            streaming?.DropWorlds();
        }

        public static void ResetState()
        {
            CurrentWorldSave = null;
            PendingCharacter = null;

            CharacterMode = WorldCharacterMode.LocalCharacters;

            ServerCharacterSummaries = new Godot.Collections.Array();

            _peerCharacters.Clear();
            _pendingProfileByPeer.Clear();
        }

        #endregion

        private static void SyncCharacters()
        {
            var localPlayer = Players.GetLocal();

            if (PendingCharacter != null && localPlayer != null)
            {
                PendingCharacter.State = GodotDictionaryParser.ToDictionary(localPlayer);
            }

            foreach (var (peerId, character) in _peerCharacters)
            {
                var player = Players.FindByPeerId(peerId);

                if (player != null)
                {
                    character.State = GodotDictionaryParser.ToDictionary(player);
                }
            }
        }

        public static async void FinishPeerJoin(long id, CharacterSaveData character)
        {
            if (!GameLoop.Multiplayer.IsServer() || character == null)
            {
                return;
            }

            var player = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn").Instantiate<Player>();

            player.Name = $"Player{id}";
            player.Position = Godot.Vector2.Zero;
            player.PeerId = id;
            player.CharacterId = character.CharacterId;
            GodotDictionaryParser.ApplyTo(player, character.State);
            player.Loaded = true;

            var spawnDimension = Dimension.Get(ChunkStreamingConstants.UPSIDEDOWN_ID);

            if (spawnDimension == null)
            {
                return;
            }

            await spawnDimension.PreloadSpawnAreaAsync(player.Position);

            player.Position = spawnDimension.FindGroundSpawnPosition(player.Position.X);

            WorldStreaming.Current?.RpcId(id, nameof(WorldStreaming.ClearLayersReceive));
            WorldStreaming.Current?.CatchUpTiles(id, player.Position);
            Players.Spawn(player);
            Players.SpawnRequest(player);

            var players = GameLoop.Tree.GetNodesInGroup("players");

            foreach (Node node in players)
            {
                if (node is NPC)
                {
                    continue;
                }

                if (node is Player existingPlayer && existingPlayer.PeerId != id)
                {
                    GD.Print($"[NetworkContext.FinishPeerJoin] informing {id} about {existingPlayer.Name}");

                    Players.SpawnRequest(existingPlayer, id);
                }
            }

            var npc = Dimension.Get(ChunkStreamingConstants.UPSIDEDOWN_ID)?.Entities?.GetNodeOrNull<Player>("NPC_Dummy");

            if (npc != null)
            {
                GD.Print($"[NetworkContext.FinishPeerJoin] informing {id} about NPC_Dummy");

                Players.SpawnNpcRequest(npc.Position, id);
            }

            WorldStreaming.Current?.CatchUpPeer(id, player.Position);
        }

        #region Core - Personagem, join e politica de save

        static SessionContext()
        {
            NetworkContext.PeerLeft += OnPeerLeft;
            NetworkContext.Disconnecting += SaveContext.SaveAll;

            SaveContext.Saving += SyncCharacters;
        }

        public static void Bind()
        {
            if (_bound || GameLoop.Tree == null)
            {
                return;
            }

            _bound = true;

            GameLoop.Tree.Root.CloseRequested += SaveContext.SaveAll;

            if (GameLoop.Multiplayer != null)
            {
                GameLoop.Multiplayer.ServerDisconnected += LeaveWorld;
            }
        }

        private static void OnPeerLeft(long peerId, Jogo25D.Characters.Player playerNode)
        {
            SavePeerCharacterOnDisconnect(peerId, playerNode);

            _peerCharacters.Remove(peerId);
            _pendingProfileByPeer.Remove(peerId);
        }

        public static void ForgetCharacter(string characterId)
        {
            if (PendingCharacter?.CharacterId == characterId)
            {
                PendingCharacter = null;
            }
        }

        public static int? HostCharacterMode()
        {
            if (!GameLoop.Multiplayer.IsServer() || CurrentWorldSave == null)
            {
                return null;
            }

            return (int)CurrentWorldSave.CharacterMode;
        }

        public static void ApplyJoinInfo(int modeInt)
        {
            CharacterMode = (WorldCharacterMode)modeInt;

            if (CharacterMode == WorldCharacterMode.ServerCharacters)
            {
                return;
            }

            ServerCharacterSummaries = new Godot.Collections.Array();

            CharacterSelectionRequired?.Invoke();
        }

        public static void ApplyLocalCharacterSubmit(long senderId, string profileId, Godot.Collections.Dictionary characterDict)
        {
            if (!GameLoop.Multiplayer.IsServer() || CurrentWorldSave == null || CurrentWorldSave.CharacterMode != WorldCharacterMode.LocalCharacters)
            {
                return;
            }

            var character = Jogo25D.Utils.GodotDictionaryParser.GodotDictionaryParser.ToResource<CharacterSaveData>(characterDict);

            if (character == null)
            {
                character = SaveStorage.CreateLocalCharacter("Sem nome");
            }

            if (character == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(character.OwnerProfileId))
            {
                character.OwnerProfileId = profileId;
            }

            _peerCharacters[senderId] = character;

            FinishPeerJoin(senderId, character);
        }

        public static bool AcceptServerProfile(long senderId, string profileId)
        {
            if (!IsServerCharacterHost())
            {
                return false;
            }

            _pendingProfileByPeer[senderId] = profileId;

            return true;
        }

        private static bool OwnsCharacter(long peerId, string characterId)
        {
            var character = SaveStorage.LoadServerCharacter(CurrentWorldSave.MultiplayerKey, characterId);

            return character != null && OwnsProfile(peerId, character.OwnerProfileId);
        }

        private static bool OwnsProfile(long peerId, string ownerProfileId)
        {
            if (!_pendingProfileByPeer.TryGetValue(peerId, out var profileId) || string.IsNullOrEmpty(profileId))
            {
                return false;
            }

            return ownerProfileId == profileId;
        }

        public static Godot.Collections.Array ServerCharacterSummariesFor(long senderId)
        {
            var summaries = new Godot.Collections.Array();

            if (!_pendingProfileByPeer.TryGetValue(senderId, out var profileId) || CurrentWorldSave == null)
            {
                return summaries;
            }

            var characters = SaveStorage.ListServerCharacters(CurrentWorldSave.MultiplayerKey)
                .Where(c => c.OwnerProfileId == profileId)
                .ToList() ?? new List<CharacterSaveData>();

            foreach (var character in characters)
            {
                summaries.Add(new Godot.Collections.Dictionary
                {
                    ["CharacterId"] = character.CharacterId,
                    ["Name"] = character.Name,
                });
            }

            return summaries;
        }

        private static bool IsServerCharacterHost()
        {
            return GameLoop.Multiplayer.IsServer()
                && CurrentWorldSave != null
                && CurrentWorldSave.CharacterMode == WorldCharacterMode.ServerCharacters;
        }

        public static bool DeleteServerCharacter(long senderId, string characterId)
        {
            if (!IsServerCharacterHost() || !OwnsCharacter(senderId, characterId))
            {
                return false;
            }

            SaveStorage.DeleteServerCharacter(CurrentWorldSave.MultiplayerKey, characterId);

            return true;
        }

        public static void ApplyServerCharacterList(Godot.Collections.Array summaries)
        {
            ServerCharacterSummaries = summaries ?? new Godot.Collections.Array();

            CharacterSelectionRequired?.Invoke();
        }

        public static void ApplyServerCharacterSelect(long senderId, string characterId)
        {
            if (!IsServerCharacterHost())
            {
                return;
            }

            var character = SaveStorage.LoadServerCharacter(CurrentWorldSave.MultiplayerKey, characterId);

            if (character == null || !OwnsProfile(senderId, character.OwnerProfileId))
            {
                return;
            }

            _peerCharacters[senderId] = character;

            FinishPeerJoin(senderId, character);
        }

        public static void ApplyServerCharacterCreate(long senderId, string name)
        {
            if (!IsServerCharacterHost())
            {
                return;
            }

            var ownerProfileId = _pendingProfileByPeer.TryGetValue(senderId, out var profileId) ? profileId : "";
            var character = SaveStorage.CreateServerCharacter(CurrentWorldSave.MultiplayerKey, name, ownerProfileId);

            if (character == null)
            {
                return;
            }

            _peerCharacters[senderId] = character;

            FinishPeerJoin(senderId, character);
        }

        private static void SavePeerCharacterOnDisconnect(long id, Player playerNode)
        {
            if (playerNode == null || CurrentWorldSave == null || !_peerCharacters.TryGetValue(id, out var character))
            {
                return;
            }

            character.State = GodotDictionaryParser.ToDictionary(playerNode);
            character.LastPlayedUtc = SaveStorage.NowUtc();

            if (CurrentWorldSave.CharacterMode == WorldCharacterMode.ServerCharacters)
            {
                SaveStorage.SaveServerCharacter(CurrentWorldSave.MultiplayerKey, character);
            }
            else
            {
                SaveStorage.SaveBackup(character.OwnerProfileId, character);
            }
        }

        #endregion
    }
}
