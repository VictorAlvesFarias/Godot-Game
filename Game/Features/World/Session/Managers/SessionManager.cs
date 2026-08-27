using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.UI;
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

        // A sessao avisa que chegou a hora de escolher personagem, com o que a tela precisa
        // pra se montar. Quem decide qual tela abrir e a UI.
        public event System.Action<CharacterSelectContext, string, Godot.Collections.Array> CharacterSelectionRequired;

        // Sessao encerrada (saiu do mundo, ou o servidor caiu). A UI decide pra onde ir.
        public event System.Action SessionEnded;


        private readonly Dictionary<long, CharacterSaveData> _peerCharacters = new();
        private readonly Dictionary<long, string> _pendingProfileByPeer = new();

        public IReadOnlyDictionary<long, CharacterSaveData> PeerCharacters => _peerCharacters;


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

            var save_manager = Game.Managers.SaveManager.Node;

            save_manager.Register(save);
            save_manager.Register(PendingCharacter);
            save_manager.StartAutosave(save.AutosaveIntervalMinutes);

            PendingWorld = null;
        }

        public string SpawnWorldAndJoin(string textAddress)
        {
            Game.Managers.WorldManager.Node.SpawnWorld();

            return Game.Managers.NetworkManager.Node.JoinServer(textAddress);
        }

        #endregion

        #region Core - Saida

        public void LeaveWorld()
        {
            Game.Managers.SaveManager.Node.SaveAll();
            Game.Managers.SaveManager.Node.StopAutosave();
            Game.Managers.SaveManager.Node.ClearRegistry();

            CurrentWorldSave = null;
            PendingCharacter = null;

            Game.Managers.NetworkManager.Node.CloseSession();

            Game.Managers.WorldManager.Node.DespawnWorld();

            SessionEnded?.Invoke();
        }

        #endregion

        private void SincronizarPersonagens()
        {
            var localPlayer = Game.Managers.WorldManager.Node.GetLocalPlayer();

            if (PendingCharacter != null && localPlayer != null)
            {
                PendingCharacter.Data = localPlayer.Data;
            }

            foreach (var (peerId, character) in _peerCharacters)
            {
                var player = Game.Managers.WorldManager.Node.FindPlayerByPeerId(peerId);

                if (player != null)
                {
                    character.Data = player.Data;
                }
            }
        }

        public async void FinishPeerJoin(long id, CharacterSaveData character)
        {
            if (!Multiplayer.IsServer() || character == null)
            {
                return;
            }

            var player = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn").Instantiate<Player>();

            player.Name = $"Player{id}";
            player.Position = Godot.Vector2.Zero;
            player.PeerId = id;
            player.Data = (PlayerData)character.Data.Duplicate(true);
            player.Loaded = true;

            await Game.Managers.TileStreamingManager.Node.PreloadSpawnAreaAsync(ChunkStreamingConstants.UPSIDEDOWN_ID, Game.Managers.DimensionManager.Node.ResolveParent(ChunkStreamingConstants.UPSIDEDOWN_ID), player.Position);

            player.Position = Game.Managers.DimensionManager.Node.FindGroundSpawnPosition(ChunkStreamingConstants.UPSIDEDOWN_ID, player.Position.X);

            Game.Managers.DimensionManager.Node.RpcId(id, nameof(DimensionManager.ClearLayersReceive));
            Game.Managers.TileStreamingManager.Node.CatchUpPeer(id, player.Position);
            Game.Managers.DimensionManager.Node.SpawnPlayer(player);
            Game.Managers.DimensionManager.Node.SpawnPlayerRequest(player);

            var players = GetTree().GetNodesInGroup("players");

            foreach (Node node in players)
            {
                if (node is NPC)
                {
                    continue;
                }

                if (node is Player existingPlayer && existingPlayer.PeerId != id)
                {
                    GD.Print($"[NetworkManager.FinishPeerJoin] informing {id} about {existingPlayer.Name}");

                    Game.Managers.DimensionManager.Node.SpawnPlayerRequest(existingPlayer, id);
                }
            }

            var npc = Game.Managers.DimensionManager.Node.ResolveParent(ChunkStreamingConstants.UPSIDEDOWN_ID)?.GetNodeOrNull<Player>("NPC_Dummy");

            if (npc != null)
            {
                GD.Print($"[NetworkManager.FinishPeerJoin] informing {id} about NPC_Dummy");

                Game.Managers.DimensionManager.Node.SpawnNpcRequest(npc.Position, id);
            }

            Game.Managers.WorldManager.Node.Streaming?.CatchUpPeer(id, player.Position);
        }

        #region Core - Personagem, join e politica de save

        public override void _Ready()
        {
            Game.WhenReady(() => GetTree().Root.CloseRequested += Game.Managers.SaveManager.Node.SaveAll);

            // A rede so avisa; quem reage e quem tem o estado. E o que mantem a seta numa
            // direcao so: Session -> Network, nunca de volta.
            Game.WhenReady(() =>
            {
                var network = Game.Managers.NetworkManager.Node;

                network.PeerLeft += OnPeerLeft;

                // O que vive no no (estado do player) entra no Data antes de gravar.
                Game.Managers.SaveManager.Node.Saving += SincronizarPersonagens;
                network.Disconnecting += Game.Managers.SaveManager.Node.SaveAll;
                network.ConnectionSucceeded += RequestJoinInfo;
                network.ServerDisconnected += LeaveWorld;
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

        public void CreateCharacter(string name)
        {
            if (CharacterMode == WorldCharacterMode.ServerCharacters)
            {
                CreateServerCharacterRequest(name);

                return;
            }

            // Criou: ja entra com ele. A sessao nao navega nem toca em tela - quem reage a
            // entrada no mundo e a UI, pelo caminho normal do SelectCharacter.
            SelectCharacter(Game.Managers.SaveManager.Node.CreateLocalCharacter(name));
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
                CharacterSelectionRequired?.Invoke(CharacterSelectContext.PeerJoinLocal, "", null);

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

            FinishPeerJoin(senderId, character);
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
            CharacterSelectionRequired?.Invoke(CharacterSelectContext.PeerJoinServer, CurrentWorldSave?.MultiplayerKey ?? "", summaries);
        }

        public void SelectServerCharacterRequest(string characterId)
        {
            RpcId(1, nameof(SelectServerCharacterServerReceive), characterId);
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

            FinishPeerJoin(senderId, character);
        }

        public void CreateServerCharacterRequest(string name)
        {
            RpcId(1, nameof(CreateServerCharacterServerReceive), name);
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

            FinishPeerJoin(senderId, character);
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
