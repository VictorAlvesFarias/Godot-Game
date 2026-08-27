# Plano de implementação: streaming, save e limpeza

Fechado em 2026-08-26. Complementa [arquitetura-streaming.md](arquitetura-streaming.md), que tem o raciocínio; aqui está **o que vai ser escrito**, em ordem, com exemplo de cada peça.

> **Status:** seções 1-4 e 7 implementadas. Ver [status-implementacao.md](status-implementacao.md) para o que mudou de verdade — três estimativas deste plano não se confirmaram na medição.

---

## Índice

| # | implementação | risco | depende de |
|---|---|---|---|
| 1 | `CoordinateUtilities` | nenhum | — |
| 2 | Queries de player no `WorldManager` | nenhum | — |
| 3 | `MinimapSystem` | baixo | 4 |
| 4 | `TileStreamingManager` (rename + enxugar + eventos) | baixo | 1 |
| 5 | Serialização de node (`ApplyTo` + transform) | médio | — |
| 6 | Spawn genérico no `DimensionManager` | médio | 5 |
| 7 | ~~Save em JSON~~ | — | **feito** |
| 8 | `EntityRecord` + `EntityStreamingManager` | alto | 4, 5, 6 |
| 9 | Correções de streaming | alto | 8 |

---

## 1. `CoordinateUtilities`

**Faz:** converte posição do mundo → célula → chunk, e volta.

**Por quê:** hoje `WorldToCell` e `CellToChunk` são privados do `ChunkStreamingManager`. O `EntityStreamingManager` precisa dos dois para saber em que chunk uma entidade está, e não pode alcançá-los.

```csharp
public static class CoordinateUtilities
{
    public static Vector2I WorldToCell(Vector2 globalPosition, int tileSize)
    {
        return new Vector2I(
            Mathf.FloorToInt(globalPosition.X / tileSize),
            Mathf.FloorToInt(globalPosition.Y / tileSize));
    }

    public static Vector2I CellToChunk(Vector2I cell)
    {
        return new Vector2I(
            Mathf.FloorToInt(cell.X / (float)ChunkStreamingConstants.CHUNK_SIZE),
            Mathf.FloorToInt(cell.Y / (float)ChunkStreamingConstants.CHUNK_SIZE));
    }

    public static Vector2I WorldToChunk(Vector2 globalPosition, int tileSize)
        => CellToChunk(WorldToCell(globalPosition, tileSize));
}
```

**Substitui:** dois métodos privados. Nenhuma mudança de comportamento — é recorte puro.

---

## 2. Queries de player no `WorldManager`

**Faz:** centraliza as buscas de player que hoje estão espalhadas.

**Por quê:** o streaming precisa de "todos os players desta dimensão", e o `WorldManager` já é o dono da árvore de players.

> **Correção:** este plano dizia "`FindLocalPlayer` duplicado idêntico em 4 telas, ~40 linhas". Falso — veio de grep de assinatura, não de corpo. Só `DeathScreenUI` e `SkillTreeUI` eram wrapper de uma linha; `HudUI` re-assina eventos do player e `FullscreenMapUI` faz wiring do `MapView`. E o lookup já passava pelo `WorldManager` nos quatro. A duplicação era o nome do método, não a lógica.

```csharp
// WorldManager — dois métodos novos ao lado dos dois que já existem.
public List<Player> GetAllPlayers()
    => GetTree().GetNodesInGroup("players").OfType<Player>().ToList();

public List<Player> GetPlayersInDimension(string dimensionId)
    => GetAllPlayers()
        .Where(p => p.GetParent() == Dimensions.ResolveParent(dimensionId))
        .ToList();
```

Nas telas, o método some e vira campo cacheado — que é a regra de node ref do projeto:

```csharp
// Antes, em 4 arquivos:
public void FindLocalPlayer()
{
    var players = GetTree().GetNodesInGroup("players");
    // ... 4 linhas iguais ...
}

// Depois, no Initialize de cada tela:
_localPlayer = Game.Managers.WorldManager.Node.GetLocalPlayer();
```

**Substitui:** 2 wrappers triviais. Nenhuma classe nova — nada de `PlayerQueryUtilities`.

---

## 3. `MinimapSystem`

