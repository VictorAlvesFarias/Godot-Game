# Fluxo de início e fim de mundo

Quem chama quem, no estado atual do código (depois da separação sessão × cena × canal × disco). Levantado lendo os métodos, não de memória.

Convenção: cada nível de indentação é uma chamada. `←` é reação a sinal, não chamada direta.

---

## 1. Início — mundo próprio (solo ou host)

```
StartUI.OnPlayPressed()
└─ RouterManager.Open(WorldSelectUI)
   └─ WorldSelectUI.OnOpened()
      └─ WorldSelectUI.PopulateWorldRows()
         └─ SessionManager.ListWorlds()
            └─ SaveManager.ListWorlds()
               └─ SaveStorage.ListWorlds()            ← lê world.tres de cada pasta

WorldSelectUI.OnWorldRowPressed(world)
├─ SessionManager.PendingWorld = world                ← setter define CharacterMode do save
├─ CharacterSelectUI.CurrentContext = OwnWorld
└─ RouterManager.Open(CharacterSelectUI)
   └─ CharacterSelectUI.OnOpened()
      └─ CharacterSelectUI.ShowLocal()
         └─ SessionManager.ListCharacters()
            └─ SaveManager.ListLocalCharacters()
               └─ SaveStorage.ListLocalCharacters()

CharacterSelectUI.SelectLocal(character)
├─ SessionManager.SelectCharacter(character)
│  ├─ NetworkManager.IsConnected()                    ← false: mundo proprio
│  ├─ SessionManager.PendingCharacter = character
│  └─ SessionManager.EnterPendingWorld()
│     ├─ SessionManager.CurrentWorldSave = save
│     ├─ WorldManager.CreateProceduralWorldAndPlayer(save, character)
│     │  ├─ WorldManager.SpawnWorld()
│     │  │  ├─ GD.Load("World.tscn").Instantiate() + Main.AddChild()
│     │  │  └─ DimensionManager.ResolveReferences()   ← acha parents, containers, layers
│     │  ├─ DimensionManager.ClearLayers()
│     │  ├─ ChunkStreamingManager.SetWorldSeed(save.Seed)
│     │  ├─ ChunkStreamingManager.ImportState(overworld, SaveManager.LoadDimensionState(...))
│     │  ├─ ChunkStreamingManager.ImportState(upsidedown, ...)
│     │  ├─ WorldManager.SetChunkStreamingEnabled(true)
│     │  ├─ LoadingUI.Open()
│     │  ├─ await ChunkStreamingManager.PreloadSpawnAreaAsync(upsidedown, parent, Zero)
│     │  ├─ DimensionManager.RestoreProps(save)       ← recria portais do save
│     │  ├─ WorldManager.RespawnLocalSoloPlayer(character)
│     │  │  ├─ Player.tscn.Instantiate()
│     │  │  ├─ DimensionManager.FindGroundSpawnPosition(upsidedown, 0)
│     │  │  ├─ player.Data = character.Data.Duplicate()   (ou GiveItem("portal") se novo)
│     │  │  ├─ DimensionManager.SpawnPlayer(player)
│     │  │  └─ DimensionManager.SpawnTestNPC()
│     │  ├─ LoadingUI.Close()
│     │  └─ RouterManager.Open(HudUI)
│     ├─ SessionManager.StartAutosave(save)           ← Timer -> SaveEverything
│     └─ SessionManager.PendingWorld = null
└─ RouterManager.Close(CharacterSelectUI)
```

**Mundo não procedural** (checkbox desmarcada): idêntico, trocando `CreateProceduralWorldAndPlayer` por `SpawnLocalWorldAndPlayer(save, character)` — sem seed, sem `ImportState`, sem preload e **sem `ClearLayers`**, porque o terreno é o desenhado à mão na cena.

---

## 2. Início — cliente entrando num servidor

