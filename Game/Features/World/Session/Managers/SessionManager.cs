using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Features.World.Characters.Resources;
using Jogo25D.Items;
using Jogo25D.Utils.GodotDictionaryParser;
using System.Collections.Generic;
using System.Linq;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Systems;

namespace Jogo25D.Session
{
    // Estado entre telas: o que foi escolhido no WorldSelect/CharacterSelect e ainda nao entrou em
    // jogo, e o caminho de volta pro menu. E o unico manager com que a UI conversa pra entrar num
    // mundo; quem executa a entrada e o WorldManager.
    public partial class SessionManager : Node
    {
        #region Dinamic properties

        private WorldSaveData _pendingWorld;

        // Escolher o mundo define o modo de personagem da sessao - a tela de personagem abre
        // depois disso e ja encontra o SaveManager sabendo se e local ou de servidor.
        public WorldSaveData PendingWorld
        {
            get => _pendingWorld;
            set
            {
                _pendingWorld = value;

                CharacterMode = value?.CharacterMode ?? WorldCharacterMode.LocalCharacters;
            }
        }
        public CharacterSaveData PendingCharacter { get; set; }

        // O save do mundo em jogo. A cena e do WorldManager; o dado e daqui.
        public WorldSaveData CurrentWorldSave { get; set; }

        // Modo de personagem da sessao: no host/solo vem do save do mundo escolhido, no cliente
        // chega pela rede no JoinInfoReceive - quem acabou de conectar nao tem o save do mundo.
        public WorldCharacterMode CharacterMode { get; set; } = WorldCharacterMode.LocalCharacters;

        public event System.Action<string, Godot.Collections.Array> ServerCharacterListAvailable;

        private readonly Dictionary<long, CharacterSaveData> _peerCharacters = new();
        private readonly Dictionary<long, string> _pendingProfileByPeer = new();

        public IReadOnlyDictionary<long, CharacterSaveData> PeerCharacters => _peerCharacters;

        private Timer _autosaveTimer;


        #endregion

        #region Core - Entrada

        public void EnterPendingWorld()
        {
            var save = PendingWorld ?? Game.Managers.SaveManager.Node.CreateWorld("Mundo sem nome", (long)GD.Randi(), WorldCharacterMode.LocalCharacters, "", SavesConstants.DEFAULT_AUTOSAVE_INTERVAL_MINUTES);

            CurrentWorldSave = save;

            // Mundo nao procedural roda o mapa desenhado a mao, com streaming desligado.
            if (!save.IsProcedural)
            {
                Game.Managers.WorldManager.Node.SpawnLocalWorldAndPlayer(save, PendingCharacter);
            }
            else
            {
                Game.Managers.WorldManager.Node.CreateProceduralWorldAndPlayer(save, PendingCharacter);
            }

            StartAutosave(save);

            PendingWorld = null;
        }

        public string SpawnWorldAndJoin(string textAddress)
        {
            Game.Managers.WorldManager.Node.SpawnWorld();

            return Game.Managers.NetworkManager.Node.JoinServer(textAddress);
        }

        #endregion

        #region Core - Saida

        // Sair do mundo: primeiro persiste e limpa o que e sessao, depois manda desmontar a cena.
        public void LeaveWorld()
        {
            PersistBeforeLeaving();

            StopAutosave();

            CurrentWorldSave = null;
            PendingCharacter = null;

            Game.Managers.NetworkManager.Node.CloseSession();

            Game.Managers.WorldManager.Node.DespawnWorld();
        }

        public void ReturnToMainMenu()
        {
            GetTree().Paused = false;

            Game.Managers.RouterManager.Node.Close(Game.Ui.PauseUI.Node);

            LeaveWorld();

            Game.Managers.RouterManager.Node.Replace(Game.Ui.StartUI.Node);
        }

        #endregion

        #region Core - Personagem, join e politica de save

