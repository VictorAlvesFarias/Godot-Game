using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Items;
using Jogo25D.Save;
using Jogo25D.Save.Resources;
using Jogo25D.Save.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Systems
{
    public static class SaveContext
    {
        #region Core - Registro e politica

        private static readonly List<Resource> _registry = new();

        private static double _autosaveIntervalSeconds;

        public static event System.Action Saving;

        public static void Register(Resource data)
        {
            if (data == null || _registry.Contains(data))
            {
                return;
            }

            _registry.Add(data);
        }

        public static void Unregister(Resource data)
        {
            if (data != null)
            {
                _registry.Remove(data);
            }
        }

        public static void ClearRegistry()
        {
            _registry.Clear();
        }

        public static void StartAutosave(int intervalMinutes)
        {
            StopAutosave();

            if (!IsHostOrSolo())
            {
                return;
            }

            _autosaveIntervalSeconds = Mathf.Max(1, intervalMinutes) * 60.0;

            ArmAutosave();
        }

        public static void StopAutosave()
        {
            _autosaveIntervalSeconds = 0;
        }

        private static void ArmAutosave()
        {
            if (_autosaveIntervalSeconds <= 0 || GameLoop.Tree == null)
            {
                return;
            }

            var timer = GameLoop.Tree.CreateTimer(_autosaveIntervalSeconds);

            timer.Timeout += OnAutosaveTimeout;
        }

        private static void OnAutosaveTimeout()
        {
            if (_autosaveIntervalSeconds <= 0)
            {
                return;
            }

            SaveAll();
            ArmAutosave();
        }

        public static void SaveAll()
        {
            Saving?.Invoke();

            var world = _registry.OfType<WorldSaveData>().FirstOrDefault();
            var host = IsHostOrSolo();

            if (world != null && host)
            {
                SaveWorld(world);
            }

            foreach (var character in _registry.OfType<CharacterSaveData>())
            {
                if (character.OwnerProfileId == SaveStorage.GetOrCreateLocalProfile()?.ProfileId)
                {
                    SaveLocalCharacter(character);

                    continue;
                }

                if (host && world != null)
                {
                    SavePeerCharacter(character, world.CharacterMode, world.MultiplayerKey);
                }
            }
        }

        private static bool IsHostOrSolo()
        {
            var multiplayer = GameLoop.Multiplayer;

            return multiplayer == null || !multiplayer.HasMultiplayerPeer() || multiplayer.IsServer();
        }

        #endregion

        #region Core - Persistencia

        public static void SaveWorld(WorldSaveData save)
        {
            if (save == null)
            {
                return;
            }

            save.LastPlayedUtc = SaveStorage.NowUtc();

            WorldDocument.Save(save);
        }

        public static void SaveLocalCharacter(CharacterSaveData character)
        {
            if (character == null)
            {
                return;
            }

            character.LastPlayedUtc = SaveStorage.NowUtc();

            SaveStorage.SaveLocalCharacter(character);
        }

        public static void SavePeerCharacter(CharacterSaveData character, WorldCharacterMode mode, string multiplayerKey)
        {
            if (character == null)
            {
                return;
            }

            character.LastPlayedUtc = SaveStorage.NowUtc();

            if (mode == WorldCharacterMode.ServerCharacters)
            {
                SaveStorage.SaveServerCharacter(multiplayerKey, character);

                return;
            }

            SaveStorage.SaveBackup(character.OwnerProfileId, character);
        }

        #endregion
    }
}