```
MultiplayerUI.OnConnectPressed()
└─ SessionManager.SpawnWorldAndJoin(endereco)
   ├─ WorldManager.SpawnWorld()                       ← cena vazia, sem terreno ainda
   └─ NetworkManager.JoinServer(endereco)

[cliente] NetworkManager.OnConnectedToServer()
└─ emite ConnectionSucceeded
   ├─← MultiplayerUI (fecha a espera)
   └─← SessionManager.RequestJoinInfo()
      └─ RpcId(1, RequestJoinInfoServerReceive)

[servidor] SessionManager.RequestJoinInfoServerReceive()
└─ RpcId(sender, JoinInfoReceive, (int)CurrentWorldSave.CharacterMode)

[cliente] SessionManager.JoinInfoReceive(mode)
├─ SessionManager.CharacterMode = mode
├─ modo LocalCharacters:
│  ├─ CharacterSelectUI.CurrentContext = PeerJoinLocal
│  └─ RouterManager.Open(CharacterSelectUI)
└─ modo ServerCharacters:
   └─ RpcId(1, RequestServerCharacterListServerReceive, profileId)
      └─ [servidor] SessionManager.SendServerCharacterListTo(sender)
         └─ RpcId(sender, ServerCharacterListReceive, resumos)
            └─ [cliente] emite ServerCharacterListAvailable
               └─← MultiplayerUI abre o CharacterSelectUI em modo servidor

[cliente] CharacterSelectUI.SelectLocal(character)
└─ SessionManager.SelectCharacter(character)
   ├─ NetworkManager.IsConnected()                    ← true
   └─ SessionManager.SubmitLocalCharacterForJoin(character)
      └─ RpcId(1, SubmitLocalCharacterServerReceive, profileId, dados)

[servidor] SessionManager.SubmitLocalCharacterServerReceive(profileId, dados)
├─ _peerCharacters[senderId] = character
└─ NetworkManager.FinishPeerJoin(senderId, character)
   ├─ Player.tscn.Instantiate() + Data do personagem
   ├─ await ChunkStreamingManager.PreloadSpawnAreaAsync(...)
   ├─ DimensionManager.FindGroundSpawnPosition(...)
   ├─ DimensionManager.RpcId(peer, ClearLayersReceive)   ← limpa o mapa padrao do cliente
   ├─ ChunkStreamingManager.CatchUpPeer(peer)            ← manda os chunks ja carregados
   ├─ DimensionManager.SpawnPlayer(player)               ← no servidor
   ├─ DimensionManager.SpawnPlayerRequest(player)        ← replica pros outros
   └─ para cada player/NPC existente: SpawnPlayerRequest / SpawnNpcRequest / SpawnWorldItemRequest
```

Os caminhos de `SelectServerCharacterRequest` e `CreateServerCharacterRequest` terminam igual: `*ServerReceive` → `_peerCharacters[sender]` → `NetworkManager.FinishPeerJoin`.

---

## 3. Fim — sair pelo menu

```
PauseUI.OnMenuPressed()
├─ RouterManager.Close(PauseUI)
├─ GetTree().Paused = false
├─ WorldManager.GetLocalPlayer().Input.RemoveBlocker("pause")
├─ SessionManager.LeaveWorld()
│  ├─ SessionManager.PersistBeforeLeaving()
│  │  ├─ SessionManager.SaveOwnLocalCharacter()
│  │  │  ├─ WorldManager.GetLocalPlayer()
│  │  │  ├─ PendingCharacter.Data = player.Data
│  │  │  └─ SaveManager.SaveLocalCharacter(PendingCharacter)
│  │  │     └─ SaveStorage.SaveLocalCharacter(...)
│  │  └─ se IsHostOrSolo: SessionManager.SaveEverything()
│  │     ├─ SaveManager.SaveWorld(CurrentWorldSave)
│  │     │  ├─ ChunkStreamingManager.ExportState(overworld) -> SaveStorage.SaveDimensionState
│  │     │  ├─ ChunkStreamingManager.ExportState(upsidedown) -> SaveStorage.SaveDimensionState
│  │     │  ├─ save.Props = DimensionManager.CollectProps()
│  │     │  └─ SaveStorage.SaveWorldMeta(save)
│  │     ├─ SessionManager.SaveOwnLocalCharacter()
│  │     └─ SessionManager.SaveRemotePeerCharacters()
│  │        └─ por peer: SaveManager.SavePeerCharacter(character, mode, key)
│  ├─ SessionManager.StopAutosave()
│  ├─ CurrentWorldSave = null; PendingCharacter = null
│  ├─ NetworkManager.CloseSession()                   ← fecha peer, limpa dicionarios
│  └─ WorldManager.DespawnWorld()
│     ├─ Main/World.QueueFree()                       ← mata player, props, itens de uma vez
│     ├─ DimensionManager.Reset()
│     ├─ ChunkStreamingManager.ResetState()
│     └─ RouterManager.Close(HudUI)
└─ RouterManager.Replace(StartUI)
```

