# Streaming: tile e entidade

Como o mundo é carregado, descarregado e persistido. Estado do código em 2026-08-27 — tudo aqui foi verificado em runtime, não é plano.

---

## 1. Duas correntes, uma unidade

O mundo tem duas categorias de conteúdo, e as duas seguem o mesmo princípio: **o arquivo guarda o delta, nunca o resultado.**

| | tile | entidade |
|---|---|---|
| base determinística | a semente do mundo | — |
| delta persistido | `ChunkMutationData[]` por chunk | o nó serializado |
| ao carregar | pinta pela semente, reaplica mutação | pendura o nó na árvore |
| ao descarregar | apaga as células | tira o nó da árvore |
| quem decide | `TileStreamingManager` | `WorldStreaming` |
| onde vive | `Managers.tscn` | raiz do `World.tscn` |

O que trafega na rede e vai pro disco é **coordenada e delta**. O terreno é regerado nas duas pontas pela mesma semente.

---

## 2. Streaming de tile

`TileStreamingManager`, em `Managers.tscn`. Só mexe em célula de tilemap — **não instancia nada**.

### Carregar e descarregar é pintar e apagar

```csharp
PaintTilesAsync   →  SetCell(cell, sourceId, atlasCoord)   // 200 células por frame
EraseTilesAsync   →  SetCell(cell, -1)
```

As layers `Base` e `Compose` já existem no `.tscn` de cada dimensão. Nunca são criadas em runtime.

### O loop

`_Process` só age no lado autoritativo (servidor ou solo), a cada `EVALUATE_INTERVAL_SECONDS`.

| constante | valor | efeito |
|---|---:|---|
| `CHUNK_SIZE` | 32 | células por lado |
| `LOAD_RADIUS_CHUNKS` | 2 | quadrado 5×5 ao redor de cada player |
| `UNLOAD_RADIUS_CHUNKS` | 4 | quadrado 9×9; só descarrega fora disso |
| `MAX_CHUNK_LOADS_PER_TICK` | 2 | teto por avaliação |
| `EVALUATE_INTERVAL_SECONDS` | 0.75 | período |

Os dois raios diferentes são histerese: sem isso um player na fronteira faria o mesmo chunk pintar e apagar em ciclo.

> **Cuidado:** `CHUNK_SIZE` e `REFERENCE_TILE_SIZE` são 32, mas o tile real do projeto é **16**. `Dimensions.TileSize` lê do `TileSet`. Assumir 32 dá coordenada errada — já aconteceu num teste.

### A decisão é por peer

```csharp
var missing = needed.Where(c => !loaded.Contains(c)) ...          // pinta no servidor
SendPendingChunksToPeers(...)                                     // envia a quem falta
```

O segundo passo existe porque `loaded` é global do servidor: sem ele, um chunk pintado por causa do player A **nunca chegava** em quem chegou depois.

### Os eventos

```csharp
public event Action<string, Vector2I> ChunkLoaded;
public event Action<string, Vector2I> ChunkUnloaded;
```

Disparados nos quatro pontos — carga e descarga, servidor e cliente. O `MinimapSystem` assina; o `TileStreamingManager` não conhece quem assina.

---

## 3. Streaming de entidade

`WorldStreaming`, script na **raiz do `World.tscn`**.

### Por que na raiz do World, e não em Managers

Porque o tempo de vida bate. Manager em `Managers.tscn` nunca morre; o mundo nasce e morre várias vezes por sessão. Toda essa diferença seria código — `ResetState`, `BeginTeardown`, flag `_tearingDown`. Com o script na raiz, **os três não existem**: o objeto morre com o mundo, e quem está desmontando é ele.

### Não há registro: a árvore é o índice

```csharp
private IEnumerable<Node2D> Streamed()
{
    return Descendants(this).Where(GodotDictionaryParser.HasSerializableFields);
}
```

O critério de participação é **ter campo marcado com `[GodotDictionaryField]`**. Hoje isso pega exatamente duas classes de nó: `Prop` e `WorldItem`. Todas as outras 26 classes com campo marcado são `Resource`, não nó.

`Player` fica de fora sozinho — os campos marcados dele vivem no `PlayerData`, que é `Resource`. Hitbox, efeito e indicador também não têm. **Nenhuma exceção precisa ser escrita.**

> **Regra que isso cria:** marcar campo num nó significa as duas coisas — "trafega por RPC" **e** "é salvo pelo mundo". Marcar um campo numa hitbox só para RPC a colocaria no save.

### O único dicionário

```csharp
private readonly Dictionary<long, Node2D> _unloaded = new();
```

Quem está fora da árvore. Existe por um motivo mecânico: a árvore não devolve o que não está nela, e **nó fora da árvore não tem dono** — se ninguém segurar a referência, vaza.

