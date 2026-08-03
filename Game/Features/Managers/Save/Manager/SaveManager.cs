using Godot;
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
        public ProfileData CachedProfile { get; set; }

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

        public CharacterSaveData LoadLocalCharacter(string characterId)
        {
            return LoadCharacterAt($"{SavesConstants.CHARACTERS_DIR}/{characterId}.tres");
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
                        result.Add(meta);
                    }
                }
            }

            return result.OrderByDescending(w => w.LastPlayedUtc).ToList();
        }

        public WorldSaveData CreateWorld(string name, long seed, WorldCharacterMode mode, string multiplayerKey, int autosaveIntervalMinutes)
        {
            var world = new WorldSaveData
            {
                WorldId = Guid.NewGuid().ToString(),
                Name = name,
                Seed = seed,
                CharacterMode = mode,
                MultiplayerKey = multiplayerKey ?? "",
                AutosaveIntervalMinutes = Mathf.Max(1, autosaveIntervalMinutes),
                CreatedUtc = NowUtc(),
                LastPlayedUtc = NowUtc(),
            };

            SaveWorldMeta(world);

            return world;
        }

        public WorldSaveData LoadWorldMeta(string worldId)
        {
            var path = $"{SavesConstants.WORLDS_DIR}/{worldId}/world.tres";

            return ResourceLoader.Exists(path)
                ? ResourceLoader.Load<WorldSaveData>(path, cacheMode: ResourceLoader.CacheMode.Ignore)
                : null;
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

        public static long NowUtc()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private static PlayerData BuildStarterPlayerData()
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

        private static string ServerCharactersDirFor(string multiplayerKey)
        {
            return $"{SavesConstants.SERVER_CHARACTERS_DIR}/{multiplayerKey}";
        }

        private static List<CharacterSaveData> ListCharactersAt(string dirPath)
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

        private static CharacterSaveData LoadCharacterAt(string path)
        {
            return ResourceLoader.Exists(path)
                ? ResourceLoader.Load<CharacterSaveData>(path, cacheMode: ResourceLoader.CacheMode.Ignore)
                : null;
        }

        private static void EnsureDir(string dirPath)
        {
            if (!DirAccess.DirExistsAbsolute(dirPath))
            {
                DirAccess.MakeDirRecursiveAbsolute(dirPath);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (FileAccess.FileExists(path))
            {
                DirAccess.RemoveAbsolute(path);
            }
        }

        private static void DeleteDirectoryRecursive(string dirPath)
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
    }
}
