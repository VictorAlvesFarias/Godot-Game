# Streaming de mundo: tile, entidade e spawn genérico

Plano de arquitetura fechado em 2026-08-25. Substitui o rascunho anterior (`arquitetura-entity-lifecycle.md`).
**Nenhum código foi alterado ainda — isto é o desenho acordado.**

---

## 1. O conceito central: chunk é a unidade das duas correntes

Hoje o mundo tem **duas** categorias de conteúdo, e só uma delas é streamada:

| conteúdo | como chega | streamado? |
|---|---|---|
| tile | semente + mutações por chunk | **sim** — pinta e apaga conforme o player anda |
| entidade (prop, item, npc) | `RestoreProps` no carregamento do mundo | **não** — tudo de uma vez, para sempre |

A decisão: **entidade passa a seguir exatamente o mesmo modelo do tile.** O chunk vira a unidade de streaming das duas correntes.

```
chunk C entra no raio do player
  ├─ TILE:     PaintTiles(semente) → aplica mutações de C
  └─ ENTIDADE: lê os registros de entidade de C no save → instancia

chunk C sai do raio
  ├─ TILE:     EraseTiles(C)                 (mutações continuam no estado)
  └─ ENTIDADE: serializa as entidades de C de volta pro registro → QueueFree
```

Isso responde a pergunta que originou o desenho: **quem carrega a entidade é o streaming, lendo do save, conforme o player anda** — do mesmo jeito que a mutação de tile é reaplicada quando o chunk volta.

### A simetria que faz o desenho fechar

| | tile | entidade |
|---|---|---|
| base determinística | semente do mundo | — (não existe entidade procedural hoje) |
| delta persistido | `ChunkMutationData[]` por chunk | `EntityRecord[]` por chunk |
| ao carregar | pinta + reaplica mutação | instancia a partir do registro |
| ao descarregar | apaga o tile | serializa de volta e libera o node |
| ao entrar peer novo | manda semente + chunks carregados | manda os registros dos chunks carregados |

O que trafega na rede continua sendo mínimo: **coordenada e delta**, nunca o resultado.

### A estratégia: registro eager, materialização lazy

Igual ao que o tile já faz hoje.

| nível | o que é | quando |
|---|---|---|
| **registro** | o delta persistido (`ChunkMutationData`, `EntityRecord`) | **eager** — a dimensão inteira entra no `ImportState`, ao entrar no mundo |
| **materialização** | a célula pintada, o node na árvore | **lazy** — só quando o chunk entra no raio |

`ImportState` carrega o dicionário inteiro da dimensão; `ApplyMutations` só roda dentro do `LoadChunkAsync`. Entidade segue o mesmo: os `EntityRecord` de toda a dimensão entram em memória de uma vez, e o node só é instanciado quando o chunk dele carrega.

No cliente nem o registro vem inteiro: ele recebe os deltas **do chunk** que está chegando, dentro do `LoadChunkReceive`. Quem guarda o dicionário completo é o servidor.

---

## 2. Os três problemas que o desenho resolve

### 2.1 Entidade acumula sem controle

`Player`, `Prop`, `Portal`, `NPC` e `WorldItem` entram na árvore e só saem por evento de jogo (item recolhido, prop quebrado, peer desconectado, mundo desmontado). Não há relação nenhuma entre distância do player e vida do node.

Cem itens dropados e não recolhidos são cem `Area2D` processando para sempre, em qualquer canto do mapa. `RestoreProps` instancia **todos** os props do save de uma vez no carregamento, mesmo os que estão a mil chunks de distância.

### 2.2 `ChunkStreamingManager` faz cinco coisas em 623 linhas

| bloco | linhas | pertence a |
|---|---:|---|
| avaliação de carga/descarga | ~150 | **fica** |
| RPC de chunk | ~50 | **fica** |
| cache de chunk carregado | ~30 | **fica** |
| pintura/apagamento | ~100 | já é delegado ao `ChunkGeneratorSystem` |
| `RecordMutation` / `ApplyMutations` | ~30 | `TerrainLayer` — é ele quem sabe o que é bloco |
| `RecordDiscovered` / `GetDiscoveredTexture` | ~30 | minimapa, responsabilidade separada |
| `ResolveBiome` | ~5 | `BiomeDB`, passa direto |
| `PreloadSpawnAreaAsync` | ~25 | setup de mundo, é do `WorldManager` |
| export/import de estado | ~40 | getter/setter, fica mas encolhe |

