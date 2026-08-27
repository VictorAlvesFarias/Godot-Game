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
        #region Core - Registro e politica

        // O que esta em jogo e precisa ser gravado. Guarda o Resource: quem registra sabe o que
        // e seu, e o SaveManager nao pergunta nada a ninguem.
        private readonly List<Resource> _registry = new();

        private Timer _autosaveTimer;

        // Emitido antes de serializar, pra quem tem estado fora do Resource se atualizar.
        public event System.Action Saving;

        public void Register(Resource data)
        {
            if (data == null || _registry.Contains(data))
            {
                return;
            }

            _registry.Add(data);
        }

        public void Unregister(Resource data)
        {
            if (data != null)
            {
                _registry.Remove(data);
            }
        }

        public void ClearRegistry()
        {
            _registry.Clear();
        }

        public void StartAutosave(int intervalMinutes)
        {
            StopAutosave();

            if (!IsHostOrSolo())
            {
                return;
            }

            _autosaveTimer = new Timer
            {
                WaitTime = Mathf.Max(1, intervalMinutes) * 60.0,
                Autostart = true,
            };

            _autosaveTimer.Timeout += SaveAll;

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

        // Grava tudo que esta registrado, decidindo pelo tipo. O mundo so e gravado pelo host;
        // personagem local vale pra todo mundo.
        public void SaveAll()
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

            if (!host)
            {
                return;
            }

            // Estado de dimensao nao fica no registry: ele e formato de arquivo, montado na
            // hora a partir do que esta vivo. Quem monta e o WorldManager, que conhece as
            // dimensoes e os dois streamings.
            if (world != null)
            {
                Game.Managers.WorldManager.Node?.SaveDimensions(world.WorldId);
            }
        }

        private bool IsHostOrSolo()
        {
            return Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();
        }

        #endregion

        #region Core - Consulta (fachada do SaveStorage)

        // A UI nao fala com o SaveStorage direto: passa por aqui, porque e aqui que se decide se o
        // dado vem do disco ou de um RPC.
        public List<WorldSaveData> ListWorlds()
        {
            return SaveStorage.ListWorlds();
        }

        public WorldSaveData CreateWorld(string name, long seed, WorldCharacterMode mode, string multiplayerKey, int autosaveIntervalMinutes, bool isProcedural = true)
        {
            return SaveStorage.CreateWorld(name, seed, mode, multiplayerKey, autosaveIntervalMinutes, isProcedural);
        }

        public void DeleteWorld(string worldId)
        {
            SaveStorage.DeleteWorld(worldId);
        }

        public DimensionSaveData LoadDimensionState(string worldId, string dimensionId)
        {
            return SaveStorage.LoadDimensionState(worldId, dimensionId);
        }

        public List<CharacterSaveData> ListLocalCharacters()
        {
            return SaveStorage.ListLocalCharacters();
        }

        public void DeleteLocalCharacter(string characterId)
        {
            SaveStorage.DeleteLocalCharacter(characterId);
        }

        public ProfileData GetOrCreateLocalProfile()
        {
            return SaveStorage.GetOrCreateLocalProfile();
        }

        #endregion

        #region Core - Persistencia

        // Recebe o que deve ser gravado. Nao procura estado em ninguem: quem sabe o que esta em
        // jogo e a sessao, e ela passa por parametro.
        public void SaveWorld(WorldSaveData save)
        {
            if (save == null)
            {
                return;
            }

            save.LastPlayedUtc = SaveStorage.NowUtc();

            SaveStorage.SaveWorldMeta(save);
        }

        public void SaveLocalCharacter(CharacterSaveData character)
        {
            if (character == null)
            {
                return;
            }

            character.LastPlayedUtc = SaveStorage.NowUtc();

            SaveStorage.SaveLocalCharacter(character);
        }

        // Personagem de peer: no modo de servidor grava na pasta do mundo, senao vira backup do
        // perfil dono. A sessao diz o modo; aqui so se escreve.
        public void SavePeerCharacter(CharacterSaveData character, WorldCharacterMode mode, string multiplayerKey)
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

        public long NowUtc()
        {
            return SaveStorage.NowUtc();
        }

        public List<CharacterSaveData> ListServerCharacters(string multiplayerKey)
        {
            return SaveStorage.ListServerCharacters(multiplayerKey);
        }

        public CharacterSaveData CreateServerCharacter(string multiplayerKey, string name, string ownerProfileId)
        {
            return SaveStorage.CreateServerCharacter(multiplayerKey, name, ownerProfileId);
        }

        public CharacterSaveData LoadServerCharacter(string multiplayerKey, string characterId)
        {
            return SaveStorage.LoadServerCharacter(multiplayerKey, characterId);
        }

        public void DeleteServerCharacter(string multiplayerKey, string characterId)
        {
            SaveStorage.DeleteServerCharacter(multiplayerKey, characterId);
        }

        public CharacterSaveData CreateLocalCharacter(string name)
        {
            return SaveStorage.CreateLocalCharacter(name);
        }

        public void SaveServerCharacter(string multiplayerKey, CharacterSaveData character)
        {
            SaveStorage.SaveServerCharacter(multiplayerKey, character);
        }

        public void SaveBackup(string profileId, CharacterSaveData character)
        {
            SaveStorage.SaveBackup(profileId, character);
        }

        #endregion

    }
}