**Faz:** guarda a imagem de descoberta por dimensão e responde a textura pro minimapa.

**Por quê:** hoje isso mora no `ChunkStreamingManager` (`RecordDiscovered`, `GetDiscoveredTexture`, `_discoveredOverworld`, `_discoveredUpsidedown`), que não tem nada com minimapa.

```csharp
public class MinimapSystem
{
    private readonly Dictionary<string, DiscoveredMapImage> _discovered = new();

    // Assina o evento do TileStreamingManager. Ele não sabe que o minimapa existe.
    public void OnChunkLoaded(string dimensionId, Vector2I chunkCoord)
    {
        var layer = Dimensions.ResolveLayer(dimensionId);
        var image = Resolve(dimensionId);

        // varre as células do chunk e marca as preenchidas
    }

    public Texture2D GetTexture(string dimensionId, out Vector2I origin) { ... }
}
```

**Substitui:** ~60 linhas e 2 campos do streaming.

---

## 4. `TileStreamingManager`

**Faz:** decide qual chunk pintar e apagar, e avisa quem se interessa.

**Por quê:** `ChunkStreamingManager` tem 623 linhas e cinco assuntos. O nome também mente — "chunk" é o mecanismo, não a responsabilidade: ele só mexe em célula de tilemap.

**O que sai:**

| sai | vai para |
|---|---|
| ~~`RecordMutation`~~ | **fica.** `ApplyChunkMutation` (a parte que sabe o que é bloco) já está no `TerrainLayer`; `RecordMutation` só faz append na lista do chunk, que é estado do streaming e é o que vai pro save |
| `RecordDiscovered` / `GetDiscoveredTexture` | `MinimapSystem` |
| `ResolveBiome` | `BiomeDB`, chamado direto |
| `PreloadSpawnAreaAsync` | `WorldManager` (é setup de mundo) |
| `WorldToCell` / `CellToChunk` | `CoordinateUtilities` |

**O que entra** — os dois eventos que sustentam o resto do plano:

```csharp
public event Action<string, Vector2I> ChunkLoaded;     // dimensionId, chunkCoord
public event Action<string, Vector2I> ChunkUnloaded;
```

```csharp
private async Task LoadChunkAsync(...)
{
    loaded.Add(chunkCoord);

    await _generator.PaintTilesAsync(...);

    ApplyMutations(...);

    ChunkLoaded?.Invoke(dimensionId, chunkCoord);   // <- minimapa e entidades reagem
}
```

`MinimapSystem` e `EntityStreamingManager` assinam. O `TileStreamingManager` **não conhece nenhum dos dois** — é a regra de pub/sub do projeto: a peça de baixo notifica, não chama.

**Resultado medido:** 623 → **600** linhas. A estimativa original de ~250 estava errada — saiu o que era de fora (minimapa, conversões: ~90 linhas), e o que restou é trabalho legítimo de streaming:

```
162  Load/Unload     126  Evaluation     61  RPC
 42  Persistência     42  Godot impl     33  Catch-up
```

---

## 5. Serialização de node

**Faz:** permite serializar e restaurar um `Node` do mesmo jeito que já se faz com `Resource`.

**Por quê:** o estado da entidade vai morar no próprio node (ver seção 10). O parser hoje só aceita `Resource` e só sabe **criar**, nunca **popular** algo que já existe.

### 5.1 O parser aceita `GodotObject`

```csharp
public static Dictionary ToDictionary(GodotObject source)          // era Resource
public static void ApplyTo(GodotObject target, Dictionary dict)    // novo
public static Resource ToResource(Dictionary dict, Type fallback)  // continua, pra Resource puro
```

`ApplyTo` é o irmão do `ToResource`: mesma varredura, mesma conversão de `Variant`, só que popula em vez de instanciar:

```csharp
public static void ApplyTo(GodotObject target, Dictionary dict)
{
    if (target == null || dict == null)
    {
        return;
    }

    foreach (var property in GetFields(target.GetType()))
    {
        if (!dict.ContainsKey(property.Name))
        {
            continue;
        }

        property.SetValue(target, FromVariant(dict[property.Name], property.PropertyType));
    }
}
```