        // Quando salvar e o que entra: a sessao sabe o que esta em jogo, o SaveManager grava.
        public void SaveEverything()
        {
            if (CurrentWorldSave == null)
            {
                return;
            }

            Game.Managers.SaveManager.Node.SaveWorld(CurrentWorldSave);

            SaveOwnLocalCharacter();
            SaveRemotePeerCharacters();
        }

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

            _autosaveTimer.Timeout += SaveEverything;

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

        private void SaveOwnLocalCharacter()
        {
            if (PendingCharacter == null)
            {
                return;
            }

            var localPlayer = Game.Managers.WorldManager.Node.GetLocalPlayer();

            if (localPlayer == null)
            {
                return;
            }

            PendingCharacter.Data = localPlayer.Data;
            PendingCharacter.LastPlayedUtc = Game.Managers.SaveManager.Node.NowUtc();

            Game.Managers.SaveManager.Node.SaveLocalCharacter(PendingCharacter);
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
                character.LastPlayedUtc = Game.Managers.SaveManager.Node.NowUtc();

                if (CurrentWorldSave.CharacterMode == WorldCharacterMode.ServerCharacters)
                {
                    Game.Managers.SaveManager.Node.SaveServerCharacter(CurrentWorldSave.MultiplayerKey, character);
                }
                else
                {
                    Game.Managers.SaveManager.Node.SaveBackup(character.OwnerProfileId, character);
                }
            }
        }

        public void PersistBeforeLeaving()
        {
            SaveOwnLocalCharacter();

            if (CurrentWorldSave != null && IsHostOrSolo())
            {
                SaveEverything();
            }
        }

        public override void _Ready()
        {
            Game.WhenReady(() => GetTree().Root.CloseRequested += PersistBeforeLeaving);

            // A rede so avisa; quem reage e quem tem o estado. E o que mantem a seta numa
            // direcao so: Session -> Network, nunca de volta.
            Game.WhenReady(() =>
            {
                var network = Game.Managers.NetworkManager.Node;

                network.PeerLeft += OnPeerLeft;
                network.Disconnecting += PersistBeforeLeaving;
                network.ConnectionSucceeded += RequestJoinInfo;
                network.ServerDisconnected += ReturnToMainMenu;
            });
        }

        private void OnPeerLeft(long peerId, Jogo25D.Characters.Player playerNode)
        {
            SavePeerCharacterOnDisconnect(peerId, playerNode);

            ForgetPeer(peerId);
        }

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

        // API unica pra quem escolhe personagem: a tela pede, o SaveManager resolve. Quem decide
        // se e local ou de servidor e o CharacterMode da sessao, nunca o chamador.
        public void CreateCharacter(string name)
        {
            if (CharacterMode == WorldCharacterMode.ServerCharacters)
            {
                CreateServerCharacterRequest(name);

                return;
            }

            Game.Ui.CharacterSelectUI.Node.CompleteLocalCreation(Game.Managers.SaveManager.Node.CreateLocalCharacter(name));
        }

        public void DeleteCharacter(string characterId)
        {
            if (CharacterMode == WorldCharacterMode.ServerCharacters)
            {
                DeleteServerCharacterRequest(characterId);

                return;
            }

            Game.Managers.SaveManager.Node.DeleteLocalCharacter(characterId);
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

            PendingCharacter = character;

            EnterPendingWorld();
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
            if (!Multiplayer.IsServer() || CurrentWorldSave == null)
            {
                return;
            }

            var senderId = Multiplayer.GetRemoteSenderId();

            RpcId(senderId, nameof(JoinInfoReceive), (int)CurrentWorldSave.CharacterMode);
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

            var profile = Game.Managers.SaveManager.Node.GetOrCreateLocalProfile();

            RpcId(1, nameof(RequestServerCharacterListServerReceive), profile?.ProfileId ?? "");
        }