Mais `_peers` (só para `PeerOnly`) e `_saveByDimension` (o objeto do registry do `SaveManager`).

### As três operações do engine já são as três operações do domínio

```
AddChild      →  carregou
RemoveChild   →  descarregou   — continua no save, volta quando o player chegar perto
QueueFree     →  esqueceu      — sai do save, não volta
```

Não existem métodos `Unload`/`Forget` na API. E a árvore avisa das três:

```csharp
GetTree().NodeAdded   += OnNodeAdded;     // entrou: sai do pool
GetTree().NodeRemoved += OnNodeRemoved;   // saiu: IsQueuedForDeletion diz se guarda ou esquece
```

Isso faz `RemoveChild = descarregar` valer **venha de onde vier** — reparent, código de gameplay, editor. Sem os sinais, um `RemoveChild` feito fora do `WorldStreaming` sumia do save e vazava.

### Medido no Godot 4.6

| como o nó sai da árvore | `IsQueuedForDeletion()` |
|---|---|
| `RemoveChild` | `false` |
| `Reparent` | `false` |
| **`QueueFree` nele mesmo** | **`true`** |
| `QueueFree` no pai direto | `false` |
| `QueueFree` no avô (`DespawnWorld`) | `false` |

E no auto-`QueueFree` o `PREDELETE` roda **antes** do `_ExitTree` — por isso nada de captura de estado acontece nesses callbacks.

> Nó descarregado não recebe `QueueFree` de ninguém: fora da árvore, nada o alcança — sem colisão, sem sinal, sem player por perto. "Esquecer" só acontece com o nó pendurado, que é o caso do item recolhido e do prop quebrado.

### A política é atributo de classe

```csharp
[Unload(UnloadMode.Global)]
public partial class Prop : Area2D { ... }
```

Na classe, não na instância: um portal é um portal. É **herdado**, então `Portal : Prop` pega sem repetir. Lido por reflexão, com cache **por tipo** — limitado pelo número de classes, nunca cresce com o jogo rodando.

| modo | servidor | peer | simulação |
|---|---|---|---|
| `Never` | mantém | mantém | continua |
| `Global` | tira | tira | **para** |
| `PeerOnly` | mantém | peer longe perde | continua |

`PeerOnly` é para o que precisa rodar sem ninguém olhando — máquina que produz, plantação que cresce. Usa `DespawnForPeer(peerId, id)`, que tira o nó de **um** peer só.

Não há troca em runtime. Se aparecer o caso, decide-se então — hoje seria gancho sem uso.

---

## 4. Identidade

O nome do nó **é** a identidade: `E{instanceId}`.

Precisa ser determinístico porque RPC do Godot resolve por **caminho**: se o nó tiver nome diferente em cada peer, RPC nele não chega. Por isso não existe campo `InstanceId` — seria duplicar o que o nome já diz.

```csharp
public static string EntityNameOf(long id) => $"E{id}";
public static long InstanceIdOf(Node node);   // parse do nome, 0 se não for entidade
```

> **`InstanceIdGenerator` mascara em 50 bits, e não pode aumentar.** O save é JSON, e JSON só tem `double`: inteiro acima de 2^53 volta da leitura com valor diferente. Medido: `165877808694513759` virou `…760`, e o nó perdia a identidade no reload. A máscara antiga era 59 bits.

---

## 5. O caminho do save

```
WorldManager.ImportDimension(save, dimensionId)
  ├─ SaveStorage.LoadDimensionState  → lê dimension.json
  ├─ state.WorldId / state.DimensionId
  ├─ TileStreamingManager.ImportState  → indexa as mutações
  ├─ WorldStreaming.ImportState        → instancia as entidades, DETACHADAS
  └─ SaveManager.Register(state)       → uma entrada no registry por arquivo
```

Nenhum nó entra na árvore no `ImportState`. A varredura pendura os que estiverem perto de algum player.

```
SaveManager.SaveAll()
  ├─ Saving?.Invoke()
  │    ├─ TileStreamingManager  escreve Chunks   no state
  │    ├─ WorldStreaming        escreve Entities no state
  │    └─ SessionManager        sincroniza personagens
  └─ percorre o registry e grava pelo tipo
```

**O `SaveManager` não conhece nenhum dos dois streamings.** O registry significa uma coisa só: *`Resource` que tem arquivo próprio*.

| tipo no registry | arquivo |
|---|---|
| `WorldSaveData` | `worlds/{id}/world.json` |
| `CharacterSaveData` | `characters/{id}.json` |
| `DimensionSaveData` | `worlds/{id}/{dimensionId}.json` |

### Salvar entidade é o merge de duas fontes

```csharp
foreach (var node in Streamed())        Append(node);   // pendurados
foreach (var node in _unloaded.Values)  Append(node);   // guardados
```