`GetFields(target.GetType())` usa o tipo **em runtime**. Medido com o `Portal.tscn` real: instanciar como `Node2D` devolve `Jogo25D.Portals.Portal`, e a reflexão enxerga as propriedades herdadas de `Prop`.

### 5.2 O transform é gravado sempre, por fora

`Position` é declarada em `Node2D` — **não dá pra anotar propriedade de classe base que não é sua**. Tentar sombreá-la com `new` grava um valor que não é a posição real.

Então o manager escreve um bloco fixo, e o atributo cuida do resto:

```csharp
private static void WriteTransform(Dictionary record, Node2D node)
{
    record["Position"] = new Dictionary { { "x", node.Position.X }, { "y", node.Position.Y } };
    record["Rotation"] = node.Rotation;
    record["Scale"]    = new Dictionary { { "x", node.Scale.X }, { "y", node.Scale.Y } };
}
```

Sem isso, um prop rotacionado perde a rotação sem erro nenhum.

### 5.3 A entidade declara o que é dela

```csharp
public partial class Prop : Area2D
{
    [GodotDictionaryField] public string PropId { get; set; } = "";
}

public partial class Portal : Prop
{
    [GodotDictionaryField] public string TargetDimension { get; set; } = "";
}
```

`Portal : Prop`, então a reflexão traz `PropId` junto. Herança de graça, sem hierarquia paralela.

---

## 6. Spawn genérico no `DimensionManager`

**Faz:** instancia qualquer entidade no lugar certo, e replica.

**Por quê:** hoje são **18 métodos** fazendo o mesmo algoritmo:

```
SpawnPlayer / SpawnPlayerReceive / SpawnPlayerRequest ×2
SpawnNpcReceive / SpawnNpcRequest / SpawnTestNPC
SpawnWorldItem / SpawnWorldItemReceive / SpawnWorldItemRequest ×2
FindWorldItem / RemoveWorldItemReceive / RemoveWorldItemRequest
SpawnPropAuthoritative / SpawnPropBroadcast / SpawnProp
RestoreProps / CollectProps
```

Todos: carregam `PackedScene`, setam id/posição/payload, `AddChild` no parent, e mandam a mesma tupla por RPC. É o mesmo problema que `PlaceBlockReceive`/`PlacePortalReceive` já teve no `Player`, e que o `UseItemAt` resolveu.

```csharp
// Instancia pela cena que o próprio record aponta, e coloca no parent da dimensão.
public Node2D Spawn(Dictionary record)
{
    var parent = ResolveParent(record["DimensionId"].AsString());

    if (parent == null)
    {
        return null;
    }

    var node = GD.Load<PackedScene>(record["ScenePath"].AsString()).Instantiate<Node2D>();

    GodotDictionaryParser.ApplyTo(node, record);

    ApplyTransform(node, record);

    parent.AddChild(node);

    return node;
}

// Spawn + replica pra todos, ou pra um peer só (catch-up).
public void SpawnRequest(Dictionary record, long targetPeerId = 0) { ... }

[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
public void SpawnReceive(Dictionary record) => Spawn(record);

public void DespawnRequest(long instanceId) { ... }
```

**Exemplo — colocar um portal hoje e depois:**

```csharp
// Antes: PortalItemDefinition precisa de um método específico no manager.
dimensions.SpawnPropAuthoritative("portal", position, dimensionId);

// Depois: descreve o que quer, e pronto.
dimensions.SpawnRequest(new Dictionary
{
    { "ScenePath", "res://Scenes/World/Props/Portal.tscn" },
    { "DimensionId", dimensionId },
    { "Position", ToDict(position) },
    { "PropId", "portal" },
});
```

Entidade nova = uma cena + as propriedades marcadas. **Zero linha no manager, zero RPC novo.**

**Por que ainda sobra 1 RPC:** RPC do Godot exige o node existir nos dois lados, no mesmo caminho. Node que ainda não existe não recebe RPC — criação não pode ser self-service.

> **Pendente investigar:** `MultiplayerSpawner` nativo faz exatamente isso (aponta parent + cenas permitidas; `AddChild` no servidor replica sozinho). O projeto tem **0 ocorrências** dele. Se cobrir o caso, esse último RPC também some.

**Substitui:** 18 métodos → 4.

