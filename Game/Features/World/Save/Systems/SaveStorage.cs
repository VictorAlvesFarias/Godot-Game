using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Items;
using Jogo25D.Save;
using Jogo25D.Utils.GodotDictionaryParser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Systems
{
    // Armazenamento: le e escreve JSON, monta e apaga pasta. Nao conhece
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

            if (FileAccess.FileExists(SavesConstants.PROFILE_PATH))
            {
                CachedProfile = ReadJson<ProfileData>(SavesConstants.PROFILE_PATH);
            }

            if (CachedProfile == null)
            {
                CachedProfile = new ProfileData { ProfileId = Guid.NewGuid().ToString() };

                WriteJson(CachedProfile, SavesConstants.PROFILE_PATH);
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
                State = BuildStarterState(),
                CreatedUtc = NowUtc(),
                LastPlayedUtc = NowUtc(),
            };

            SaveLocalCharacter(character);

            return character;
        }

        public static void SaveLocalCharacter(CharacterSaveData character)
        {
            EnsureDir(SavesConstants.CHARACTERS_DIR);

            WriteJson(character, $"{SavesConstants.CHARACTERS_DIR}/{character.CharacterId}.json");
        }

        public static void DeleteLocalCharacter(string characterId)
        {
            DeleteIfExists($"{SavesConstants.CHARACTERS_DIR}/{characterId}.json");
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
                State = BuildStarterState(),
                CreatedUtc = NowUtc(),
                LastPlayedUtc = NowUtc(),
            };

            SaveServerCharacter(multiplayerKey, character);

            return character;
        }

        public static CharacterSaveData LoadServerCharacter(string multiplayerKey, string characterId)
        {
            return LoadCharacterAt($"{ServerCharactersDirFor(multiplayerKey)}/{characterId}.json");
        }

        public static void SaveServerCharacter(string multiplayerKey, CharacterSaveData character)
        {
            var dir = ServerCharactersDirFor(multiplayerKey);

            EnsureDir(dir);

            WriteJson(character, $"{dir}/{character.CharacterId}.json");
        }

        public static void DeleteServerCharacter(string multiplayerKey, string characterId)
        {
            DeleteIfExists($"{ServerCharactersDirFor(multiplayerKey)}/{characterId}.json");
        }

        public static void SaveBackup(string ownerProfileId, CharacterSaveData character)
        {
            if (string.IsNullOrEmpty(ownerProfileId) || character == null)
            {
                return;
            }

            var dir = $"{SavesConstants.PEER_BACKUPS_DIR}/{ownerProfileId}";

            EnsureDir(dir);

            WriteJson(character, $"{dir}/{character.CharacterId}.json");
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

                var metaPath = $"{SavesConstants.WORLDS_DIR}/{folderName}/world.json";

                if (!FileAccess.FileExists(metaPath))
                {
                    continue;
                }

                var documento = LoadWorldDocument(folderName);
                var meta = documento == null ? null : WorldDocument.MetaDe<WorldSaveData>(documento);

                if (meta != null)
                {
                    result.Add(meta);
                }
            }

            return result.OrderByDescending(w => w.LastPlayedUtc).ToList();
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
            SaveWorldDocument(world.WorldId, new Godot.Collections.Dictionary
            {
                { WorldDocument.TYPE, "world" },
                { WorldDocument.STATE, WorldDocument.EstadoDe(world) },
                { WorldDocument.DIMENSIONS, new Godot.Collections.Array() },
            });
        }

        public static Godot.Collections.Dictionary LoadWorldDocument(string worldId)
        {
            var caminho = $"{SavesConstants.WORLDS_DIR}/{worldId}/world.json";

            if (!FileAccess.FileExists(caminho))
            {
                return null;
            }

            var arquivo = FileAccess.Open(caminho, FileAccess.ModeFlags.Read);

            if (arquivo == null)
            {
                return null;
            }

            var texto = arquivo.GetAsText();

            arquivo.Close();

            var lido = Json.ParseString(texto);

            return lido.VariantType == Variant.Type.Dictionary ? lido.AsGodotDictionary() : null;
        }

        public static void SaveWorldDocument(string worldId, Godot.Collections.Dictionary documento)
        {
            var pasta = $"{SavesConstants.WORLDS_DIR}/{worldId}";

            EnsureDir(pasta);

            var arquivo = FileAccess.Open($"{pasta}/world.json", FileAccess.ModeFlags.Write);

            if (arquivo == null)
            {
                GD.PushError($"[SaveStorage] nao consegui escrever o mundo {worldId}");

                return;
            }

            arquivo.StoreString(Json.Stringify(documento, "\t"));

            arquivo.Close();
        }

        public static void DeleteWorld(string worldId)
        {
            DeleteDirectoryRecursive($"{SavesConstants.WORLDS_DIR}/{worldId}");
        }

        public static long NowUtc()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        // Estado inicial de um personagem novo. Instancia um Player so pra tirar o retrato dos
        // valores padrao e libera em seguida - assim o schema vive num lugar so (a classe), em
        // vez de ser repetido a mao aqui.
        //
        // Node nao e RefCounted: o Free() e obrigatorio, por isso o try/finally.
        private static Godot.Collections.Dictionary BuildStarterState()
        {
            var player = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn").Instantiate<Player>();

            try
            {
                player.GiveItem(ItemFactory.CreateInstance("portal"));

                return GodotDictionaryParser.ToDictionary(player);
            }
            finally
            {
                player.Free();
            }
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
                if (dir.CurrentIsDir() || !fileName.EndsWith(".json"))
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
            return FileAccess.FileExists(path)
                ? ReadJson<CharacterSaveData>(path)
                : null;
        }

        #region Core - Json

        // O save e JSON puro: mesmo formato que ja trafega por RPC, via GodotDictionaryParser.
        // O tipo concreto vem no campo "$type" do proprio arquivo, entao nao existe factory.
        private static void WriteJson(Resource resource, string path)
        {
            var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);

            if (file == null)
            {
                GD.PushError($"[SaveStorage] nao consegui escrever {path}: {FileAccess.GetOpenError()}");

                return;
            }

            file.StoreString(Json.Stringify(GodotDictionaryParser.ToDictionary(resource), "\t"));

            file.Close();
        }

        private static T ReadJson<T>(string path) where T : Resource
        {
            if (!FileAccess.FileExists(path))
            {
                return null;
            }

            var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);

            if (file == null)
            {
                GD.PushError($"[SaveStorage] nao consegui ler {path}: {FileAccess.GetOpenError()}");

                return null;
            }

            var texto = file.GetAsText();

            file.Close();

            var parsed = Json.ParseString(texto);

            if (parsed.VariantType != Variant.Type.Dictionary)
            {
                GD.PushError($"[SaveStorage] {path} nao e um objeto JSON valido");

                return null;
            }

            return GodotDictionaryParser.ToResource<T>(parsed.AsGodotDictionary());
        }

        #endregion

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
