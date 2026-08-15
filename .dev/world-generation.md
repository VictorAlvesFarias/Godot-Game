# Mundo: seleção, criação, streaming e geração — fluxo integrado

Documento de referência ponta a ponta: do clique do jogador na UI até o chunk pintado na tela. Cobre o estado atual do código (pós-reorganização de `ChunkStreamingManager`/`ChunkGeneratorSystem`/`WorldManager`, biomas N-genéricos via `BiomeDB.OrderedIds`, padrão Request/Receive consolidado).

---

## 1. Usuário escolhe mundo e personagem (UI)

```
WorldSelectUI                          CharacterSelectUI                    WorldManager
─────────────                          ─────────────────                    ────────────
jogador clica num mundo salvo
(ou "Mundo Padrão")
  └─ OnWorldRowPressed(world) /
     OnDefaultWorldPressed()
       ├─ NetworkManager.PendingWorld = world (ou null)
       ├─ NetworkManager.PendingWorldIsDefault = true/false
       └─ Close() + abre CharacterSelectUI.OpenForOwnWorld()
                                         │
                                         ▼
                                        jogador seleciona/cria personagem
                                          └─ OnLocalSelected?.Invoke(character)
                                               ├─ NetworkManager.PendingCharacter = character
                                               └─ NetworkManager.EnterPendingWorld() ──────────┐
                                                                                                 ▼
                                                                                    EnterPendingWorld()
                                                                                      if PendingWorldIsDefault:
                                                                                        SpawnLocalWorldAndPlayer()
                                                                                      else:
                                                                                        CreateProceduralWorldAndPlayer(PendingWorld)
```

`WorldSelectUI` e `CharacterSelectUI` não sabem nada sobre geração de mundo — as duas telas existem só pra popular `PendingWorld`/`PendingWorldIsDefault`/`PendingCharacter` no `WorldManager` e no fim chamar **um único método**. Toda a lógica de "como montar o mundo" fica isolada ali.

---

## 2. Criação/carregamento do mundo — `WorldManager.CreateProceduralWorldAndPlayer`

Passo a passo, na ordem real do método:

1. **`SpawnWorld()`** — instancia `World.tscn` uma única vez sob `Main/` (se já existe, não faz nada) e resolve `OverworldParent`/`UpsidedownParent`/os `SubViewportContainer` via `ResolveWorldReferences()`.
2. **`ClearWorldLayers()`** — limpa as `TileMapLayer` `Base`/`Compose` das duas dimensões. Essas layers **já vêm pré-criadas** dentro de `Overworld.tscn`/`Upsidedown.tscn` — `TileSet` e o script `TerrainLayer` já atribuídos no editor. Nada de layer é instanciado em runtime.
3. **`ChunkStreamingManager.SetWorldSeed(save.Seed)`** — o seed do save vira a única fonte de determinismo do resto do processo.
4. **`ChunkStreamingManager.ImportState(dimensionId, ...)`** ×2 — recarrega só as *mutações* salvas (bloco quebrado/colocado) de cada dimensão via `SaveManager.LoadDimensionState`. O terreno em si **nunca é salvo** — é sempre regerado a partir do seed, as mutações são reaplicadas por cima depois.
5. **`SetChunkStreamingEnabled(true)`** — liga `ChunkStreamingManager.Enabled`, destravando o loop de `_Process` (seção 3).
6. **`ChunkStreamingManager.PreloadSpawnAreaAsync(UPSIDEDOWN_ID, ..., Vector2.Zero)`** — carrega de forma síncrona (aguardada) um bloco de chunks ao redor da origem *antes* de soltar o jogador ali, pra ele não cair num buraco vazio. Usa `LoadChunkAsync` por baixo — o mesmo método que o loop de streaming normal usa.
7. **`RestorePortals(save)`** — reconstrói os portais salvos (`PropDB.Get("portal").Spawn(...)`).
8. **`RespawnLocalSoloPlayer()`** — instancia `Player.tscn`, aplica `PendingCharacter.Data` (ou dá o item inicial `"portal"` se for personagem novo), posiciona no chão via `FindGroundSpawnPosition` (varre células verticalmente até achar uma sólida), `SpawnPlayer(...)` adiciona no grupo `"players"`.
9. **`StartAutosaveTimer(save)`** — só roda se `IsHostOrSolo()`; cria um `Timer` com `WaitTime = AutosaveIntervalMinutes * 60`, conectado a `SaveCurrentWorld`.

**`SpawnLocalWorldAndPlayer()`** (mundo fixo/padrão) é o mesmo esqueleto bem mais curto: `SpawnWorld()` + `SetChunkStreamingEnabled(false)` + `RespawnLocalSoloPlayer()`. Sem seed, sem import, sem preload — nesse modo o `ChunkStreamingManager` fica **desligado**, o mapa é só o que está desenhado à mão na cena.

---

## 3. Loop de streaming em runtime — quem decide o que carregar/descarregar

