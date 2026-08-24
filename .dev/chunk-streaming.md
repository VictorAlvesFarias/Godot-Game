# Chunk streaming: o que "carregado" significa, quem manda e o que volta do save

Documento de referência do [ChunkStreamingManager.cs](../Game/Features/World/Chunks/Managers/ChunkStreamingManager.cs). Tudo aqui foi lido no código em 2026-08-24, não é memória de conversa.

Complementa [world-generation.md](world-generation.md) (que descreve a geração em si — bioma, altura de coluna, autotile, estruturas). As seções 1 e 2 daquele documento estão **desatualizadas** — falam de `NetworkManager.PendingWorld` e "Mundo Padrão", que não existem mais.

---

## 1. Carregado/descarregado é célula de tile, não node

É a parte que mais engana pelo nome. O chunk loader **não instancia e não libera nenhum node**. Todo o ciclo de vida acontece dentro de duas `TileMapLayer` que já existem na cena:

| termo | o que acontece de fato |
|---|---|
| **carregar** | `PaintTilesAsync` escreve `SetCell(cell, sourceId, atlasCoord)` nas layers `Base` e `Compose` da dimensão, 200 células por frame |
| **descarregar** | `EraseTilesAsync` escreve `SetCell(cell, -1)` nas mesmas células |

As layers vêm prontas do `.tscn` (`Overworld.tscn`, `Upsidedown.tscn`), com `TileSet` e script já atribuídos — nunca são criadas em runtime.

O que o manager guarda é bookkeeping puro:

```
_loadedOverworld / _loadedUpsidedown        HashSet<Vector2I>  chunks pintados agora
_overworldState  / _upsidedownState         Dictionary<Vector2I, ChunkStateData>  mutações por chunk
_overworldLoadedPeers / _upsidedownLoadedPeers   Dictionary<Vector2I, HashSet<long>>  quem recebeu cada chunk
_discoveredOverworld / _discoveredUpsidedown     imagem do minimapa
```

### Consequência: entidade nenhuma entra nesse ciclo

`Player`, `Prop`/`Portal`, `NPC` e `WorldItem` estão **fora do streaming**. Quem cria e destrói esses nodes é o `DimensionManager`, e não existe hoje nenhuma ligação entre distância do player e ciclo de vida de entidade.

Na prática: um `WorldItem` dropado num chunk que descarrega continua vivo na árvore, pendurado no parent da dimensão, pairando sobre chão apagado. Cem itens dropados são cem nodes ativos para sempre, independentemente de onde os players estejam.

---

## 2. Quem decide, e com que orçamento

`_Process` só faz alguma coisa se `Enabled` **e** `IsServerAuthoritative()` (servidor, ou solo sem peer). O cliente puro nunca avalia nada — ele só obedece.

Constantes em [ChunkStreamingConstants.cs](../Game/Constants/ChunkStreamingConstants.cs):

| constante | valor | efeito |
|---|---:|---|
| `CHUNK_SIZE` | 32 | células por lado do chunk |
| `LOAD_RADIUS_CHUNKS` | 2 | quadrado 5×5 de chunks ao redor de cada player |
| `UNLOAD_RADIUS_CHUNKS` | 4 | quadrado 9×9; só descarrega fora disso |
| `MAX_CHUNK_LOADS_PER_TICK` | 2 | teto de chunks pintados por avaliação |
| `EVALUATE_INTERVAL_SECONDS` | 0.75 | período da avaliação |

Ciclo por dimensão, em `EvaluateCore`:

1. lista os players cujo `GetParent()` é o parent daquela dimensão (peers com `PeerId > 0`)
2. monta `needed` = união dos quadrados de raio 2 ao redor de cada player, e `neededByPeer` = quais peers pediram cada coordenada
3. `missing` = `needed` que ainda não está em `loaded`, **ordenado pela distância Chebyshev ao player mais próximo**, cortado em `MAX_CHUNK_LOADS_PER_TICK`
4. carrega esses
5. varre `loaded` e marca para descarregar todo chunk que não está a ≤ 4 chunks de **nenhum** player
6. descarrega

Os dois raios diferentes (2 para carregar, 4 para descarregar) são histerese: sem isso, um player andando na fronteira faria o mesmo chunk pintar e apagar em ciclos. Nada é carregado especulativamente na direção do movimento — só o quadrado ao redor da posição atual.

Cada dimensão tem sua própria avaliação e sua própria guarda de reentrada (`_isEvaluatingOverworld`, `_isEvaluatingUpsidedown`), então uma pintura longa não bloqueia a outra dimensão nem empilha avaliações.

---

## 3. O que volta do save: semente e mutação, nunca tile

O `.tres` **não guarda tile nenhum**. Guarda:

