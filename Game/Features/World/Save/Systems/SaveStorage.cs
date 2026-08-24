using Godot;
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
    // Armazenamento: le e escreve .tres, monta e apaga pasta, migra formato antigo. Nao conhece
    // no, rede nem sessao - por isso e system, nao manager.
    //
    // Nao deve ser chamado pela UI. Quem decide se o dado vem do disco ou de um RPC e o
    // SaveManager; aqui e so o lado do disco.
    public static class SaveStorage
    {
        #region Dinamic properties

        public static ProfileData CachedProfile { get; set; }

        #endregion

        public static ProfileData GetOrCreateLocalProfile()
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

        public static List<CharacterSaveData> ListLocalCharacters()
        {
            return ListCharactersAt(SavesConstants.CHARACTERS_DIR);
        }

        public static CharacterSaveData CreateLocalCharacter(string name)
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

        public static void SaveLocalCharacter(CharacterSaveData character)
        {
            EnsureDir(SavesConstants.CHARACTERS_DIR);

            ResourceSaver.Save(character, $"{SavesConstants.CHARACTERS_DIR}/{character.CharacterId}.tres");
        }

        public static void DeleteLocalCharacter(string characterId)
        {
            DeleteIfExists($"{SavesConstants.CHARACTERS_DIR}/{characterId}.tres");
        }

        public static List<CharacterSaveData> ListServerCharacters(string multiplayerKey)
        {
            return ListCharactersAt(ServerCharactersDirFor(multiplayerKey));
        }

        public static CharacterSaveData CreateServerCharacter(string multiplayerKey, string name, string ownerProfileId)
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

        public static CharacterSaveData LoadServerCharacter(string multiplayerKey, string characterId)
        {
            return LoadCharacterAt($"{ServerCharactersDirFor(multiplayerKey)}/{characterId}.tres");
        }

        public static void SaveServerCharacter(string multiplayerKey, CharacterSaveData character)
        {
            var dir = ServerCharactersDirFor(multiplayerKey);

            EnsureDir(dir);

            ResourceSaver.Save(character, $"{dir}/{character.CharacterId}.tres");
        }

        public static void DeleteServerCharacter(string multiplayerKey, string characterId)
        {
            DeleteIfExists($"{ServerCharactersDirFor(multiplayerKey)}/{characterId}.tres");
        }

        public static void SaveBackup(string ownerProfileId, CharacterSaveData character)
        {
            if (string.IsNullOrEmpty(ownerProfileId) || character == null)
            {
                return;
            }

            var dir = $"{SavesConstants.PEER_BACKUPS_DIR}/{ownerProfileId}";

            EnsureDir(dir);

            ResourceSaver.Save(character, $"{dir}/{character.CharacterId}.tres");
        }

        public static List<WorldSaveData> ListWorlds()
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

        public static WorldSaveData CreateWorld(string name, long seed, WorldCharacterMode mode, string multiplayerKey, int autosaveIntervalMinutes, bool isProcedural = true)
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

        public static void SaveWorldMeta(WorldSaveData world)
        {
            var dir = $"{SavesConstants.WORLDS_DIR}/{world.WorldId}";

            EnsureDir(dir);

            ResourceSaver.Save(world, $"{dir}/world.tres");
        }

        public static DimensionSaveData LoadDimensionState(string worldId, string dimensionId)
        {
            var path = $"{SavesConstants.WORLDS_DIR}/{worldId}/{dimensionId}.tres";

            return ResourceLoader.Exists(path)
                ? ResourceLoader.Load<DimensionSaveData>(path, cacheMode: ResourceLoader.CacheMode.Ignore)
                : new DimensionSaveData();
        }

        public static void SaveDimensionState(string worldId, string dimensionId, DimensionSaveData state)
        {
            var dir = $"{SavesConstants.WORLDS_DIR}/{worldId}";

            EnsureDir(dir);

            ResourceSaver.Save(state, $"{dir}/{dimensionId}.tres");
        }

        public static void DeleteWorld(string worldId)
        {
            DeleteDirectoryRecursive($"{SavesConstants.WORLDS_DIR}/{worldId}");
        }

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
    }
}