### 2.3 `DimensionManager` tem 18 métodos que são o mesmo algoritmo

```
SpawnPlayer / SpawnPlayerReceive / SpawnPlayerRequest ×2
SpawnNpcReceive / SpawnNpcRequest / SpawnTestNPC
SpawnWorldItem / SpawnWorldItemReceive / SpawnWorldItemRequest ×2
FindWorldItem / RemoveWorldItemReceive / RemoveWorldItemRequest
SpawnPropAuthoritative / SpawnPropBroadcast / SpawnProp
RestoreProps / CollectProps
```

Todos fazem **exatamente a mesma coisa**:

1. carrega um `PackedScene`
2. seta id, posição e um `Resource` de payload
3. `AddChild` no parent da dimensão
4. faz `Rpc`/`RpcId` da mesma tupla `(id, dicionário, posição, dimensionId)`

É o mesmo problema que o `PlaceBlockReceive`/`PlacePortalReceive` no `Player` já teve, e que foi resolvido com `UseItemAt` + definição. A solução aqui é a mesma: **uma definição por tipo de entidade, um método genérico, um RPC.**

---

## 3. As peças novas

### 3.1 `TileStreamingManager` (renomeado, ~250 linhas)

`ChunkStreamingManager` → `TileStreamingManager`. O nome passa a dizer a responsabilidade em vez do mecanismo: ele só mexe em célula de tilemap. "Chunk" continua existindo como unidade interna, mas não é o assunto da classe.

Fica com:

- loop de avaliação por distância (raio 2 carrega, raio 4 descarrega, teto por tick)
- `_loaded`, `_state`, `_loadedPeers` por dimensão
- os três RPCs (`SetWorldSeedReceive`, `LoadChunkReceive`, `UnloadChunkReceive`)
- `ExportState` / `ImportState`

Sai tudo o que a seção 2.2 marcou como pertencente a outro lugar.

**Ponto de extensão:** ao carregar/descarregar um chunk, emite para quem se interessa:

```csharp
public event Action<string, Vector2I> ChunkLoaded;    // dimensionId, chunkCoord
public event Action<string, Vector2I> ChunkUnloaded;
```

`EntityStreamingManager` e `MinimapSystem` assinam. O `TileStreamingManager` não conhece nenhum dos dois — mantém a regra de pub/sub do projeto (a peça de baixo notifica, não chama).

### 3.2 `EntityStreamingManager` (novo, ~200 linhas)

Assina `ChunkLoaded`/`ChunkUnloaded` e faz pela entidade o que o tile já faz.

```
EntityStreamingManager : Node
├─ _records : Dictionary<(string dim, Vector2I chunk), List<EntityRecord>>
│     o que o save conhece, carregado ou não
├─ _live : Dictionary<long instanceId, Node>
│     o que está instanciado agora
│
├─ OnChunkLoaded(dimensionId, chunkCoord)
│     └─ para cada record do chunk: DimensionManager.Spawn(record)
│
├─ OnChunkUnloaded(dimensionId, chunkCoord)
│     └─ para cada entidade viva no chunk: serializa em EntityRecord, QueueFree
│
├─ Track(node, record)      // entidade criada em runtime entra na contabilidade
├─ Forget(instanceId)       // item recolhido, prop quebrado: some do save também
│
├─ ExportState(dimensionId) : DimensionEntitySaveData
└─ ImportState(dimensionId, save)
```

**`Player` fica de fora.** Player não é conteúdo de mundo: quem o cria e destrói é o join/leave da sessão, e ele é o *centro* do raio, não conteúdo dele. `Prop`/`Portal`, `WorldItem` e `NPC` entram.

**Diferença entre descarregar e esquecer:**

| | descarregar | esquecer |
|---|---|---|
| gatilho | player se afastou | item recolhido, prop quebrado |
| node | `QueueFree` | `QueueFree` |
| registro no save | **mantido** | **removido** |
| volta quando o chunk volta? | sim | não |