---

## 7. Save em JSON — **implementado**

**Faz:** grava e lê o save como JSON, pelo mesmo serializador que já trafega por RPC.

```json
{
	"$type": "dimension",
	"Chunks": [{
		"$type": "chunk_entry",
		"ChunkCoordX": -2,
		"State": { "$type": "chunk_state", "Mutations": [
			{ "$type": "chunk_mutation", "Type": "break", "Position": { "x": -7.0, "y": 42.0 } }
		]}
	}]
}
```

`$type` é id curto declarado pela classe (`[SaveType("prop")]`), resolvido por um mapa montado uma vez por reflexão. Sem factory, sem switch.

**Medições que fixaram o formato:**

| ponto | resultado |
|---|---|
| `Json.Stringify` com `Vector2` cru | vira string `"(12.5, -3.0)"`; `AsVector2()` na volta dá `Zero` — perda silenciosa |
| número na volta | sempre `float`; o parser converte pelo tipo declarado, então `int`/`long` sobrevivem |
| `long` > 2^53 | perde precisão |

Por isso `Vector2` é serializado como `{"x":…, "y":…}` **no parser**, e vale a regra: nada de inteiro acima de 2^53 em campo de save.

Verificado em runtime: seed, enum, bool, int, arrays aninhadas, `PlayerData` aninhado e os dois `Vector2` — round-trip idêntico.

---

## 8. `EntityStreamingManager`

**Faz:** pela entidade, o que o `TileStreamingManager` faz pelo tile.

```
EntityStreamingManager : Node
├─ _records : Dictionary<(string dim, Vector2I chunk), List<Dictionary>>
│     o que o save conhece — materializado ou não. Mesmo formato do disco.
├─ _live    : Dictionary<long instanceId, Node2D>
│     o que está na árvore agora
│
├─ OnChunkLoaded    → instancia os records do chunk
├─ OnChunkUnloaded  → serializa e libera os nodes do chunk
├─ Register / Unregister   ← chamados pelo _EnterTree/_ExitTree da entidade
├─ Forget(instanceId)      ← item recolhido, prop quebrado
├─ BeginTeardown()         ← DespawnWorld avisa antes de liberar o World
└─ ExportState / ImportState
```

### 8.0 `EntityRecord` — o wrapper tipado

O record é `Godot.Collections.Dictionary`, mas **ninguém lê campo específico de entidade dele**:

| quem toca no record | lê o quê |
|---|---|
| `OnChunkLoaded` | `InstanceId`, `ScenePath`, `DimensionId` |
| `OnChunkUnloaded` | `InstanceId` |
| `ImportState` | `Position` |
| `Register` / `Forget` | `InstanceId` |
| save | passa o dicionário inteiro adiante |

`PropId`, `TargetDimension` e afins vão **dict → node** pelo `ApplyTo` e **node → dict** pelo `ToDictionary`, sem ninguém no meio inspecionando. O dicionário é payload opaco fora dessas quatro chaves fixas.

Então basta tipar as quatro:

```csharp
// Wrapper fino sobre o dicionário. Não entra na herança de ninguém, então não
// traz hierarquia paralela nem cast - só tira o stringly-typed do código nosso.
public readonly struct EntityRecord
{
    private readonly Dictionary _dict;

    public EntityRecord(Dictionary dict) => _dict = dict;

    public long InstanceId    => _dict["InstanceId"].AsInt64();
    public string ScenePath   => _dict["ScenePath"].AsString();
    public string DimensionId => _dict["DimensionId"].AsString();
    public Vector2 Position   => ReadVector(_dict, "Position");

    public Dictionary Raw => _dict;
}
```

Com isso o `EntityStreamingManager` trabalha tipado (`record.InstanceId`, `record.ScenePath`) e o `Raw` só aparece na fronteira: `ApplyTo(node, record.Raw)` e o que vai pro arquivo.

### 8.1 Entrar no mundo

```csharp
var save = SaveStorage.LoadDimensionState(worldId, "upsidedown");

TileStreaming.ImportState("upsidedown", save);
EntityStreaming.ImportState("upsidedown", save);   // só indexa por chunk; nenhum node
```

### 8.2 Chunk entra no raio