- a **semente** do mundo (`WorldSaveData.Seed`)
- por chunk, uma lista de `ChunkMutationData` (`Type`, `Position`, `ExtraData`)

O terreno é sempre regerado pela semente; o save só descreve o que o jogador mudou em cima dela.

### Gravação

`TerrainLayer` registra a mutação no momento em que o bloco muda:

```
ProcessBreakBlock   → RecordMutation(DimensionId, cell, "break", "")
PlaceBlockAuthoritative → RecordMutation(DimensionId, cell, "place", blockId)
```

`RecordMutation` converte célula → chunk e faz `chunkState.Mutations.Add(...)`. Isso é memória viva; só vira arquivo quando o `SaveManager` chama `ExportState(dimensionId)`, que achata o dicionário em `DimensionSaveData.Chunks`.

### Leitura

Em `WorldManager.CreateProceduralWorldAndPlayer`, **antes** de ligar o streaming:

```
SetWorldSeed(save.Seed)
ImportState(OVERWORLD_ID,  SaveManager.LoadDimensionState(...))
ImportState(UPSIDEDOWN_ID, SaveManager.LoadDimensionState(...))
SetChunkStreamingEnabled(true)
PreloadSpawnAreaAsync(...)
```

A ordem importa: `ImportState` popula `_overworldState`/`_upsidedownState` com as mutações do disco, e só então o loop começa a pintar. Assim, quando `LoadChunkAsync` roda para um chunk, `state[chunkCoord]` já tem o histórico salvo.

### A ordem dentro de `LoadChunkAsync`

```
loaded.Add(chunkCoord)
PaintTilesAsync(...)          → terreno procedural puro, direto da semente
ApplyMutations(...)           → reaplica break/place por cima, na ordem gravada
RecordDiscovered(...)         → marca as células no minimapa
loadedPeers[chunkCoord] = requestingPeers
→ RpcId para cada peer que precisa
```

`ApplyChunkMutation` no `TerrainLayer` traduz cada entrada: `"break"` chama `EraseBlockAndReconnect` (ou `BreakDecorationOnly` se a célula da `Compose` já estiver vazia), `"place"` resolve o `blockId` no `BlockDB` e chama `PlaceBlock`. Ambos passam pelo autotile, então a borda reconecta certo.

Descarregar **não perde nada**: `UnloadChunkAsync` apaga os tiles mas não toca em `state`. As mutações continuam no dicionário e são reaplicadas quando o chunk voltar.

---

## 4. RPC: o servidor empurra, o cliente nunca pede

Essa é a resposta curta: **o servidor notifica, o cliente não pede nada.**

Os três RPCs do manager são todos `MultiplayerApi.RpcMode.Authority, CallLocal = false`:

| RPC | direção | quando |
|---|---|---|
| `SetWorldSeedReceive(seed)` | servidor → peer | primeira coisa do catch-up |
| `LoadChunkReceive(dimensionId, chunkCoord, stateDict)` | servidor → peer | chunk pintado, ou catch-up |
| `UnloadChunkReceive(dimensionId, chunkCoord)` | servidor → peer | chunk apagado |

`Authority` significa que só o servidor pode disparar; um cliente que tentasse chamar seria rejeitado pelo Godot. Não existe nenhum caminho de pedido do cliente — nem RPC `AnyPeer`, nem método público que um peer possa acionar.

**Cuidado com o nome.** `LoadChunkRequest`/`UnloadChunkRequest` são métodos privados que rodam **no servidor** e fazem o `RpcId` de saída. Aqui `Request` não é "o cliente pede ao servidor"; é o lado emissor do par Request/Receive do projeto. O mesmo vale para `requestingPeers` em `LoadChunkAsync`: é a lista que o **servidor calculou** a partir da posição dos players, não uma fila de pedidos que chegaram.

### O que trafega

Só coordenada e mutação. `LoadChunkReceive` manda `chunkCoord` e o `ChunkStateData` serializado em `Godot.Collections.Dictionary` — nunca o tilemap pintado. O cliente recebe, pinta o mesmo chunk com **a mesma semente** (`PaintTilesAsync`) e reaplica as mesmas mutações. Determinismo da geração é o que faz as duas pontas baterem; o pacote é minúsculo porque o terreno não viaja.

### Contabilidade de quem tem o quê

`loadedPeers[chunkCoord]` guarda quais peers receberam aquele chunk. Serve para o descarregamento: `UnloadChunkAsync` só manda `UnloadChunkReceive` para quem está nessa lista, e o próprio servidor (`ownPeerId`) é sempre pulado nos dois sentidos — ele já pintou localmente. `IsPeerConnected` filtra peer que caiu antes do envio, e `RemovePeer(peerId)` limpa o id de todos os chunks quando a conexão morre (chamado pelo `NetworkManager` no `OnPeerDisconnected`).