Essa distinção é o coração do desenho — é ela que faz um portal continuar existindo depois de você ir embora e voltar, e um item recolhido não ressuscitar.

### 3.3 `EntityRecord` — o formato único

```csharp
public partial class EntityRecord : Resource
{
    public string TypeId { get; set; }        // "portal", "world_item", "npc_dummy"
    public long InstanceId { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public string DimensionId { get; set; }
    public Godot.Collections.Dictionary Data { get; set; }   // payload específico do tipo
}
```

O mesmo `EntityRecord` serve para **três coisas**, e é isso que elimina os 18 métodos:

- **save**: é o que vai pro `.tres`
- **rede**: é o que o RPC de spawn carrega
- **spawn**: é o que o `DimensionManager` recebe para instanciar

`PropSaveData` passa a ser um `EntityRecord` com `TypeId = "portal"`. `PropSaveData` continua existindo só como classe obsoleta, pelo mesmo motivo de sempre — os `.tres` salvos guardam o caminho do script.

### 3.4 `EntityDefinition` — a definição por tipo

Uma `Resource` por tipo de entidade, no mesmo espírito de `ItemDefinition`:

```csharp
public abstract partial class EntityDefinition : Resource
{
    public abstract string TypeId { get; }
    public abstract PackedScene Scene { get; }

    // Aplica o payload no node recém-instanciado.
    public abstract void Apply(Node2D node, EntityRecord record);

    // Extrai o payload de um node vivo.
    public abstract EntityRecord Capture(Node2D node);
}
```

`PropEntityDefinition`, `WorldItemEntityDefinition`, `NpcEntityDefinition`. Entidade nova = uma definição, **zero linha** no `DimensionManager` e zero RPC novo.

### 3.5 `DimensionManager` — 18 métodos viram 4

```csharp
// Instancia pela definição e coloca no parent da dimensão. Autoritativo.
public Node2D Spawn(EntityRecord record)

// Spawn + replica pra todo mundo (ou pra um peer só, no catch-up).
public void SpawnRequest(EntityRecord record, long targetPeerId = 0)

[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, ...)]
public void SpawnReceive(Godot.Collections.Dictionary recordDict)

// Tira da árvore em todas as pontas.
public void DespawnRequest(long instanceId)
```

Mais `ResolveParent`/`ResolveLayer`/`ShowOnly`/`FindGroundSpawnPosition`, que continuam como estão.

`RestoreProps` e `CollectProps` **somem**: quem restaura é o `EntityStreamingManager` ao carregar o chunk, e quem coleta é o `ExportState` dele.

`SpawnTestNPC` some: vira um `EntityRecord` de `TypeId = "npc_dummy"` gravado no save do mundo novo, como qualquer outra entidade.

### 3.6 `MinimapSystem` (novo, ~50 linhas)

Assina `ChunkLoaded`, varre as células do chunk e pinta a imagem de descoberta. Guarda `_discoveredOverworld`/`_discoveredUpsidedown` e expõe `GetDiscoveredTexture`. É o que sai do `TileStreamingManager`.

---

## 4. Como o save fica

```
user://saves/worlds/<id>/
├─ world.tres              meta, semente, personagens
├─ dimension_overworld.tres
│    ├─ Chunks[] → ChunkEntryData { coord, ChunkStateData { Mutations[] } }
│    └─ Entities[] → EntityRecord[]        ← NOVO, chaveado por chunk
└─ dimension_upsidedown.tres
```

`DimensionSaveData` ganha a lista de entidade ao lado da de mutação. As duas são deltas por chunk; o carregamento das duas é disparado pelo mesmo evento.

### O que já existe hoje