```csharp
private void OnChunkLoaded(string dimensionId, Vector2I chunkCoord)
{
    foreach (var record in Records(dimensionId, chunkCoord))
    {
        if (_live.ContainsKey(record.InstanceId))
        {
            continue;
        }

        Game.Managers.DimensionManager.Node.Spawn(record);   // AddChild dispara o _EnterTree
    }
}
```

### 8.3 A entidade se registra sozinha

```csharp
// Prop.cs — herdado por Portal e qualquer prop futuro.
public override void _EnterTree() => EntityStreaming.Register(this);
public override void _ExitTree()  => EntityStreaming.Unregister(this);
```

E o `Register` precisa saber se aquele node **veio do save** ou **nasceu agora** — senão o portal recém-carregado entra de novo na lista e duplica. A identidade viaja **no próprio node**, não em estado ambiente:

```csharp
public void Register(Node2D node)
{
    if (!GodotDictionaryParser.HasSerializableFields(node))
    {
        return;   // efeito, label, hitbox: streaming não tem nada com isso
    }

    if (node is IHasInstanceId identified && identified.InstanceId != 0)
    {
        _live[identified.InstanceId] = node;   // restaurado: o record já existe

        return;
    }

    // Nasceu agora: cria o record a partir do próprio node.
    var record = GodotDictionaryParser.ToDictionary(node);

    record["InstanceId"] = InstanceIdGenerator.NextInstanceId();
    record["ScenePath"] = node.SceneFilePath;      // o Godot preenche sozinho
    record["DimensionId"] = Dimensions.ResolveDimensionIdOf(node);

    WriteTransform(record, node);

    Records(...).Add(record);
    _live[record["InstanceId"].AsInt64()] = node;
}
```

> Uma versão anterior deste plano usava um campo `_restoring` setado antes do `AddChild`. **Não usar**: quebra no instante em que algum `AddChild` virar `CallDeferred`, e o bug é silencioso — registro duplicado no save.

### 8.4 Chunk sai do raio

```csharp
private void OnChunkUnloaded(string dimensionId, Vector2I chunkCoord)
{
    foreach (var record in Records(dimensionId, chunkCoord))
    {
        if (!_live.TryGetValue(record.InstanceId, out var node))
        {
            continue;
        }

        // O estado mora no node: captura ANTES de liberar, ou some.
        Merge(record.Raw, GodotDictionaryParser.ToDictionary(node));
        WriteTransform(record.Raw, node);

        _live.Remove(record.InstanceId);

        node.QueueFree();
    }
}
```

### 8.5 Descarregar ≠ esquecer

| | descarregar | esquecer |
|---|---|---|
| gatilho | player se afastou | item recolhido, prop quebrado |
| node | `QueueFree` | `QueueFree` |
| record | **fica** | **sai** |
| volta com o chunk? | sim | não |

É essa distinção que faz o portal sobreviver a você ir e voltar, e o item recolhido não ressuscitar.

### 8.6 `_ExitTree` só faz membership

Medido em Godot 4.6 headless, sonda em `_exit_tree`:

| como o node sai | `IsQueuedForDeletion()` | pai queued |
|---|---|---|
| `RemoveChild` | `false` | `false` |
| `Reparent` | `false` | `false` |
| `QueueFree` no próprio node | **`true`** | `false` |
| `QueueFree` no pai direto | `false` | `true` |
| `QueueFree` no **avô** (= `DespawnWorld`) | `false` | `false` |

Descarregar, trocar de dimensão e desmontar o mundo são **indistinguíveis** de dentro do callback. E no auto-`QueueFree` o `PREDELETE` roda **antes** do `_ExitTree` — serializar ali é serializar objeto em teardown.

Por isso: `_ExitTree` remove de `_live` e nada mais. Captura acontece em `OnChunkUnloaded`/`Forget`, e o teardown é anunciado por `BeginTeardown()`.

### 8.7 `Player` fica de fora

Player é conteúdo de **sessão**: quem o cria e destrói é o join/leave, e ele é o *centro* do raio, não conteúdo dele.

Como `NPC : Player`, isso não é expressável por tipo — o código já contorna em dois lugares (`p.PeerId > 0`, `if (node is NPC) continue`). Com o discriminador sendo o **atributo**, participa quem declara, não quem herda. Os dois contornos somem.