`ChunkStreamingManager._Process(delta)` roda todo frame, mas só age a cada `EVALUATE_INTERVAL_SECONDS` (0.75s) e só se `IsServerAuthoritative()` (só quem manda decide; solo local também conta como autoritativo). A cada tick dispara `EvaluateAsync` pras duas dimensões em paralelo → `EvaluateCore`:

```
EvaluateCore(dimensionId, dimensionParent, ...)
 ├─ players = jogadores presentes nessa dimensão
 ├─ pra cada player: WorldToCell(GlobalPosition) → CellToChunk(cell)
 ├─ needed = todo chunk num raio LOAD_RADIUS_CHUNKS(2) de cada player
 ├─ missing = needed − loaded, ordenado por distância, corta em MAX_CHUNK_LOADS_PER_TICK(2)
 │    └─ LoadChunkAsync(...) por chunk faltando
 └─ toUnload = loaded fora do raio UNLOAD_RADIUS_CHUNKS(4)
      └─ UnloadChunkAsync(...) por chunk
```

`UNLOAD_RADIUS_CHUNKS` (4) é maior que `LOAD_RADIUS_CHUNKS` (2) de propósito — dá uma margem de histerese, evita carregar/descarregar o mesmo chunk repetidamente pra quem fica andando na borda.

**`LoadChunkAsync`** (privado):
```
ResolveLayer(dimensionId) / ResolveBaseLayer(dimensionId)   ← ponto único, layer sempre existe, nunca cria
loaded.Add(chunkCoord)
await _generator.PaintTilesAsync(...)                        ← geração de fato, seção 4
ApplyMutations(layer, chunkState, dimensionId)                ← reaplica break/place salvos
RecordDiscovered(dimensionId, layer, chunkCoord)               ← marca minimapa
pra cada peer que precisava desse chunk:
  LoadChunkRequest(peerId, dimensionId, chunkCoord, stateDict) ← RpcId(peerId, nameof(LoadChunkReceive), ...)
```

**`UnloadChunkAsync`**: `_generator.EraseTilesAsync(...)` + `UnloadChunkRequest(peerId, ...)` por peer que tinha o chunk carregado.

---

## 4. A geração de fato — `ChunkGeneratorSystem.PaintTilesAsync`

`_generator` é uma instância de `ChunkGeneratorSystem` guardada como campo do `ChunkStreamingManager` (não é mais `static class`). Pra cada chunk:

### 4.1 Resolver bioma + altura do chão, coluna por coluna

```
GetBiomeIdAtPosition(worldSeed, dimensionId, worldX, worldY)
 ├─ axisValue = GetSmoothedBiomeAxisValue(...)         ← média de várias amostras de ruído 1D em volta de X
 ├─ proximity = GetProximityToNearestBiomeBoundary(...) ← quão perto está da fronteira entre 2 biomas
 ├─ se perto de fronteira:
 │    warpOffset = GetBiomeBoundaryWarpOffset(...)      ← ruído fractal em função de Y, escalado pela proximidade
 │    axisValue recalculado com X deslocado pelo warp   ← ondula a linha de fronteira ao longo de Y
 └─ PickBiomeIdForAxisValue(axisValue, BiomeDB.OrderedIds)
      normaliza [-1,1] → [0,1] → índice → biomeIds[índice]
```

O número de biomas é **dinâmico** — `BiomeDB.OrderedIds` divide o eixo de -1 a 1 em N faixas iguais (`bandWidth = 2f / biomeCount`). Cadastrar um bioma novo no `BiomeDB` já ganha uma faixa automaticamente no mundo, sem tocar no algoritmo de geração.

`ResolveSolidCellsByBiome` usa isso duas vezes por coluna: uma vez no centro do chunk (decide a altura do chão daquela coluna, via `BiomeDefinition.HeightOffset`/`HeightAmplitude`/`NoiseFrequency`), e uma vez **por célula sólida** abaixo do chão (pode dar um bioma diferente do da coluna, perto de uma fronteira ondulada — por isso um chunk pode ter blocos de biomas diferentes empilhados).

### 4.2 Autotile — `TerrainLayer` (mediator próprio, não é o autotile nativo do Godot)

`BuildBiomeGroups` agrupa as células sólidas por bioma e inclui vizinhos de borda já pintados no chunk adjacente (`AddSolidBorderNeighbors`). Depois, por grupo:

```
target.ConnectAsync(cells, terrainSet)                                ← pinta o Compose
target.ReconnectForeignBorderAsync(cells, terrainSet)                 ← corrige costura com terrain set vizinho
baseTarget.ConnectDependentAsync(target, cells, BorderCapTerrainSet)  ← pinta o Base (só onde Compose é sólido)
baseTarget.ReconnectForeignBorderDependentAsync(...)
```

`TerrainLayer.Connect(Async)` calcula, pra cada célula, a assinatura de 8 vizinhos (quais peering bits estão conectados) e busca no `TileSet` o tile que bate — com fallback progressivo derrubando bits de canto se não achar exato. É o **mesmo mediator**, sem exceção, seja geração procedural ou jogador quebrando/colocando bloco (`WorldManager.PaintBlockAndReconnect`/`EraseBlockAndReconnect`) — por isso não existe conexão nativa do Godot no meio do caminho.