| peça | estado |
|---|---|
| `SaveManager.Register` / `Unregister` / `ClearRegistry` | **implementado** — lista de `Resource`, dispatch por tipo em `SaveAll` |
| evento `Saving` antes de serializar | **implementado** — `SessionManager` assina e sincroniza o estado vivo do player |
| autosave por timer, só no host | **implementado** |
| registro de `WorldSaveData` e `CharacterSaveData` | **implementado** — `SessionManager` registra ao entrar no mundo |
| mutação de tile por chunk | **implementado** |
| props no save | **implementado, mas fora do streaming** — `RestoreProps` carrega tudo de uma vez |
| `WorldItem` no save | **não existe** |
| `NPC` no save | **não existe** |
| entidade chaveada por chunk | **não existe** |
| registro automático por `_EnterTree`/`_ExitTree` | **não existe** |

### O que o registro do `SaveManager` passa a receber

Hoje o registry guarda `WorldSaveData` e `CharacterSaveData`. Passa a guardar também o `DimensionEntitySaveData` de cada dimensão, exportado pelo `EntityStreamingManager` no evento `Saving`:

```
SaveManager.SaveAll()
  ├─ Saving?.Invoke()
  │    ├─ SessionManager.SincronizarPersonagens()   (já existe)
  │    └─ EntityStreamingManager.FlushLiveEntities()  (novo — entidade viva vira record)
  └─ grava cada Resource do registry pelo tipo
```

O `EntityStreamingManager` não grava arquivo: ele atualiza o `Resource` que já está registrado. Mantém a regra **UI → manager → system**, com `SaveStorage` como único ponto de IO.

### Registro automático por sinal da árvore

Ideia já discutida e agora colocada no lugar certo: a entidade não precisa saber do `SaveManager`. Quem registra é o `EntityStreamingManager`, via `Track`/`Forget`, chamados pelo `DimensionManager.Spawn`/`DespawnRequest`. Como todo spawn passa por lá, o registro é automático sem interface nova e sem `_EnterTree` em cada classe.

---

## 5. Utilities e código morto

### 5.1 `CoordinateUtilities` (novo, ~30 linhas)

`WorldToCell` e `CellToChunk` hoje são privados do `ChunkStreamingManager`, e o `EntityStreamingManager` vai precisar dos dois para saber em que chunk uma entidade está.

```csharp
public static class CoordinateUtilities
{
    public static Vector2I WorldToCell(Vector2 globalPosition, int tileSize);
    public static Vector2I CellToChunk(Vector2I cell);
    public static Vector2I WorldToChunk(Vector2 globalPosition, int tileSize);
    public static Vector2 ChunkToWorld(Vector2I chunkCoord, int tileSize);
}
```

### 5.2 Queries de player ficam no `WorldManager`

`WorldManager` já tem `GetLocalPlayer()` e `FindPlayerByPeerId()`. Ganha os dois que o streaming precisa:

```csharp
public List<Player> GetAllPlayers();
public List<Player> GetPlayersInDimension(string dimensionId);
```

Não existe classe de utility de player: o manager já é o dono da árvore de players. `TileStreamingManager` e `EntityStreamingManager` chamam `Game.Managers.WorldManager.Node`.

### 5.3 `FindLocalPlayer` duplicado em 4 telas

Método idêntico em `DeathScreenUI:61`, `HudUI:258`, `FullscreenMapUI:94`, `SkillTreeUI:97`. Some dos quatro; cada tela resolve uma vez no `Initialize` e cacheia — que é a regra de node ref do projeto:

```csharp
_localPlayer = Game.Managers.WorldManager.Node.GetLocalPlayer();
```

### 5.4 Outros resíduos já mapeados

- `SaveManager` tem 3 regions vazias.
- `ApplyMutations` recebe `dimensionId` e não usa.
- O cliente guarda `ResolveState(...)[chunkCoord]` ao receber chunk e nunca lê.
- `SetChunkStreamingEnabled` no `WorldManager` é wrapper de uma atribuição.
- `PortalSaveData` só existe como classe obsoleta (necessária: os `.tres` gravam o caminho do script).
- `SaveStorage.CachedProfile` é estático sem invalidação.

---

## 6. Bugs de streaming que o redesenho precisa corrigir

Levantados na leitura do código; não corrigir junto seria carregar o defeito para a arquitetura nova.