---

## 9. Correções de streaming

Quatro defeitos. Os dois primeiros mudam estrutura de dados — precisam ser resolvidos **junto** com a seção 8, não depois.

### 9.1 Entidade que se move não tem dono de chunk

`WorldItem` é `CharacterBody2D`: cai, escorrega, muda de chunk. Registrado em A e fisicamente em B, ele é liberado quando A descarrega — mesmo com player ao lado em B — e reaparece na posição velha quando A recarrega.

Decisão necessária: reavaliar a chave de chunk quando a entidade se move, ou separar entidade estática de móvel. `Prop` é fixo e não sofre; item e NPC sofrem.

### 9.2 Peer não está modelado para entidade

Pro tile existe `loadedPeers` — quem recebeu qual chunk. Pra entidade, nada. E o bug do tile se repete: **chunk já carregado nunca chega no segundo peer**, porque `missing` filtra pelo `loaded` global do servidor:

```csharp
var missing = needed.Where(c => !loaded.Contains(c))
```

Se A carregou o chunk C e depois B caminha até lá, `LoadChunkAsync` não roda, `loadedPeers[C]` continua só com A, e **B nunca recebe C**. Tile faltando é feio; entidade faltando é dessincronização de estado — um peer vê o portal, o outro não.

Correção: decidir por peer, comparando `neededByPeer[coord]` com `loadedPeers[coord]`.

### 9.3 Chunk vazio é gravado à toa

Medido num save real: `upsidedown.json` tem **78 chunks para 15 mutações** — 63 entradas só dizem "visitei aqui", a ~325 bytes cada. `ExportState` precisa pular chunk com lista vazia.

Isso importa mais do que parece: sem a correção, o arquivo cresce com **área explorada** em vez de crescer com o que o jogador fez.

### 9.4 Catch-up manda o mundo inteiro

`CatchUpDimension` envia todo chunk carregado das duas dimensões, sem filtrar por distância do spawn do peer. Com entidade junto, o custo dobra.

---

## 10. Ter ou não ter `Resource`: a comparação

A pergunta central do desenho. As duas versões, lado a lado.

### 10.1 Com `Resource` (`Data` separado)

Cada entidade tem uma classe de dado espelho:

```csharp
[SaveType("portal")]
public partial class PortalData : PropSaveData
{
    [GodotDictionaryField] public string TargetDimension { get; set; } = "";
}

public partial class Portal : Prop
{
    public PortalData Data { get; set; }
}
```

**Serializar:**

```csharp
portal.Data.Position = portal.Position;          // sincronização manual, campo a campo
var record = GodotDictionaryParser.ToDictionary(portal.Data);
record["ScenePath"] = portal.SceneFilePath;
```

**Desserializar:**

```csharp
// 1. reconstrói o DADO — Activator.CreateInstance, tipo vem do "$type"
var data = (PortalData)GodotDictionaryParser.ToResource(record);

// 2. reconstrói o NODE — PackedScene, tipo vem do "ScenePath"
var node = GD.Load<PackedScene>(record["ScenePath"].AsString()).Instantiate<Node2D>();

// 3. injeta, com cast e com uma interface só pra isso existir
((IEntityNode)node).Data = data;

// 4. aplica o que é do node
node.Position = data.Position;
```

Quatro passos, dois objetos, um cast, uma interface, e um `$type` que só existe pra reconstruir o dado — enquanto o `ScenePath` no mesmo arquivo já identifica a entidade.

### 10.2 Sem `Resource` (propriedades no node)

```csharp
public partial class Portal : Prop
{
    [GodotDictionaryField] public string TargetDimension { get; set; } = "";
}
```

**Serializar:**

```csharp
var record = GodotDictionaryParser.ToDictionary(portal);   // reflexão sobre o node
record["ScenePath"] = portal.SceneFilePath;
WriteTransform(record, portal);
```

**Desserializar:**

```csharp
// 1. reconstrói o NODE
var node = GD.Load<PackedScene>(record["ScenePath"].AsString()).Instantiate<Node2D>();

// 2. popula
GodotDictionaryParser.ApplyTo(node, record);
ApplyTransform(node, record);
```