Quem foi `QueueFree` não aparece em nenhuma das duas — **é assim que sai do save**, sem flag e sem aviso.

### O formato

```json
{
	"$type": "dimension",
	"WorldId": "5eabc3d8",
	"DimensionId": "upsidedown",
	"Chunks": [
		{ "$type": "chunk_entry", "ChunkCoordX": -2, "ChunkCoordY": 5,
		  "State": { "$type": "chunk_state", "Mutations": [
		      { "$type": "chunk_mutation", "Type": "break", "Position": { "x": -7.0, "y": 42.0 } }
		  ]}}
	],
	"Entities": [
		{ "ScenePath": "res://Scenes/World/Props/Portal.tscn",
		  "InstanceId": 221823356363824,
		  "DimensionId": "upsidedown",
		  "Position": { "x": 864.0, "y": -128.0 },
		  "PropId": "portal" }
	]
}
```

O registro de entidade é o **nó serializado**: os campos marcados dele, mais o que ele não consegue declarar sozinho — a cena de onde veio, a identidade, a dimensão e a posição, que é do `Node2D`.

`$type` só aparece em `Resource`. Nó não leva: quem o reconstrói é a cena (`PackedScene.Instantiate`), não o `Activator`.

### Regras do formato, medidas

| ponto | consequência |
|---|---|
| `Json.Stringify` com `Vector2` cru | vira a string `"(12.5, -3.0)"`; `AsVector2()` na volta dá `Zero`, **sem erro** |
| número na volta | sempre `float`; o parser converte pelo tipo declarado, então `int`/`long` sobrevivem |
| `long` acima de 2^53 | perde precisão |

Por isso `Vector2` é serializado como `{"x":…, "y":…}` **no parser**, e vale a regra: **nada de inteiro acima de 2^53 em campo de save.**

---

## 6. Multiplayer

**O cliente é burro.** Não guarda registro, não avalia distância, não decide nada:

```csharp
if (!Enabled || !IsServerAuthoritative())
{
    return;
}
```

Ele recebe ordem de spawn e despawn e obedece. E nunca lê o arquivo de save: `ImportState` só roda no caminho de host/solo.

| operação | quem manda |
|---|---|
| pintar/apagar chunk | servidor, via `LoadChunkReceive` / `UnloadChunkReceive` |
| criar/remover entidade | servidor, via `SpawnReceive` / `DespawnReceive` |
| tirar entidade de um peer só | servidor, via `DespawnForPeer` |

### Catch-up de quem entra

Dentro do `FinishPeerJoin`:

```
PreloadSpawnAreaAsync            → servidor pinta a área de spawn
ClearLayersReceive               → limpa o tilemap do cliente
TileStreamingManager.CatchUpPeer → semente + chunks dentro do raio
WorldStreaming.CatchUpPeer       → entidades na árvore dentro do raio
```

Os dois filtram por `UNLOAD_RADIUS_CHUNKS` ao redor da posição de spawn do peer.

---

## 7. Mapa de arquivos

| arquivo | papel |
|---|---|
| `TileStreamingManager.cs` | decisão de carga/descarga de tile, RPCs, catch-up, estado de chunk |
| `ChunkGeneratorSystem.cs` | `PaintTilesAsync` / `EraseTilesAsync` — a escrita de célula |
| `MinimapSystem.cs` | mapa de descoberta por dimensão; assina `ChunkLoaded` |
| `WorldStreaming.cs` | varredura, pool, política, catch-up e persistência de entidade |
| `UnloadAttribute.cs` / `UnloadMode.cs` | a política declarada na classe |
| `DimensionManager.cs` | `Spawn` / `Build` / `SpawnRequest` / `Despawn*`, e onde é cada dimensão |
| `TerrainLayer.cs` | autotile, `ApplyChunkMutation`, origem das mutações |
| `SaveManager.cs` | registry por tipo; não conhece streaming |
| `SaveStorage.cs` | IO de JSON |
| `CoordinateUtilities.cs` | mundo → célula → chunk, e distância de Chebyshev |

---

## 8. O que não está testado

**Nada foi exercitado com dois peers.** O `PeerOnly` só existe nesse cenário e nunca rodou. Os RPCs de personagem mudaram de nó duas vezes, o de bloco foi pro `TerrainLayer`, o spawn de entidade é novo e o `$type` mudou de formato nas duas pontas.

Checklist mínima:

- [ ] quebrar/colocar bloco nas duas pontas, conferindo autotile na borda entre biomas
- [ ] colocar e quebrar portal; trocar de dimensão pelo portal
- [ ] entrar com 2 peers: player remoto, NPC, item dropado
- [ ] andar para longe e voltar: prop continua, item recolhido não volta
- [ ] `PeerOnly`: um peer perto e outro longe da mesma entidade
- [ ] entrar e sair de mundo 2× seguidas