`SessionManager.ReturnToMainMenu()` é o mesmo caminho, e é o que o `NetworkManager` dispara por sinal quando o servidor cai.

---

## 4. Fim — desconexão

```
NetworkManager.Disconnect()
├─ emite Disconnecting
│  └─← SessionManager.PersistBeforeLeaving()          (mesmo bloco do item 3)
├─ Peer.Close(); Multiplayer.MultiplayerPeer = null
├─ libera todos os nós do grupo "players"
└─ WorldManager.CallDeferred("RespawnLocalSoloPlayer")   ⚠ ver "quebrado" abaixo

[cliente] NetworkManager.OnServerDisconnected()
└─ emite ServerDisconnected
   └─← SessionManager.ReturnToMainMenu()              (item 3)

NetworkManager.OnPeerDisconnected(id)                 [servidor]
├─ emite PeerLeft(id, playerNode)
│  └─← SessionManager.OnPeerLeft(id, player)
│     ├─ SessionManager.SavePeerCharacterOnDisconnect(id, player)
│     │  └─ SaveManager.SavePeerCharacter(character, mode, key)
│     └─ SessionManager.ForgetPeer(id)
├─ playerNode.QueueFree()
└─ ChunkStreamingManager.RemovePeer(id)
```

---

## Direção das dependências

```
SessionManager  ──►  SaveManager (22) · NetworkManager (7) · WorldManager (5) · RouterManager (3)
WorldManager    ──►  ChunkStreaming (6) · Router (3) · Save (2) · Dimension (1)
NetworkManager  ──►  Dimension (5) · World (2) · ChunkStreaming (2)
SaveManager     ──►  Dimension (1) · ChunkStreaming (1)
                     └─►  SaveStorage (system)
```

Nenhuma seta volta. As três que voltavam viraram sinal do `NetworkManager`: `Disconnecting`, `ConnectionSucceeded`, `ServerDisconnected` — o canal avisa, quem tem estado reage.

Regra que sustenta isso: **o `WorldManager` recebe por parâmetro** (`save`, `character`) em vez de ler a sessão. Foi o que quebrou o ciclo `Session ↔ World`.

---

## Quebrado agora (introduzido na última mudança)

```
NetworkManager.Disconnect()
└─ WorldManager.CallDeferred("RespawnLocalSoloPlayer")   ← método agora exige (CharacterSaveData)
```

`RespawnLocalSoloPlayer` passou a receber o personagem por parâmetro, e essa chamada por nome continua sem argumento. **Falha em runtime** ao desconectar de um servidor, não em compilação. Correção provável: `Disconnect()` deixar de recriar o player solo e a sessão decidir isso ao reagir a `Disconnecting`.

## Não testado

Todo o item 2 e o item 4 dependem de RPC, e os 8 RPC de personagem mudaram de nó duas vezes (`NetworkManager` → `SaveManager` → `SessionManager`). Nada disso foi executado com dois peers.
