using Godot;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Features.World.Characters.Resources;
using System;
using System.Linq;

namespace Jogo25D.Systems
{
    // Persistencia em disco do sistema de save (ver .docs/sistema-de-save.md e
    // .docs/spec-sistema-de-save.md). Tudo em Resource/.tres via
    // ResourceSaver/ResourceLoader - os tipos ja sao [Export] Resource,
    // entao nao existe camada de serializacao propria aqui.
    public partial class SaveManager : Node
    {
        public static string DEFAULT_NODE_PATH = "/root/Main/Managers/SaveManager";

        private const string ProfilePath = "user://profile.tres";
        private const string CharactersDir = "user://saves/characters";
        private const string ServerCharactersDir = "user://saves/server_characters";
        private const string PeerBackupsDir = "user://saves/peer_backups";
        private const string WorldsDir = "user://saves/worlds";

        private ProfileData _cachedProfile;

        #region Core - Perfil

        public ProfileData GetOrCreateLocalProfile()
        {
            if (_cachedProfile != null)
            {
                return _cachedProfile;
            }

            if (ResourceLoader.Exists(ProfilePath))
            {
                _cachedProfile = ResourceLoader.Load<ProfileData>(ProfilePath, cacheMode: ResourceLoader.CacheMode.Ignore);
            }

            if (_cachedProfile == null)
            {
                _cachedProfile = new ProfileData { ProfileId = Guid.NewGuid().ToString() };

                ResourceSaver.Save(_cachedProfile, ProfilePath);
            }

            return _cachedProfile;
        }

        #endregion

        #region Core - Personagens locais

        public System.Collections.Generic.List<CharacterSaveData> ListLocalCharacters()
        {
            return ListCharactersAt(CharactersDir);
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
            return LoadCharacterAt($"{CharactersDir}/{characterId}.tres");
        }

        public void SaveLocalCharacter(CharacterSaveData character)
        {
            EnsureDir(CharactersDir);

            ResourceSaver.Save(character, $"{CharactersDir}/{character.CharacterId}.tres");
        }

        public void DeleteLocalCharacter(string characterId)
        {
            DeleteIfExists($"{CharactersDir}/{characterId}.tres");
        }

        #endregion

        #region Core - Personagens de servidor

        public System.Collections.Generic.List<CharacterSaveData> ListServerCharacters(string multiplayerKey)
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

        // Nunca e a fonte de verdade - so uma copia de seguranca guardada
        // pelo host, indexada pelo ProfileId do dono do personagem local.
        public void SaveBackup(string ownerProfileId, CharacterSaveData character)
        {
            if (string.IsNullOrEmpty(ownerProfileId) || character == null)
            {
                return;
            }

            var dir = $"{PeerBackupsDir}/{ownerProfileId}";

            EnsureDir(dir);

            ResourceSaver.Save(character, $"{dir}/{character.CharacterId}.tres");
        }

        #endregion

        #region Core - Mundos

        public System.Collections.Generic.List<WorldSaveData> ListWorlds()
        {
            var result = new System.Collections.Generic.List<WorldSaveData>();

            using var dir = DirAccess.Open(WorldsDir);

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

                var metaPath = $"{WorldsDir}/{folderName}/world.tres";

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
            var path = $"{WorldsDir}/{worldId}/world.tres";

            return ResourceLoader.Exists(path)
                ? ResourceLoader.Load<WorldSaveData>(path, cacheMode: ResourceLoader.CacheMode.Ignore)
                : null;
        }

        public void SaveWorldMeta(WorldSaveData world)
        {
            var dir = $"{WorldsDir}/{world.WorldId}";

            EnsureDir(dir);

            ResourceSaver.Save(world, $"{dir}/world.tres");
        }

        public DimensionSaveData LoadDimensionState(string worldId, string dimensionId)
        {
            var path = $"{WorldsDir}/{worldId}/{dimensionId}.tres";

            return ResourceLoader.Exists(path)
                ? ResourceLoader.Load<DimensionSaveData>(path, cacheMode: ResourceLoader.CacheMode.Ignore)
                : new DimensionSaveData();
        }

        public void SaveDimensionState(string worldId, string dimensionId, DimensionSaveData state)
        {
            var dir = $"{WorldsDir}/{worldId}";

            EnsureDir(dir);

            ResourceSaver.Save(state, $"{dir}/{dimensionId}.tres");
        }

        public void DeleteWorld(string worldId)
        {
            DeleteDirectoryRecursive($"{WorldsDir}/{worldId}");
        }

        #endregion

        #region Utils

        public static long NowUtc()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        // Personagem novo ganha o mesmo "kit" inicial que o WorldManager
        // hoje da na mao (portal + arco inicial) - assim o fluxo autoritativo
        // de entrada so precisa marcar Player.Loaded = true e nunca precisa
        // saber se o personagem "acabou de nascer" ou ja tinha progresso.
        private static PlayerData BuildStarterPlayerData()
        {
            var data = new PlayerData();

            data.Inventory ??= new Jogo25D.Features.World.Items.Resources.InventoryData();

            var portal = Jogo25D.Items.ItemFactory.CreateInstance("portal");
            var bow = Jogo25D.Items.ItemFactory.CreateInstance("bow_starting2");

            new Inventory().AddItem(data.Inventory, portal);
            new Inventory().AddItem(data.Inventory, bow);

            data.EquippedItemId = bow.InstanceId;

            return data;
        }

        private static string ServerCharactersDirFor(string multiplayerKey)
        {
            return $"{ServerCharactersDir}/{multiplayerKey}";
        }

        private static System.Collections.Generic.List<CharacterSaveData> ListCharactersAt(string dirPath)
        {
            var result = new System.Collections.Generic.List<CharacterSaveData>();

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

        // DirAccess nao tem um "remover recursivo" pronto - remove todo
        // arquivo/subpasta antes de remover a pasta em si (world.tres,
        // overworld.tres, upsidedown.tres, ficam todos direto dentro de
        // saves/worlds/{WorldId}/, sem subpastas, mas isso cobre o caso
        // geral mesmo assim).
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
