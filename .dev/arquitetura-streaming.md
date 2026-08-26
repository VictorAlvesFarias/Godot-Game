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
| delta persistido | `ChunkMutationData[]` por chunk | `EntitySaveData[]` por chunk |
| ao carregar | pinta + reaplica mutação | instancia a partir do registro |
| ao descarregar | apaga o tile | serializa de volta e libera o node |
| ao entrar peer novo | manda semente + chunks carregados | manda os registros dos chunks carregados |

O que trafega na rede continua sendo mínimo: **coordenada e delta**, nunca o resultado.

### A estratégia: registro eager, materialização lazy

Igual ao que o tile já faz hoje.

| nível | o que é | quando |
|---|---|---|
| **registro** | o delta persistido (`ChunkMutationData`, `EntitySaveData`) | **eager** — a dimensão inteira entra no `ImportState`, ao entrar no mundo |
| **materialização** | a célula pintada, o node na árvore | **lazy** — só quando o chunk entra no raio |

`ImportState` carrega o dicionário inteiro da dimensão; `ApplyMutations` só roda dentro do `LoadChunkAsync`. Entidade segue o mesmo: os `EntitySaveData` de toda a dimensão entram em memória de uma vez, e o node só é instanciado quando o chunk dele carrega.

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

### 3.2 `EntityStreamingManager` (novo, ~180 linhas)

Assina `ChunkLoaded`/`ChunkUnloaded` e faz pela entidade o que o tile já faz. É também o `EntityRegistry` onde a `WorldEntity` se registra sozinha.

```
EntityStreamingManager : Node
├─ _records : Dictionary<(string dim, Vector2I chunk), List<EntitySaveData>>
│     o que o save conhece, materializado ou não
├─ _live : Dictionary<long instanceId, WorldEntity>
│     o que está na árvore agora
│
├─ Register(entity)     ← chamado pelo _EnterTree da própria entidade
├─ Unregister(entity)   ← chamado pelo _ExitTree da própria entidade
│
├─ OnChunkLoaded(dimensionId, chunkCoord)
│     └─ para cada record do chunk: data.Scene.Instantiate() + Restore(data)
│
├─ OnChunkUnloaded(dimensionId, chunkCoord)
│     └─ para cada entidade viva no chunk: entity.Unload()
│
├─ BeginTeardown()      ← DespawnWorld avisa; a partir daí ignora _ExitTree
│
├─ ExportState(dimensionId) : DimensionEntitySaveData
└─ ImportState(dimensionId, save)
```

### 3.3 `WorldEntity` — a base que se registra sozinha

Em vez de um manager que sabe criar cada tipo, **cada entidade se registra ao entrar na árvore**. O `Prop` já faz metade disso hoje: tem os próprios RPCs de quebra e um `virtual ToSave()`. `WorldEntity` generaliza.

```
WorldEntity : Node2D
├─ _EnterTree()  → EntityRegistry.Register(this)      automático
├─ _ExitTree()   → EntityRegistry.Unregister(this)    automático, e SÓ isso
│
├─ virtual EntitySaveData Capture()      estado -> resource
├─ virtual void Restore(EntitySaveData)  resource -> estado
│
├─ Unload()   captura, mantém o registro no save, sai da árvore
├─ Forget()   remove do save, QueueFree
│
└─ RPCs próprios de comportamento (quebrar, interagir) — como o Prop já tem
```

`Prop`/`Portal`, `WorldItem` e `NPC` passam a herdar daqui. **`Player` não**: ele é sessão, não conteúdo de mundo — quem o cria e destrói é o join/leave, e ele é o *centro* do raio, não conteúdo dele.

#### A regra dura: `_ExitTree` só faz membership

Medido em projeto Godot isolado (4.6), com sonda em `_exit_tree`:

| como o node sai | `IsQueuedForDeletion()` nele | pai queued | `PREDELETE` |
|---|---|---|---|
| `RemoveChild` | `false` | `false` | depois |
| `Reparent` | `false` | `false` | — (segue vivo) |
| **`QueueFree` no próprio node** | **`true`** | `false` | **antes do `_ExitTree`** |
| `QueueFree` no pai direto | `false` | `true` | depois |
| `QueueFree` no **avô** (é o caso do `DespawnWorld`) | `false` | `false` | depois |
| `Free` no avô | `false` | `false` | depois |

Duas consequências:

1. **`IsQueuedForDeletion()` só isola "eu fui liberado diretamente".** Descarregar, trocar de dimensão e desmontar o mundo caem todos em `false`/`false` — indistinguíveis. Olhar o pai não resolve: só acusa quando o pai **direto** foi o liberado, e no `DespawnWorld` o `QueueFree` é no `World`, dois ou mais níveis acima.
2. **No auto-`QueueFree` o `PREDELETE` roda antes do `_ExitTree`.** Serializar ali é serializar um objeto já em teardown.

Por isso: **`_ExitTree` tira do registro e nada mais.** Nunca captura estado, nunca decide se salva. A intenção é declarada nos métodos explícitos, antes de sair da árvore.

| | `Unload()` | `Forget()` |
|---|---|---|
| gatilho | chunk saiu do raio | item recolhido, prop quebrado |
| captura | `Capture()` antes de sair | não precisa |
| registro no save | **mantido** | **removido** |
| node | sai da árvore | `QueueFree` |
| volta quando o chunk volta? | sim | não |

E o teardown do mundo, que é indistinguível de fora, é resolvido por fora: o `WorldManager.DespawnWorld` avisa o registry que vai desmontar, e o registry ignora os `_ExitTree` que chegarem depois disso.

### 3.4 `EntitySaveData` e o formato do save: JSON

**Implementado em 2026-08-26.** O save deixou de ser `.tres` do Godot e passou a ser JSON puro, escrito pelo `GodotDictionaryParser` — o mesmo serializador que já trafega por RPC. Um formato só para disco e rede.

```json
{
	"$type": "dimension",
	"Chunks": [
		{
			"$type": "chunk_entry",
			"ChunkCoordX": -2,
			"State": {
				"$type": "chunk_state",
				"Mutations": [
					{ "$type": "chunk_mutation", "Type": "break", "Position": { "x": -7.0, "y": 42.0 } }
				]
			}
		}
	]
}
```

**O tipo vem no arquivo, não há factory.** `$type` é um id curto e estável declarado pela própria classe:

```csharp
[SaveType("prop")]
public partial class PropSaveData : Resource { ... }
```

O parser monta o mapa `id -> Type` uma vez, por reflexão, e instancia com `Activator.CreateInstance`. Sem lista central, sem switch. Classe sem `[SaveType]` cai no `FullName`.

Por que id próprio e não o nome do tipo: o `$type` antigo gravava `AssemblyQualifiedName`, **com versão do assembly**. Para RPC tanto faz — os dois lados são o mesmo build. Para save é mina: bumpar versão derruba mundo antigo. Com id estável, renomear classe, mover arquivo ou trocar namespace não quebra nada — que era exatamente a dor deixada pelo `PortalSaveData`.

#### Restrições do formato, medidas

Testado em Godot 4.6 headless:

| ponto | resultado |
|---|---|
| `Json.Stringify` com `Vector2` cru | vira a string `"(12.5, -3.0)"`, e `AsVector2()` na volta dá `Vector2.Zero` — **perda silenciosa** |
| número na volta | sempre `float`; o `FromVariant` converte pelo tipo declarado, então `int`/`long` sobrevivem |
| `long` acima de 2^53 | perde precisão (`…806` volta `…800`) |
| `JSON.from_native` | preserva tudo, mas o arquivo ganha tags de tipo e deixa de ser JSON legível |

Decisões que saíram disso:

1. **`Vector2` é serializado como `{"x":…, "y":…}` pelo parser.** O C# continua com `Vector2` nas propriedades; só a fronteira converte. Nada de partir campo em `X`/`Y` no código de gameplay.
2. **Nada de inteiro acima de 2^53 em campo de save.** Hoje está seguro: `WorldSeed` é `uint32` (`(uint)GD.Randi()`) e timestamps são ~1.7e9.
3. **Tipo não suportado estoura**, em vez de gravar lixo em silêncio.

#### Entidade

O `EntitySaveData` do streaming segue o mesmo formato — mais o caminho da cena, já que `PackedScene` não sobrevive a JSON:

```csharp
[SaveType("entity")]
public partial class EntitySaveData : Resource
{
    [Export, GodotDictionaryField] public string ScenePath { get; set; }
    [Export, GodotDictionaryField] public Vector2 Position { get; set; }
    [Export, GodotDictionaryField] public string DimensionId { get; set; }
    [Export, GodotDictionaryField] public long InstanceId { get; set; }
}
```

E o carregamento continua sendo uma linha genérica: `GD.Load<PackedScene>(data.ScenePath).Instantiate()`.

### 3.5 `DimensionManager` — de 18 métodos de spawn a 1 RPC

Com auto-registro e tipo no resource, sobra do manager só o que é mesmo dele: **saber onde é o lugar**.

```
DimensionManager
├─ ResolveParent / ResolveLayer / ResolveBaseLayer / ShowOnly
├─ FindGroundSpawnPosition
└─ SpawnReceive(EntitySaveData)     ← o único RPC que sobra
```

`RestoreProps`, `CollectProps`, `SpawnTestNPC` e os 18 métodos de spawn somem. Quem restaura é o `EntityStreamingManager` ao carregar o chunk; quem coleta é o `ExportState` dele.

**Por que ainda sobra um RPC:** o RPC do Godot exige que o node já exista nos dois lados, no mesmo caminho. Um node que ainda não existe não pode receber RPC — então **criação não pode ser self-service**, mesmo com auto-registro. Registro, save, quebra e interação podem; criação, não.

**Questão em aberto:** o `MultiplayerSpawner` nativo resolve exatamente isso — aponta pra um parent, lista as cenas spawnáveis, e um `AddChild` no servidor replica sozinho. O projeto não usa nenhum (`MultiplayerSpawner`/`MultiplayerSynchronizer`: 0 ocorrências). Se adotado, o último RPC também some e o auto-registro cobre o ciclo inteiro. **Precisa de investigação antes de decidir.**

### 3.6 `MinimapSystem` (novo, ~50 linhas)

Assina `ChunkLoaded`, varre as células do chunk e pinta a imagem de descoberta. Guarda `_discoveredOverworld`/`_discoveredUpsidedown` e expõe `GetDiscoveredTexture`. É o que sai do `TileStreamingManager`.

---

## 4. Como o save fica

