using Godot;
using Jogo25D.Core;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Features.World.Characters.Resources;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Items;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Systems
{
    public partial class SaveManager : Node
    {
        #region Dinamic properties

        public ProfileData CachedProfile { get; set; }

        #endregion

        #region Core - Perfil

        public ProfileData GetOrCreateLocalProfile()
        {
            if (CachedProfile != null)
            {
                return CachedProfile;
            }

            if (ResourceLoader.Exists(SavesConstants.PROFILE_PATH))
            {
                CachedProfile = ResourceLoader.Load<ProfileData>(SavesConstants.PROFILE_PATH, cacheMode: ResourceLoader.CacheMode.Ignore);
            }

            if (CachedProfile == null)
            {
                CachedProfile = new ProfileData { ProfileId = Guid.NewGuid().ToString() };

                ResourceSaver.Save(CachedProfile, SavesConstants.PROFILE_PATH);
            }

            return CachedProfile;
        }

        #endregion

        #region Core - Personagens locais

        public List<CharacterSaveData> ListLocalCharacters()
        {
            return ListCharactersAt(SavesConstants.CHARACTERS_DIR);
        }

        public CharacterSaveData CreateLocalCharacter(string name)
        {
            var profile = GetOrCreateLocalProfile();
            var character = new CharacterSaveData
            {
                CharacterId = Guid.NewGuid().ToString(),
                OwnerProfileId = profile.ProfileId,
                MultiplayerKey = "",
                Name = name,
                Data = BuildStarterPlayerData(),
                CreatedUtc = NowUtc(),
                LastPlayedUtc = NowUtc(),
            };

            SaveLocalCharacter(character);

            return character;
        }

        public void SaveLocalCharacter(CharacterSaveData character)
        {
            EnsureDir(SavesConstants.CHARACTERS_DIR);

            ResourceSaver.Save(character, $"{SavesConstants.CHARACTERS_DIR}/{character.CharacterId}.tres");
        }

        public void DeleteLocalCharacter(string characterId)
        {
            DeleteIfExists($"{SavesConstants.CHARACTERS_DIR}/{characterId}.tres");
        }

        #endregion

        #region Core - Personagens de servidor

        public List<CharacterSaveData> ListServerCharacters(string multiplayerKey)
        {
            return ListCharactersAt(ServerCharactersDirFor(multiplayerKey));
        }

        public CharacterSaveData CreateServerCharacter(string multiplayerKey, string name, string ownerProfileId)
        {
            var character = new CharacterSaveData
            {
                CharacterId = Guid.NewGuid().ToString(),
                OwnerProfileId = ownerProfileId,
                MultiplayerKey = multiplayerKey,
                Name = name,
                Data = BuildStarterPlayerData(),
                CreatedUtc = NowUtc(),
                LastPlayedUtc = NowUtc(),
            };

            SaveServerCharacter(multiplayerKey, character);

            return character;
        }

        public CharacterSaveData LoadServerCharacter(string multiplayerKey, string characterId)
        {
            return LoadCharacterAt($"{ServerCharactersDirFor(multiplayerKey)}/{characterId}.tres");
        }

        public void SaveServerCharacter(string multiplayerKey, CharacterSaveData character)
        {
            var dir = ServerCharactersDirFor(multiplayerKey);

            EnsureDir(dir);

            ResourceSaver.Save(character, $"{dir}/{character.CharacterId}.tres");
        }

        public void DeleteServerCharacter(string multiplayerKey, string characterId)
        {
            DeleteIfExists($"{ServerCharactersDirFor(multiplayerKey)}/{characterId}.tres");
        }

        #endregion

        #region Core - Backup de personagem "por Peer"

        public void SaveBackup(string ownerProfileId, CharacterSaveData character)
        {
            if (string.IsNullOrEmpty(ownerProfileId) || character == null)
            {
                return;
            }

            var dir = $"{SavesConstants.PEER_BACKUPS_DIR}/{ownerProfileId}";

            EnsureDir(dir);

            ResourceSaver.Save(character, $"{dir}/{character.CharacterId}.tres");
        }

        #endregion

        #region Core - Mundos

        public List<WorldSaveData> ListWorlds()
        {
            var result = new List<WorldSaveData>();

            using var dir = DirAccess.Open(SavesConstants.WORLDS_DIR);

            if (dir == null)
            {
                return result;
            }

            dir.ListDirBegin();

            for (var folderName = dir.GetNext(); folderName != ""; folderName = dir.GetNext())
            {
                if (!dir.CurrentIsDir() || folderName == "." || folderName == "..")
                {
                    continue;
                }

                var metaPath = $"{SavesConstants.WORLDS_DIR}/{folderName}/world.tres";

                if (ResourceLoader.Exists(metaPath))
                {
                    var meta = ResourceLoader.Load<WorldSaveData>(metaPath, cacheMode: ResourceLoader.CacheMode.Ignore);

                    if (meta != null)
                    {
                        MigrateLegacyPortals(meta);

                        result.Add(meta);
                    }
                }
            }

            return result.OrderByDescending(w => w.LastPlayedUtc).ToList();
        }

        // Mundos salvos antes de portal virar prop gravaram a lista em "Portals". Converte pra
        // "Props" com PropId = "portal" na leitura; o proximo save ja grava no formato novo.
        private static void MigrateLegacyPortals(WorldSaveData world)
        {
            if (world?.Portals == null || world.Portals.Count == 0)
            {
                return;
            }

            world.Props ??= new Godot.Collections.Array<PropSaveData>();

            foreach (var legacy in world.Portals)
            {
                if (legacy == null)
                {
                    continue;
                }

                legacy.PropId = string.IsNullOrEmpty(legacy.PropId) ? "portal" : legacy.PropId;

                world.Props.Add(legacy);
            }

            world.Portals.Clear();
        }

        public WorldSaveData CreateWorld(string name, long seed, WorldCharacterMode mode, string multiplayerKey, int autosaveIntervalMinutes, bool isProcedural = true)
        {
            var world = new WorldSaveData
            {
                WorldId = Guid.NewGuid().ToString(),
                Name = name,
                Seed = seed,
                CharacterMode = mode,
                IsProcedural = isProcedural,
                MultiplayerKey = multiplayerKey ?? "",
                AutosaveIntervalMinutes = Mathf.Max(1, autosaveIntervalMinutes),
                CreatedUtc = NowUtc(),
                LastPlayedUtc = NowUtc(),
            };

            SaveWorldMeta(world);

            return world;
        }

        public void SaveWorldMeta(WorldSaveData world)
        {
            var dir = $"{SavesConstants.WORLDS_DIR}/{world.WorldId}";

            EnsureDir(dir);

            ResourceSaver.Save(world, $"{dir}/world.tres");
        }

        public DimensionSaveData LoadDimensionState(string worldId, string dimensionId)
        {
            var path = $"{SavesConstants.WORLDS_DIR}/{worldId}/{dimensionId}.tres";

            return ResourceLoader.Exists(path)
                ? ResourceLoader.Load<DimensionSaveData>(path, cacheMode: ResourceLoader.CacheMode.Ignore)
                : new DimensionSaveData();
        }

        public void SaveDimensionState(string worldId, string dimensionId, DimensionSaveData state)
        {
            var dir = $"{SavesConstants.WORLDS_DIR}/{worldId}";

            EnsureDir(dir);

            ResourceSaver.Save(state, $"{dir}/{dimensionId}.tres");
        }

        public void DeleteWorld(string worldId)
        {
            DeleteDirectoryRecursive($"{SavesConstants.WORLDS_DIR}/{worldId}");
        }

        #endregion

        #region Utils

        public long NowUtc()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private PlayerData BuildStarterPlayerData()
        {
            var data = new PlayerData();

            data.Inventory ??= new InventoryData();

            var portal = ItemFactory.CreateInstance("portal");
            var bow = ItemFactory.CreateInstance("bow_starting2");

            new Inventory().AddItem(data.Inventory, portal);
            new Inventory().AddItem(data.Inventory, bow);

            data.EquippedItemId = bow.InstanceId;

            return data;
        }

        private string ServerCharactersDirFor(string multiplayerKey)
        {
            return $"{SavesConstants.SERVER_CHARACTERS_DIR}/{multiplayerKey}";
        }

        private List<CharacterSaveData> ListCharactersAt(string dirPath)
        {
            var result = new List<CharacterSaveData>();

            using var dir = DirAccess.Open(dirPath);

            if (dir == null)
            {
                return result;
            }

            dir.ListDirBegin();

            for (var fileName = dir.GetNext(); fileName != ""; fileName = dir.GetNext())
            {
                if (dir.CurrentIsDir() || !fileName.EndsWith(".tres"))
                {
                    continue;
                }

                var character = LoadCharacterAt($"{dirPath}/{fileName}");

                if (character != null)
                {
                    result.Add(character);
                }
            }

            return result.OrderByDescending(c => c.LastPlayedUtc).ToList();
        }

        private CharacterSaveData LoadCharacterAt(string path)
        {
            return ResourceLoader.Exists(path)
                ? ResourceLoader.Load<CharacterSaveData>(path, cacheMode: ResourceLoader.CacheMode.Ignore)
                : null;
        }

        private void EnsureDir(string dirPath)
        {
            if (!DirAccess.DirExistsAbsolute(dirPath))
            {
                DirAccess.MakeDirRecursiveAbsolute(dirPath);
            }
        }

        private void DeleteIfExists(string path)
        {
            if (FileAccess.FileExists(path))
            {
                DirAccess.RemoveAbsolute(path);
            }
        }

        private void DeleteDirectoryRecursive(string dirPath)
        {
            using var dir = DirAccess.Open(dirPath);

            if (dir == null)
            {
                return;
            }

            dir.ListDirBegin();

            for (var entryName = dir.GetNext(); entryName != ""; entryName = dir.GetNext())
            {
                if (entryName == "." || entryName == "..")
                {
                    continue;
                }

                var entryPath = $"{dirPath}/{entryName}";

                if (dir.CurrentIsDir())
                {
                    DeleteDirectoryRecursive(entryPath);
                }
                else
                {
                    DirAccess.RemoveAbsolute(entryPath);
                }
            }

            DirAccess.RemoveAbsolute(dirPath);
        }

        #endregion
        #region Core - Politica de autosave

        private Timer _autosaveTimer;

        private bool IsHostOrSolo()
        {
            return Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();
        }

        public void StartAutosave(WorldSaveData save)
        {
            StopAutosave();

            if (save == null || !IsHostOrSolo())
            {
                return;
            }

            _autosaveTimer = new Timer
            {
                WaitTime = Mathf.Max(1, save.AutosaveIntervalMinutes) * 60.0,
                Autostart = true,
            };

            _autosaveTimer.Timeout += SaveCurrentWorld;

            AddChild(_autosaveTimer);
        }

        public void StopAutosave()
        {
            if (_autosaveTimer == null)
            {
                return;
            }

            _autosaveTimer.QueueFree();

            _autosaveTimer = null;
        }

        public void SaveCurrentWorld()
        {
            if (Game.Managers.WorldManager.Node.CurrentWorldSave == null)
            {
                return;
            }

                        var chunkStreamingManager = Game.Managers.ChunkStreamingManager.Node;

            if (chunkStreamingManager != null)
            {
                SaveDimensionState(Game.Managers.WorldManager.Node.CurrentWorldSave.WorldId, ChunkStreamingConstants.OVERWORLD_ID, chunkStreamingManager.ExportState(ChunkStreamingConstants.OVERWORLD_ID));
                SaveDimensionState(Game.Managers.WorldManager.Node.CurrentWorldSave.WorldId, ChunkStreamingConstants.UPSIDEDOWN_ID, chunkStreamingManager.ExportState(ChunkStreamingConstants.UPSIDEDOWN_ID));
            }

            Game.Managers.WorldManager.Node.CurrentWorldSave.Props = Game.Managers.DimensionManager.Node.CollectProps();
            Game.Managers.WorldManager.Node.CurrentWorldSave.LastPlayedUtc = NowUtc();

            SaveWorldMeta(Game.Managers.WorldManager.Node.CurrentWorldSave);

            SaveOwnLocalCharacter();
            SaveRemotePeerCharacters();
        }

        private void SaveOwnLocalCharacter()
        {
            if (Game.Managers.SessionManager.Node.PendingCharacter == null)
            {
                return;
            }

            var localPlayer = Game.Managers.WorldManager.Node.GetLocalPlayer();

            if (localPlayer == null)
            {
                return;
            }

            Game.Managers.SessionManager.Node.PendingCharacter.Data = localPlayer.Data;
            Game.Managers.SessionManager.Node.PendingCharacter.LastPlayedUtc = NowUtc();

            SaveLocalCharacter(Game.Managers.SessionManager.Node.PendingCharacter);
        }

        private void SaveRemotePeerCharacters()
        {
            if (!IsHostOrSolo())
            {
                return;
            }

            foreach (var player in GetTree().GetNodesInGroup("players").OfType<Player>())
            {
                if (player.PeerId <= 1 || !_peerCharacters.TryGetValue(player.PeerId, out var character))
                {
                    continue;
                }

                character.Data = player.Data;
                character.LastPlayedUtc = NowUtc();

                if (Game.Managers.WorldManager.Node.CurrentWorldSave.CharacterMode == WorldCharacterMode.ServerCharacters)
                {
                    SaveServerCharacter(Game.Managers.WorldManager.Node.CurrentWorldSave.MultiplayerKey, character);
                }
                else
                {
                    SaveBackup(character.OwnerProfileId, character);
                }
            }
        }

        public void PersistBeforeLeaving()
        {
            SaveOwnLocalCharacter();

            if (Game.Managers.WorldManager.Node.CurrentWorldSave != null && IsHostOrSolo())
            {
                SaveCurrentWorld();
            }
        }

        #endregion

        #region Personagem da sessao

        public override void _Ready()
        {
            Game.WhenReady(() =>
            {
                Game.Managers.NetworkManager.Node.PeerLeft += OnPeerLeft;
            });
        }

        private void OnPeerLeft(long peerId, Jogo25D.Characters.Player playerNode)
        {
            SavePeerCharacterOnDisconnect(peerId, playerNode);

            ForgetPeer(peerId);
        }

        // Modo de personagem desta sessao. No host/solo vem do save; no cliente chega pela rede
        // no JoinInfoReceive - por isso nao da pra deduzir do CurrentWorldSave em todo mundo.
        public WorldCharacterMode CharacterMode { get; set; } = WorldCharacterMode.LocalCharacters;

        public event System.Action<string, Godot.Collections.Array> ServerCharacterListAvailable;

        private readonly Dictionary<long, CharacterSaveData> _peerCharacters = new();
        private readonly Dictionary<long, string> _pendingProfileByPeer = new();

        public IReadOnlyDictionary<long, CharacterSaveData> PeerCharacters => _peerCharacters;

        public void ForgetPeer(long peerId)
        {
            _peerCharacters.Remove(peerId);
            _pendingProfileByPeer.Remove(peerId);
        }

        public void ForgetAllPeers()
        {
            _peerCharacters.Clear();
            _pendingProfileByPeer.Clear();
        }

        #endregion

        #region Core - Personagem da sessao (local x servidor)

        // API unica pra quem escolhe personagem: a tela pede, o SaveManager resolve. Quem decide
        // se e local ou de servidor e o CharacterMode da sessao, nunca o chamador.
        public void CreateCharacter(string name)
        {
            if (CharacterMode == WorldCharacterMode.ServerCharacters)
            {
                CreateServerCharacterRequest(name);

                return;
            }

            Game.Ui.CharacterSelectUI.Node.CompleteLocalCreation(CreateLocalCharacter(name));
        }

        public void DeleteCharacter(string characterId)
        {
            if (CharacterMode == WorldCharacterMode.ServerCharacters)
            {
                DeleteServerCharacterRequest(characterId);

                return;
            }

            DeleteLocalCharacter(characterId);
        }

        // Personagem escolhido: no mundo proprio entra no jogo, no join manda pro servidor.
        public void SelectCharacter(CharacterSaveData character)
        {
            if (character == null)
            {
                return;
            }

            if (Game.Managers.NetworkManager.Node.IsConnected())
            {
                SubmitLocalCharacterForJoin(character);

                return;
            }

            Game.Managers.SessionManager.Node.PendingCharacter = character;

            Game.Managers.SessionManager.Node.EnterPendingWorld();
        }

        public void SelectCharacter(string serverCharacterId)
        {
            SelectServerCharacterRequest(serverCharacterId);
        }

        // Chamado por quem acabou de conectar: pergunta ao servidor em que modo o mundo esta.
        public void RequestJoinInfo()
        {
            RpcId(1, nameof(RequestJoinInfoServerReceive));
        }


        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void RequestJoinInfoServerReceive()
        {
            if (!Multiplayer.IsServer() || Game.Managers.WorldManager.Node.CurrentWorldSave == null)
            {
                return;
            }

            var senderId = Multiplayer.GetRemoteSenderId();

            RpcId(senderId, nameof(JoinInfoReceive), (int)Game.Managers.WorldManager.Node.CurrentWorldSave.CharacterMode);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void JoinInfoReceive(int modeInt)
        {
            var mode = (WorldCharacterMode)modeInt;

            CharacterMode = mode;

            if (mode == WorldCharacterMode.LocalCharacters)
            {
                Game.Ui.CharacterSelectUI.Node.CurrentContext = Jogo25D.UI.CharacterSelectContext.PeerJoinLocal;

                Game.Managers.RouterManager.Node.Open(Game.Ui.CharacterSelectUI.Node);

                return;
            }

            var profile = GetOrCreateLocalProfile();

            RpcId(1, nameof(RequestServerCharacterListServerReceive), profile?.ProfileId ?? "");
        }

        public void SubmitLocalCharacterForJoin(CharacterSaveData character)
        {
            if (character == null)
            {
                return;
            }

            Game.Managers.SessionManager.Node.PendingCharacter = character;

            var profile = GetOrCreateLocalProfile();

            RpcId(1, nameof(SubmitLocalCharacterServerReceive), profile?.ProfileId ?? "", Jogo25D.Utils.GodotDictionaryParser.GodotDictionaryParser.ToDictionary(character));
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SubmitLocalCharacterServerReceive(string profileId, Godot.Collections.Dictionary characterDict)
        {
            if (!Multiplayer.IsServer() || Game.Managers.WorldManager.Node.CurrentWorldSave == null || Game.Managers.WorldManager.Node.CurrentWorldSave.CharacterMode != WorldCharacterMode.LocalCharacters)
            {
                return;
            }

            var senderId = Multiplayer.GetRemoteSenderId();
            var character = Jogo25D.Utils.GodotDictionaryParser.GodotDictionaryParser.ToResource<CharacterSaveData>(characterDict);

            if (character == null)
            {
                character = CreateLocalCharacter("Sem nome");
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

            Game.Managers.NetworkManager.Node.FinishPeerJoin(senderId, character);
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void RequestServerCharacterListServerReceive(string profileId)
        {
            if (!Multiplayer.IsServer() || Game.Managers.WorldManager.Node.CurrentWorldSave == null || Game.Managers.WorldManager.Node.CurrentWorldSave.CharacterMode != WorldCharacterMode.ServerCharacters)
            {
                return;
            }

            var senderId = Multiplayer.GetRemoteSenderId();

            _pendingProfileByPeer[senderId] = profileId;

            SendServerCharacterListTo(senderId);
        }

        private void SendServerCharacterListTo(long senderId)
        {
            if (!_pendingProfileByPeer.TryGetValue(senderId, out var profileId))
            {
                return;
            }

            var saveManager = Game.Managers.SaveManager.Node;
            var characters = ListServerCharacters(Game.Managers.WorldManager.Node.CurrentWorldSave.MultiplayerKey)
                .Where(c => c.OwnerProfileId == profileId)
                .ToList() ?? new List<CharacterSaveData>();

            var summaries = new Godot.Collections.Array();

            foreach (var character in characters)
            {
                summaries.Add(new Godot.Collections.Dictionary
                {
                    ["CharacterId"] = character.CharacterId,
                    ["Name"] = character.Name,
                });
            }

            RpcId(senderId, nameof(ServerCharacterListReceive), summaries);
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void DeleteServerCharacterServerReceive(string characterId)
        {
            if (!Multiplayer.IsServer() || Game.Managers.WorldManager.Node.CurrentWorldSave == null || Game.Managers.WorldManager.Node.CurrentWorldSave.CharacterMode != WorldCharacterMode.ServerCharacters)
            {
                return;
            }

            var senderId = Multiplayer.GetRemoteSenderId();

            DeleteServerCharacter(Game.Managers.WorldManager.Node.CurrentWorldSave.MultiplayerKey, characterId);

            SendServerCharacterListTo(senderId);
        }

        public void DeleteServerCharacterRequest(string characterId)
        {
            RpcId(1, nameof(DeleteServerCharacterServerReceive), characterId);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ServerCharacterListReceive(Godot.Collections.Array summaries)
        {
            ServerCharacterListAvailable?.Invoke(Game.Managers.WorldManager.Node.CurrentWorldSave?.MultiplayerKey ?? "", summaries);
        }

        public void SelectServerCharacterRequest(string characterId)
        {
            RpcId(1, nameof(SelectServerCharacterServerReceive), characterId);
        }

        public void CreateServerCharacterRequest(string name)
        {
            RpcId(1, nameof(CreateServerCharacterServerReceive), name);
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SelectServerCharacterServerReceive(string characterId)
        {
            if (!Multiplayer.IsServer() || Game.Managers.WorldManager.Node.CurrentWorldSave == null || Game.Managers.WorldManager.Node.CurrentWorldSave.CharacterMode != WorldCharacterMode.ServerCharacters)
            {
                return;
            }

            var senderId = Multiplayer.GetRemoteSenderId();
            var character = LoadServerCharacter(Game.Managers.WorldManager.Node.CurrentWorldSave.MultiplayerKey, characterId);

            if (character == null)
            {
                return;
            }

            _peerCharacters[senderId] = character;

            Game.Managers.NetworkManager.Node.FinishPeerJoin(senderId, character);
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void CreateServerCharacterServerReceive(string name)
        {
            if (!Multiplayer.IsServer() || Game.Managers.WorldManager.Node.CurrentWorldSave == null || Game.Managers.WorldManager.Node.CurrentWorldSave.CharacterMode != WorldCharacterMode.ServerCharacters)
            {
                return;
            }

            var senderId = Multiplayer.GetRemoteSenderId();
            var saveManager = Game.Managers.SaveManager.Node;
            var ownerProfileId = _pendingProfileByPeer.TryGetValue(senderId, out var profileId) ? profileId : "";
            var character = CreateServerCharacter(Game.Managers.WorldManager.Node.CurrentWorldSave.MultiplayerKey, name, ownerProfileId);

            if (character == null)
            {
                return;
            }

            _peerCharacters[senderId] = character;

            Game.Managers.NetworkManager.Node.FinishPeerJoin(senderId, character);
        }

        #endregion

        private void SavePeerCharacterOnDisconnect(long id, Player playerNode)
        {
            if (playerNode == null || Game.Managers.WorldManager.Node.CurrentWorldSave == null || !_peerCharacters.TryGetValue(id, out var character))
            {
                return;
            }

            var saveManager = Game.Managers.SaveManager.Node;

            if (saveManager == null)
            {
                return;
            }

            character.Data = playerNode.Data;
            character.LastPlayedUtc = NowUtc();

            if (Game.Managers.WorldManager.Node.CurrentWorldSave.CharacterMode == WorldCharacterMode.ServerCharacters)
            {
                SaveServerCharacter(Game.Managers.WorldManager.Node.CurrentWorldSave.MultiplayerKey, character);
            }
            else
            {
                SaveBackup(character.OwnerProfileId, character);
            }
        }


    }
}