### 4.3 Estruturas (árvores) — `PlaceStructures`

Depois do terreno pintado, pra cada coluna cujo bioma tem `StructureIds` não vazio (hoje só `lime_ground` → `["tree"]`):
- Rola chance (`WorldRandom.StructureRandom(...) >= structure.Chance`) — se não passar, pula.
- Mede a bounding box real da estrutura (`structure.GetBounds(...)` — gera a árvore inteira só pra medir).
- Confere volume livre (`IsStructureVolumeClear`) e espaçamento mínimo contra a última instância — o espaçamento escaneia até `MaxSpacingLookbackTiles` (32) *pra trás do início do chunk*, pra não resetar o cursor a cada chunk novo.
- Se passou em tudo, `structure.CollectCells(...)` gera de verdade e devolve grupos de célula por terrain set (tronco/folha), que viram `target.Connect(cells, terrainSet)` no final — autotile de novo, seção 4.2.

Tudo isso usa **`WorldRandom`** (`Random(worldSeed, context, worldX, salt)` / variante com `range`) — hash determinístico puro, sem estado, é a única fonte de "aleatoriedade" de todo o pipeline (bioma, altura, estrutura).

---

## 5. Multiplayer — replicar o que o servidor decidiu

Todo `*Receive` (RPC handler de verdade, `[Rpc]`-atributado) só é disparado de dentro do seu `*Request` correspondente — nunca direto:

| Request (único disparador) | Receive (handler `[Rpc]`) | Quando dispara |
|---|---|---|
| `LoadChunkRequest(peerId, ...)` | `LoadChunkReceive(...)` | depois que o servidor termina de pintar um chunk localmente, avisa cada peer que precisava dele |
| `UnloadChunkRequest(peerId, ...)` | `UnloadChunkReceive(...)` | depois que o servidor apaga um chunk localmente |
| `SetWorldSeedRequest(targetPeerId)` | `SetWorldSeedReceive(seed)` | quando um peer novo entra (`CatchUpPeer`) |

`CatchUpPeer(targetPeerId)` (chamado quando um peer novo conecta) é o "onboarding": `SetWorldSeedRequest` primeiro, depois `CatchUpDimension` por dimensão, que reenvia `LoadChunkRequest` (com o `ChunkStateData` serializado) pra **todo chunk já carregado no servidor** — o cliente novo regenera cada chunk localmente a partir do seed recebido; só as mutações vêm pela rede, o terreno nunca é transmitido como dado.

No fluxo de entrada de um peer (`WorldManager.FinishPeerJoin`): `PreloadSpawnAreaAsync` na posição do player novo → `RpcId(id, nameof(ClearWorldLayersReceive))` (limpa o que o cliente tinha desenhado por padrão) → `ChunkStreamingManager.CatchUpPeer(id)` → `SpawnPlayer`/`SpawnPlayerRequest`.

---

## 6. Persistência — o que é salvo e quando

- **Mutações** (`RecordMutation`, chamado por `WorldManager` toda vez que um bloco é quebrado/colocado) — guardadas em `ChunkStateData.Mutations` por chunk, em memória, até o autosave.
- **`SaveCurrentWorld()`** (chamado pelo `AutosaveTimer` e ao sair do mundo): `ChunkStreamingManager.ExportState(dimensionId)` serializa as mutações de todo chunk conhecido (mesmo os já descarregados — o dicionário `_overworldState`/`_upsidedownState` não é limpo no unload) → `SaveManager.SaveDimensionState(...)`. Também salva portais (`CollectPortals()`) e metadados do save (`SaveWorldMeta`).
- **Terreno em si nunca é salvo** — regenerado sempre a partir do `WorldSeed`, determinístico via `WorldRandom`.

---

## Mapa rápido de arquivos

```
Features/UI/WorldSelect/View/WorldSelectUI.cs        seleção de mundo (clique → PendingWorld)
Features/UI/CharacterSelect/View/CharacterSelectUI.cs seleção de personagem (clique → EnterPendingWorld)
Features/World/Core/Managers/WorldManager.cs          orquestra criação/join/save; mutações de bloco
Features/World/Chunks/Managers/ChunkStreamingManager.cs  loop de load/unload, RPC, persistência, WorldSeed
Features/World/Chunks/Systems/ChunkGeneratorSystem.cs    pintura de chunk, resolução de bioma, estruturas
Features/World/Chunks/Singletons/WorldRandom.cs          hash/RNG determinístico compartilhado
Features/World/Biomes/Database/BiomeDB.cs                registro de biomas (N genérico via OrderedIds)
Features/World/Biomes/Structures/BiomeDefinition.cs       dados de cada bioma (ruído, terrain sets, estruturas)
Features/World/Biomes/Singletons/TerrainLayer.cs          autotile mediator (Connect/ReconnectForeignBorder)
Features/World/Structures/Database/StructureDB.cs         registro de estruturas (árvore)
Features/World/Structures/Definitions/TreeStructureDefinition.cs  algoritmo da árvore
```