```
user://saves/worlds/<id>/
├─ world.json              meta, semente, personagens
├─ overworld.json
│    ├─ Chunks[] → ChunkEntryData { coord, ChunkStateData { Mutations[] } }
│    └─ Entities[] → EntitySaveData[]        ← NOVO, chaveado por chunk
└─ upsidedown.json
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
| save em JSON com `$type` estável | **implementado 2026-08-26** |
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

A entidade não conhece o `SaveManager`. Ela só se anuncia:

```
WorldEntity._EnterTree()  → EntityStreamingManager.Register(this)
WorldEntity._ExitTree()   → EntityStreamingManager.Unregister(this)
```

Não há interface nova (`ISavable` e afins) nem passagem obrigatória por um método de spawn: quem entra na árvore está registrado, venha de onde vier — do streaming, de um RPC, ou de código de gameplay.

O `EntityStreamingManager` é quem fala com o `SaveManager`, registrando o `DimensionEntitySaveData` de cada dimensão. Cadeia preservada: **entidade → manager → system**.

**O que o `_ExitTree` não pode fazer** (ver a medição em 3.3): capturar estado ou decidir se salva. Ele só remove da lista de vivos. Captura acontece em `Unload()`/`Forget()`, antes de sair da árvore; e o teardown do mundo é anunciado por `BeginTeardown()`, porque de dentro do callback ele é indistinguível de um descarregamento normal.

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
| `DimensionManager` | 535 | ~310 | −225 (18 métodos de spawn viram 1 RPC) |
| `EntityStreamingManager` | — | ~180 | +180 |
| `WorldEntity` + `EntitySaveData` | — | ~120 | +120 |
| `MinimapSystem` | — | ~50 | +50 |
| `CoordinateUtilities` | — | ~30 | +30 |
| `WorldManager` | 218 | ~230 | +12 |
| 4 telas (`FindLocalPlayer`) | — | — | −40 |
| **total** | | | **≈ −176 linhas** |

Menos código, e o que sobra tem uma responsabilidade cada.

---

## 8. Ordem de execução

Cada passo compila e roda sozinho.

> **Feito em 2026-08-26:** migração do save para JSON (`SaveStorage`, `GodotDictionaryParser`, `[SaveType]`), e remoção da última chamada de UI dentro do `SessionManager` (`CompleteLocalCreation`), que estava quebrando o build.

1. **`CoordinateUtilities`** — extrai `WorldToCell`/`CellToChunk`, sem mudar comportamento.
2. **`WorldManager`** — adiciona `GetAllPlayers`/`GetPlayersInDimension`; remove `FindLocalPlayer` das 4 telas.
3. **`MinimapSystem`** — extrai `RecordDiscovered`/`GetDiscoveredTexture`; `TileStreamingManager` passa a emitir `ChunkLoaded`.
4. **Rename `ChunkStreamingManager` → `TileStreamingManager`** — e tira `RecordMutation`, `ApplyMutations`, `ResolveBiome`, `PreloadSpawnAreaAsync` para os donos certos.
5. **`WorldEntity` + `EntitySaveData`** — a base com `_EnterTree`/`_ExitTree`, `Capture`/`Restore`, `Unload`/`Forget`. `Prop` passa a herdar; nada mais muda ainda.
6. **`DimensionManager.SpawnReceive(EntitySaveData)`** — o RPC genérico, convivendo com os antigos.
7. **Migrar os chamadores** para o genérico; apagar os 18 métodos antigos.
8. **`EntityStreamingManager`** — assina `ChunkLoaded`/`ChunkUnloaded`, recebe o auto-registro, export/import; `RestoreProps`/`CollectProps` somem.
9. **Investigar `MultiplayerSpawner`** — se cobrir o caso, o RPC do passo 6 some.
10. **Corrigir os bugs da seção 6.**
11. **Testar** — smoke headless, solo (andar e voltar: prop continua, item recolhido não volta), 2 peers (catch-up, carregar chunk que o outro já tinha), mundo salvo antigo.

Os passos 1–4 são mecânicos e sem risco. O 5–8 é o desenho novo. O 10 é o que faltava desde antes.

---

## 9. Regras que o desenho respeita

Do [managers-architecture-redesign.md](managers-architecture-redesign.md):

- **Alvo existe? É dele.** Mutação de tile é do `TerrainLayer`; por isso `RecordMutation` sai do streaming.
- **Criar é de quem tem o lugar.** Instanciar continua no `DimensionManager`, agora com um método só.
- **Nenhum manager leva nome de tipo de entidade.** `EntityStreamingManager` é sobre streaming, não sobre prop ou item.
- **A peça de baixo notifica.** `TileStreamingManager` emite `ChunkLoaded`; não conhece nem o minimapa nem o streaming de entidade.
- **UI → manager → system.** `SaveStorage` continua sendo o único IO.