### Catch-up de quem entra

Dentro de `FinishPeerJoin`, na ordem:

```
PreloadSpawnAreaAsync(...)      → servidor pinta a área de spawn localmente (requestingPeers = {ownPeerId})
FindGroundSpawnPosition(...)
DimensionManager.RpcId(id, ClearLayersReceive)   → limpa o tilemap do cliente
ChunkStreamingManager.CatchUpPeer(id)
```

`CatchUpPeer` manda a semente e depois, para as **duas** dimensões, um `LoadChunkReceive` de **todo chunk atualmente em `loaded`**, adicionando o peer novo em `loadedPeers` de cada um.

O `ClearLayersReceive` antes é obrigatório: sem ele o cliente teria resíduo do mundo anterior e o `loaded.Contains(chunkCoord)` do lado dele descartaria os chunks que estão chegando.

---

## 5. Buracos conhecidos

Nada aqui está corrigido — é levantamento.

### 5.1 Chunk já carregado nunca chega num segundo peer

O `missing` filtra por `loaded`, que é **global do servidor**, não por peer:

```csharp
var missing = needed.Where(c => !loaded.Contains(c))
```

Se o chunk C foi carregado por causa do player A, `loaded` já o contém. Quando o player B caminha até C, C não entra em `missing`, `LoadChunkAsync` não roda, `loadedPeers[C]` continua só com A — e **B nunca recebe C**. O terreno simplesmente não aparece para ele.

Só não dá problema no caso em que C já estava carregado no instante em que B entrou, porque aí o `CatchUpPeer` mandou tudo. É um bug de dois players se afastando e depois se aproximando de regiões diferentes.

A correção natural é fazer a decisão por peer: comparar `neededByPeer[coord]` com `loadedPeers[coord]` e mandar `LoadChunkRequest` para quem falta, mesmo quando o chunk já está pintado no servidor.

### 5.2 Catch-up manda o mundo inteiro

`CatchUpDimension` envia todo chunk carregado das duas dimensões, sem filtrar por distância do ponto de spawn do peer novo. Com vários players espalhados, quem entra recebe e pinta regiões onde nunca vai chegar — e depois recebe o `UnloadChunkReceive` de cada uma.

### 5.3 Lista de mutações cresce sem fim

`chunkState.Mutations` é append-only. Quebrar e recolocar o mesmo bloco cem vezes grava cem entradas, todas reaplicadas em ordem a cada carregamento e todas gravadas no `.tres`. Não existe compactação por célula.

### 5.4 Entidade não é streamada nem, em parte, salva

Além do que a seção 1 descreve, `WorldItem` e `NPC` não têm código de persistência nenhum — somem ao sair do mundo. `Prop`/`Portal` são salvos, mas pelo `DimensionManager` (`CollectProps`/`RestoreProps`), fora do fluxo de chunk.

### 5.5 Detalhes menores

- `ApplyMutations` recebe `dimensionId` e não usa.
- O cliente guarda `ResolveState(dimensionId)[chunkCoord]` ao receber o chunk, mas nunca lê esse estado para nada — só o servidor exporta.
- `RecordDiscovered` varre 32×32 células por chunk carregado, nos dois lados, só para o minimapa.

---

## Mapa rápido de arquivos

| arquivo | papel |
|---|---|
| [ChunkStreamingManager.cs](../Game/Features/World/Chunks/Managers/ChunkStreamingManager.cs) | decisão de load/unload, RPCs, catch-up, export/import de estado |
| [ChunkGeneratorSystem.cs](../Game/Features/World/Chunks/Systems/ChunkGeneratorSystem.cs) | `PaintTilesAsync` / `EraseTilesAsync` — a escrita de célula |
| [TerrainLayer.cs](../Game/Features/World/Biomes/Singletons/TerrainLayer.cs) | autotile, `ApplyChunkMutation`, origem das mutações |
| [ChunkStateData.cs](../Game/Features/World/Chunks/Resources/ChunkStateData.cs) / [ChunkMutationData.cs](../Game/Features/World/Chunks/Resources/ChunkMutationData.cs) | o que é serializado |
| [ChunkStreamingConstants.cs](../Game/Constants/ChunkStreamingConstants.cs) | raios, tamanho de chunk, orçamento por tick |
| [WorldManager.cs](../Game/Features/World/Core/Managers/WorldManager.cs) | liga o streaming, importa estado, preload de spawn |
| [SessionManager.cs](../Game/Features/World/Session/Managers/SessionManager.cs) | `FinishPeerJoin` — preload, clear e catch-up do peer novo |