Dois passos, um objeto, sem cast, sem interface, sem `$type`.

### 10.3 A diferença nas duas desserializações

| | com `Resource` | sem `Resource` |
|---|---|---|
| passos até o node pronto | 4 | 2 |
| objetos criados | 2 (`PortalData` + `Portal`) | 1 |
| caminhos de reconstrução | 2 (`Activator` + `PackedScene`) | 1 (`PackedScene`) |
| como o tipo é identificado | `$type` **e** `ScenePath` (redundantes) | `ScenePath` |
| cast na injeção | sim, explícito e unchecked em cada classe (10.4) | não |
| interface só pra injetar | sim (`IEntityNode`) | não |
| classes por entidade | 2 | 1 |
| campo novo na classe | 2 lugares, ou sync manual | 1 lugar, entra sozinho |
| herança | hierarquia paralela (`PortalData : PropSaveData`) | de graça |
| entidade descarregada | objeto tipado (`data.PropId`) | dicionário (`record["PropId"]`) |

### 10.4 O cast que o `Resource` obriga

Propriedade em C# não tem covariância. Para a interface declarar `EntitySaveData Data { get; set; }` e o `Portal` ter `PortalData Data`, os tipos teriam que ser idênticos — não são. Então **toda** entidade carrega esta implementação explícita:

```csharp
public partial class Portal : Prop
{
    public PortalData Data { get; set; }

    EntitySaveData IEntityNode.Data
    {
        get => Data;
        set => Data = (PortalData)value;   // cast sem verificação
    }
}
```

O cast é unchecked: subtipo errado vira `InvalidCastException` em runtime, no meio do carregamento de chunk.

E as saídas estão todas fechadas:

| tentativa | por que não |
|---|---|
| `IEntityNode<T>` genérica | o manager não guarda lista heterogênea; precisa de base não-genérica junto, e o cast migra pro manager |
| `Data` sempre do tipo base | some 1 cast, aparecem N: `((PortalData)Data).TargetDimension` em cada uso |
| node genérico `Prop<TData>` | script C# do Godot tem que ser classe não-genérica; `[Export]` genérico já falhou aqui com `GD0102` |

O ponto de fundo: `PortalData : PropSaveData` espelhando `Portal : Prop` é uma correspondência que **o compilador não consegue expressar**. Por isso ela reaparece como cast manual em cada classe.

Sem `Resource`, some inteiro: `ApplyTo(node, record)` trabalha por reflexão sobre `node.GetType()`, que já é o tipo concreto.

### 10.5 O argumento que parecia decidir, e não decide

"Com `Resource`, o dado sobrevive ao node de graça — não precisa serializar antes de liberar."

**Falso.** A posição mora no node (`node.Position`), não no `Resource`. Liberar o node sem copiar perde a posição nos **dois** desenhos. A diferença é só a forma:

| | com `Resource` | sem `Resource` |
|---|---|---|
| antes de liberar | `data.Position = node.Position;`<br>`data.Cooldown = node.Cooldown;`<br>…campo a campo | `Merge(record, ToDictionary(node));` |
| campo novo | lembrar de sincronizar | entra sozinho |

Esquecer uma linha de sincronização é bug silencioso de save. Sem `Resource`, não há linha pra esquecer.

### 10.6 Decisão: sem `Data`

**Fechado em 2026-08-26.** Conteúdo de mundo não tem `Resource` espelho — as propriedades moram no node.

Antes de decidir, o que o `Data` de fato compraria:

| benefício | vale aqui? |
|---|---|
| compilador pega typo de campo (`data.TargetDimension` × `record["TargetDimensio"]`) | **sim** — resolvido pelo `EntityRecord` (8.0) no código que existe |
| schema do save num arquivo só, revisável | **sim** — é o único que se perde de verdade |
| consulta tipada a entidade descarregada | hoje nada faz isso; plausível no futuro |
| testabilidade sem árvore de cena | não — o projeto não tem teste |
| migração de versão de save | não — a política é "quebrou, cria outro" |

E o que **não** compra, apesar da intuição: "o dado sobrevive ao node de graça". A posição mora no node nos dois desenhos; sem capturar antes de liberar, some igual (10.5).