        public void SubmitLocalCharacterForJoin(CharacterSaveData character)
        {
            if (character == null)
            {
                return;
            }

            PendingCharacter = character;

            var profile = Game.Managers.SaveManager.Node.GetOrCreateLocalProfile();

            RpcId(1, nameof(SubmitLocalCharacterServerReceive), profile?.ProfileId ?? "", Jogo25D.Utils.GodotDictionaryParser.GodotDictionaryParser.ToDictionary(character));
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SubmitLocalCharacterServerReceive(string profileId, Godot.Collections.Dictionary characterDict)
        {
            if (!Multiplayer.IsServer() || CurrentWorldSave == null || CurrentWorldSave.CharacterMode != WorldCharacterMode.LocalCharacters)
            {
                return;
            }

            var senderId = Multiplayer.GetRemoteSenderId();
            var character = Jogo25D.Utils.GodotDictionaryParser.GodotDictionaryParser.ToResource<CharacterSaveData>(characterDict);

            if (character == null)
            {
                character = Game.Managers.SaveManager.Node.CreateLocalCharacter("Sem nome");
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
            if (!Multiplayer.IsServer() || CurrentWorldSave == null || CurrentWorldSave.CharacterMode != WorldCharacterMode.ServerCharacters)
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
            var characters = Game.Managers.SaveManager.Node.ListServerCharacters(CurrentWorldSave.MultiplayerKey)
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
            if (!Multiplayer.IsServer() || CurrentWorldSave == null || CurrentWorldSave.CharacterMode != WorldCharacterMode.ServerCharacters)
            {
                return;
            }

            var senderId = Multiplayer.GetRemoteSenderId();

            Game.Managers.SaveManager.Node.DeleteServerCharacter(CurrentWorldSave.MultiplayerKey, characterId);

            SendServerCharacterListTo(senderId);
        }

        public void DeleteServerCharacterRequest(string characterId)
        {
            RpcId(1, nameof(DeleteServerCharacterServerReceive), characterId);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ServerCharacterListReceive(Godot.Collections.Array summaries)
        {
            ServerCharacterListAvailable?.Invoke(CurrentWorldSave?.MultiplayerKey ?? "", summaries);
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
            if (!Multiplayer.IsServer() || CurrentWorldSave == null || CurrentWorldSave.CharacterMode != WorldCharacterMode.ServerCharacters)
            {
                return;
            }

            var senderId = Multiplayer.GetRemoteSenderId();
            var character = Game.Managers.SaveManager.Node.LoadServerCharacter(CurrentWorldSave.MultiplayerKey, characterId);

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
            if (!Multiplayer.IsServer() || CurrentWorldSave == null || CurrentWorldSave.CharacterMode != WorldCharacterMode.ServerCharacters)
            {
                return;
            }

            var senderId = Multiplayer.GetRemoteSenderId();
            var saveManager = Game.Managers.SaveManager.Node;
            var ownerProfileId = _pendingProfileByPeer.TryGetValue(senderId, out var profileId) ? profileId : "";
            var character = Game.Managers.SaveManager.Node.CreateServerCharacter(CurrentWorldSave.MultiplayerKey, name, ownerProfileId);

            if (character == null)
            {
                return;
            }

            _peerCharacters[senderId] = character;

            Game.Managers.NetworkManager.Node.FinishPeerJoin(senderId, character);
        }

        private void SavePeerCharacterOnDisconnect(long id, Player playerNode)
        {
            if (playerNode == null || CurrentWorldSave == null || !_peerCharacters.TryGetValue(id, out var character))
            {
                return;
            }

            var saveManager = Game.Managers.SaveManager.Node;

            if (saveManager == null)
            {
                return;
            }

            character.Data = playerNode.Data;
            character.LastPlayedUtc = Game.Managers.SaveManager.Node.NowUtc();

            if (CurrentWorldSave.CharacterMode == WorldCharacterMode.ServerCharacters)
            {
                Game.Managers.SaveManager.Node.SaveServerCharacter(CurrentWorldSave.MultiplayerKey, character);
            }
            else
            {
                Game.Managers.SaveManager.Node.SaveBackup(character.OwnerProfileId, character);
            }
        }

        #endregion
    }
}