**Chunk já carregado nunca chega num segundo peer.** `missing` filtra por `loaded`, que é global do servidor. Se A carregou o chunk C e depois B caminha até lá, `LoadChunkAsync` não roda, `loadedPeers[C]` continua só com A, e B nunca recebe C. A decisão precisa ser **por peer**: comparar `neededByPeer[coord]` com `loadedPeers[coord]` e enviar para quem falta, mesmo com o chunk já pintado no servidor.

**Catch-up manda o mundo inteiro.** `CatchUpDimension` envia todo chunk carregado das duas dimensões, sem filtrar por distância do spawn do peer. Com o streaming de entidade junto, o custo dobra — precisa filtrar por raio.

**Chunk vazio é gravado à toa.** Medido num save real: `upsidedown.tres` tem **78 chunks gravados para 15 mutações** — 63 entradas existem só para dizer que o chunk foi visitado, a ~325 bytes cada. `ExportState` precisa pular chunk com lista vazia.

**Lista de mutação cresce sem fim.** `chunkState.Mutations` é append-only; quebrar e recolocar o mesmo bloco grava duas entradas para sempre. Compactar por célula na hora do `ExportState`.

---

## 7. Antes e depois

| arquivo | antes | depois | delta |
|---|---:|---:|---|
| `ChunkStreamingManager` → `TileStreamingManager` | 623 | ~250 | −373 |
| `DimensionManager` | 535 | ~330 | −205 (18 métodos de spawn viram 4) |
| `EntityStreamingManager` | — | ~200 | +200 |
| `EntityRecord` + `EntityDefinition` + 3 definições | — | ~150 | +150 |
| `MinimapSystem` | — | ~50 | +50 |
| `CoordinateUtilities` | — | ~30 | +30 |
| `WorldManager` | 218 | ~230 | +12 |
| 4 telas (`FindLocalPlayer`) | — | — | −40 |
| **total** | | | **≈ −176 linhas** |

Menos código, e o que sobra tem uma responsabilidade cada.

---

## 8. Ordem de execução

Cada passo compila e roda sozinho.

1. **`CoordinateUtilities`** — extrai `WorldToCell`/`CellToChunk`, sem mudar comportamento.
2. **`WorldManager`** — adiciona `GetAllPlayers`/`GetPlayersInDimension`; remove `FindLocalPlayer` das 4 telas.
3. **`MinimapSystem`** — extrai `RecordDiscovered`/`GetDiscoveredTexture`; `TileStreamingManager` passa a emitir `ChunkLoaded`.
4. **Rename `ChunkStreamingManager` → `TileStreamingManager`** — e tira `RecordMutation`, `ApplyMutations`, `ResolveBiome`, `PreloadSpawnAreaAsync` para os donos certos.
5. **`EntityRecord` + `EntityDefinition` + as 3 definições** — só os tipos, ninguém usa ainda.
6. **`DimensionManager.Spawn/SpawnRequest/SpawnReceive/DespawnRequest`** — genéricos, convivendo com os antigos.
7. **Migrar os chamadores** para o genérico; apagar os 18 métodos antigos.
8. **`EntityStreamingManager`** — assina `ChunkLoaded`/`ChunkUnloaded`, `Track`/`Forget`, export/import; `RestoreProps`/`CollectProps` somem.
9. **Corrigir os 3 bugs da seção 6.**
10. **Testar** — smoke headless, solo (andar e voltar: prop continua, item recolhido não volta), 2 peers (catch-up, carregar chunk que o outro já tinha), mundo salvo antigo.

Os passos 1–4 são mecânicos e sem risco. O 5–8 é o desenho novo. O 9 é o que faltava desde antes.

---

## 9. Regras que o desenho respeita

Do [managers-architecture-redesign.md](managers-architecture-redesign.md):

- **Alvo existe? É dele.** Mutação de tile é do `TerrainLayer`; por isso `RecordMutation` sai do streaming.
- **Criar é de quem tem o lugar.** Instanciar continua no `DimensionManager`, agora com um método só.
- **Nenhum manager leva nome de tipo de entidade.** `EntityStreamingManager` é sobre streaming, não sobre prop ou item.
- **A peça de baixo notifica.** `TileStreamingManager` emite `ChunkLoaded`; não conhece nem o minimapa nem o streaming de entidade.
- **UI → manager → system.** `SaveStorage` continua sendo o único IO.