O que ele cobra, medido ao longo do desenho:

- hierarquia paralela `PortalData : PropSaveData` espelhando `Portal : Prop`
- cast explícito e unchecked em cada entidade, porque propriedade em C# não tem covariância (10.4)
- sincronização manual campo a campo antes de liberar
- `$type` redundante com o `ScenePath` que já está no mesmo arquivo
- 4 passos de desserialização em vez de 2, e 2 objetos em vez de 1

**Mitigação do que se perde:** o schema deixa de estar num arquivo próprio, então ele fica agrupado numa region na própria entidade:

```csharp
public partial class Portal : Prop
{
    #region Save

    [GodotDictionaryField] public string TargetDimension { get; set; } = "";
    [GodotDictionaryField] public float Cooldown { get; set; }

    #endregion
}
```

Assim "o que é persistido de um portal" continua sendo uma pergunta com resposta num lugar só — e mudança de formato de save continua saltando aos olhos em revisão.

**A regra geral fica valendo:**

> `Resource` separado quando o dado tem vida própria fora do node. Propriedades no node quando não tem.

Por isso `Player` mantém `PlayerData`: ele é copiado pra `CharacterSaveData`, guardado em `_peerCharacters`, mandado por RPC no join e gravado em arquivo separado. Lá o requisito existe; nas entidades de mundo, não.

## 11. Avaliação e opinião

### O esqueleto está certo

Seed + delta com materialização lazy é modelo provado, e já funciona no tile. Estender pra entidade é generalização, não aposta. `Descarregar ≠ esquecer` é a distinção correta e ficou limpa. Reconstruir node por cena e `Resource` por `$type` não é escolha estética — é o único caminho que o Godot permite.

E o formato único pra disco, RAM e rede é ganho real: menos ponto de conversão, menos bug.

### O que me preocupa de verdade

**1. Entidade móvel (9.1) é o buraco mais sério, e é de desenho, não de código.** Item dropado cai — não é caso raro, é o caso comum. Resolver depois significa mudar a estrutura de chave, ou seja, retrabalho no `EntityStreamingManager` inteiro.

**2. Peer não modelado (9.2) tem o mesmo problema.** O bug já existe no tile e vai ser herdado. Em entidade, ele deixa de ser cosmético: dois peers com visões diferentes do mundo, ambos podendo interagir.

**3. O escopo cresceu muito além do que originou a conversa.** Começou em "o chunk loader está grande demais para uma função simples". Terminou em streaming de entidade + formato de save + API de spawn + registro por árvore + rename. Cada peça se justifica; juntas são uma reescrita da camada de mundo.

**4. E o que motivou tudo ainda não foi feito.** Você reclamou de método duplicado, morto e criado à toa. `FindLocalPlayer` continua nas 4 telas, as 3 regions vazias continuam, e as seções 1–3 — mecânicas, sem risco, uma hora de trabalho — estão esperando enquanto projetamos as seções 8–9.

### O que eu faria

1. **Seções 1–3.** Sem risco, e atacam exatamente a queixa original.
2. **Seção 4** (rename + eventos). Mecânica, e destrava as outras.
3. **Decidir 9.1 e 9.2 no papel** antes de escrever o `EntityStreamingManager`. As duas mudam estrutura de dados.
4. **Investigar `MultiplayerSpawner`.** É a pergunta mais barata com o maior efeito: pode apagar o RPC da seção 6 e simplificar a 8.
5. **Seções 5–6.** Spawn genérico entrega valor sozinho — 18 métodos viram 4 — e **não depende do streaming de entidade**.
6. **Seção 8 por último.**

### O que eu cortaria, se fosse pra cortar

A seção 8 é a mais cara e a menos urgente. O problema que ela resolve — cem `Area2D` vivos pra sempre — é real, mas não morde num mundo pequeno. O `RestoreProps` carregando tudo de uma vez é o pedaço que incomoda hoje, e ele dá pra corrigir sozinho: filtrar por distância no carregamento, sem streaming completo.

Se o objetivo é reduzir complexidade agora, **as seções 1–6 entregam quase todo o ganho com uma fração do risco**. A 8 é a que traz arquitetura nova, e é a única que eu adiaria sem culpa.
