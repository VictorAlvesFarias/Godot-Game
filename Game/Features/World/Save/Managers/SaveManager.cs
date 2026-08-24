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
        #region Core - Politica de autosave


        #endregion

        #region Personagem da sessao


        #endregion

        #region Core - Personagem da sessao (local x servidor)


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

            var chunkStreamingManager = Game.Managers.ChunkStreamingManager.Node;

            if (chunkStreamingManager != null)
            {
                SaveStorage.SaveDimensionState(save.WorldId, ChunkStreamingConstants.OVERWORLD_ID, chunkStreamingManager.ExportState(ChunkStreamingConstants.OVERWORLD_ID));
                SaveStorage.SaveDimensionState(save.WorldId, ChunkStreamingConstants.UPSIDEDOWN_ID, chunkStreamingManager.ExportState(ChunkStreamingConstants.UPSIDEDOWN_ID));
            }

            save.Props = Game.Managers.DimensionManager.Node.CollectProps();
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
