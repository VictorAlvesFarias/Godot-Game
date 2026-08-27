# Streaming de mundo: tile, entidade e spawn genérico

Plano de arquitetura fechado em 2026-08-25. Substitui o rascunho anterior (`arquitetura-entity-lifecycle.md`).
**Nenhum código foi alterado ainda — isto é o desenho acordado.**

O passo a passo do que será escrito está em [plano-implementacao.md](plano-implementacao.md), e o que já foi feito em [status-implementacao.md](status-implementacao.md).

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
| delta persistido | `ChunkMutationData[]` por chunk | um dicionário por entidade, por chunk |
| ao carregar | pinta + reaplica mutação | instancia a partir do registro |
| ao descarregar | apaga o tile | serializa de volta e libera o node |
| ao entrar peer novo | manda semente + chunks carregados | manda os registros dos chunks carregados |

O que trafega na rede continua sendo mínimo: **coordenada e delta**, nunca o resultado.

### A estratégia: registro eager, materialização lazy

Igual ao que o tile já faz hoje.

| nível | o que é | quando |
|---|---|---|
| **registro** | o delta persistido (mutação de tile, dicionário de entidade) | **eager** — a dimensão inteira entra no `ImportState`, ao entrar no mundo |
| **materialização** | a célula pintada, o node na árvore | **lazy** — só quando o chunk entra no raio |

`ImportState` carrega o dicionário inteiro da dimensão; `ApplyMutations` só roda dentro do `LoadChunkAsync`. Entidade segue o mesmo: os dicionários de toda a dimensão entram em memória de uma vez, e o node só é instanciado quando o chunk dele carrega.

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

Assina `ChunkLoaded`/`ChunkUnloaded` e faz pela entidade o que o tile já faz. É também o registro onde as entidades se anunciam sozinhas.

```
EntityStreamingManager : Node
├─ _records : Dictionary<(string dim, Vector2I chunk), List<Dictionary>>
│     o que o save conhece, materializado ou não - mesmo formato do disco
├─ _live : Dictionary<long instanceId, Node2D>
│     o que está na árvore agora
│
├─ Register(entity)     ← chamado pelo _EnterTree da própria entidade
├─ Unregister(entity)   ← chamado pelo _ExitTree da própria entidade
│
├─ OnChunkLoaded(dimensionId, chunkCoord)
│     └─ para cada record do chunk: Instantiate(ScenePath) + ApplyTo(node, record)
│
├─ OnChunkUnloaded(dimensionId, chunkCoord)
│     └─ para cada entidade viva no chunk: entity.Unload()
│
├─ BeginTeardown()      ← DespawnWorld avisa; a partir daí ignora _ExitTree
│
├─ ExportState(dimensionId) → lista de dicionários
└─ ImportState(dimensionId, save)
```

### 3.3 O estado mora no node, não num `Data` paralelo

**Decidido em 2026-08-26.** A entidade não tem um `Resource` espelho. Ela marca as próprias propriedades:

```csharp
[SaveType("portal")]
public partial class Portal : Prop
{
    [GodotDictionaryField] public string TargetDimension { get; set; } = "";
    [GodotDictionaryField] public float Cooldown { get; set; }
}

public partial class Prop : Area2D
{
    [GodotDictionaryField] public string PropId { get; set; } = "";
    [GodotDictionaryField] public Vector2 Position { get; set; }   // herdada de Node2D
}
```

#### Por que não um `Data` separado

Porque `Data` cria **duas hierarquias paralelas**:

```
Portal : Prop : Area2D          PortalData : PropSaveData : Resource
```

Toda mudança na de node teria que ser espelhada na de dado. É a mesma doença dos 18 métodos de spawn, em outro eixo. Com as propriedades no node, a reflexão já traz as marcadas da classe base junto — herança de graça, uma hierarquia só.

E a sincronização deixa de ser manual. Capturar o estado do node antes de liberá-lo é necessário nos **dois** desenhos — a posição mora no node, não no `Resource`, então com `Data` você também teria que copiar. A diferença é a forma:

| | com `Data` | sem `Data` |
|---|---|---|
| antes de liberar | `data.Position = node.Position;`<br>`data.Cooldown = node.Cooldown;`<br>…campo a campo | `Merge(record, ToDictionary(node));` |
| campo novo na classe | precisa lembrar de sincronizar | entra sozinho, pelo atributo |

Esquecer uma linha de sincronização é um bug silencioso de save. Sem `Data`, não há linha para esquecer.

#### Consequência: o registro em memória é o próprio dicionário

Descarregar existe pra liberar o node — mas se o estado mora nele, liberar destrói o estado. Então no unload a entidade é serializada e o que fica é o dicionário:

```csharp
private readonly Dictionary<(string dim, Vector2I chunk), List<Godot.Collections.Dictionary>> _records = new();
private readonly Dictionary<long, Node2D> _live = new();
```

O registro em memória e o formato em disco passam a ser **a mesma coisa**. O custo é que entidade descarregada fica stringly-typed (`dict["PropId"]`); como praticamente nada consulta entidade descarregada, é barato.

#### Consequência: `$type` não é necessário pra entidade

`Activator.CreateInstance` não reconstrói node — node é cena com filhos. A reconstrução é `GD.Load<PackedScene>(caminho).Instantiate()`, e **a cena já carrega o script**. Para node, o `ScenePath` *é* o tipo.

O caminho não precisa ser escrito à mão: `node.SceneFilePath` vem preenchido pelo Godot em qualquer node instanciado de um `PackedScene` (medido: `'user://alvo.tscn'` na instância, `''` num `new()`).

`$type` continua indispensável para `Resource` puro — meta do mundo, personagem, mutação de tile.

#### A exceção: `Player`

`Player.Data` continua existindo. Ele é copiado pra `CharacterSaveData`, guardado em `_peerCharacters`, mandado por RPC no join e salvo separado do node — o dado precisa sobreviver ao node de propósito.

Não é inconsistência, é a linha que já estava traçada: **conteúdo de mundo põe o estado no node; conteúdo de sessão mantém `Resource` próprio.**

#### O que muda no parser

O `GetFields` já usa reflexão sobre `Type` qualquer, então é pouco:

```csharp
public static Dictionary ToDictionary(GodotObject source)         // era Resource
public static void ApplyTo(GodotObject target, Dictionary dict)   // novo: popula em vez de criar
public static Resource ToResource(Dictionary dict, Type fallback) // continua, pra Resource puro
```

#### Quem participa do streaming

Sem `Data`, o discriminador é o atributo: entidade que declara `[GodotDictionaryField]` e está sob um parent de dimensão entra; o resto (efeito, label, hitbox) não.

Isso resolve o `NPC : Player` de graça. Hoje NPC **é** um Player por herança, então "Player fica de fora" não é expressável por tipo — e o código contorna em dois lugares (`p.PeerId > 0` no `EvaluateCore`, `if (node is NPC) continue` no `FinishPeerJoin`). Participa quem **declara**, não quem herda.

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

Por isso: **`_ExitTree` tira do registro e nada mais.** Nunca captura estado, nunca decide se salva. A intenção é declarada por quem age — `OnChunkUnloaded` ou `Forget` —, antes de sair da árvore.

| | `Unload()` | `Forget()` |
|---|---|---|
| gatilho | chunk saiu do raio | item recolhido, prop quebrado |
| captura | serializa o node pro dicionário antes de sair | não precisa |
| registro no save | **mantido** | **removido** |
| node | sai da árvore | `QueueFree` |
| volta quando o chunk volta? | sim | não |

E o teardown do mundo, que é indistinguível de fora, é resolvido por fora: o `WorldManager.DespawnWorld` chama `BeginTeardown()` antes de liberar o `World`, e o registry ignora os `_ExitTree` que chegarem depois disso.

### 3.4 O formato do save: JSON

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
// Game/Features/World/Save/Managers/Resources/PropSaveData.cs
[SaveType("prop")]
public partial class PropSaveData : Resource
{
    [Export, GodotDictionaryField] public string PropId { get; set; } = "portal";
    [Export, GodotDictionaryField] public float PositionX { get; set; }
    ...
}
```

#### O código que instancia o Resource

Está em `GodotDictionaryParser`, e é este — implementado e rodando:

```csharp
// Le o "$type" do proprio dicionario e devolve o Resource ja no tipo certo.
// Nao existe switch nem lista de tipos em lugar nenhum.
public static Resource ToResource(Dictionary dict, Type fallbackType = null)
{
    if (dict == null || dict.Count == 0)
    {
        return null;
    }

    var type = ResolveType(dict, fallbackType);

    if (type == null)
    {
        return null;
    }

    var resource = (Resource)Activator.CreateInstance(type);

    foreach (var property in GetFields(type))
    {
        if (!dict.ContainsKey(property.Name))
        {
            continue;
        }

        property.SetValue(resource, FromVariant(dict[property.Name], property.PropertyType));
    }

    return resource;
}

private static Type ResolveType(Dictionary dict, Type fallbackType)
{
    if (dict.TryGetValue("$type", out var typeNameVariant))
    {
        var typeName = typeNameVariant.AsString();

        if (!string.IsNullOrEmpty(typeName))
        {
            // 1. id estavel do [SaveType]
            if (TypeById.TryGetValue(typeName, out var mapped))
            {
                return mapped;
            }

            // 2. fallback: FullName de quem nao anotou
            var resolved = Type.GetType(typeName);

            if (resolved != null)
            {
                return resolved;
            }
        }
    }

    // 3. ultimo recurso: o tipo que o chamador esperava
    return fallbackType;
}
```

E o mapa `id -> Type`, montado uma vez por reflexão — é isto que substitui a factory:

```csharp
private static void EnsureTypeMap()
{
    if (_typeById != null)
    {
        return;
    }

    _typeById = new Dictionary<string, Type>();
    _idByType = new Dictionary<Type, string>();

    foreach (var type in typeof(GodotDictionaryParser).Assembly.GetTypes())
    {
        var attribute = type.GetCustomAttribute<SaveTypeAttribute>();

        if (attribute == null || string.IsNullOrEmpty(attribute.Id))
        {
            continue;
        }

        if (_typeById.TryGetValue(attribute.Id, out var conflito))
        {
            GD.PushError($"[GodotDictionaryParser] id de save duplicado {attribute.Id}: {conflito} e {type}");

            continue;
        }

        _typeById[attribute.Id] = type;
        _idByType[type] = attribute.Id;
    }
}
```

Classe nova com `[SaveType("x")]` entra no mapa sozinha, no próximo boot. Ninguém precisa registrar nada em lugar nenhum.

#### E o código que instancia a classe (o node)

Node **não** passa por aí. `Activator.CreateInstance` cria um objeto C# nu; node é cena com filhos — sprite, colisão, tudo. Quem reconstrói node é o Godot, pela cena:

```csharp
// EntityStreamingManager, quando o chunk entra no raio.
var node = GD.Load<PackedScene>(record["ScenePath"].AsString()).Instantiate<Node2D>();

// O node ja existe; aqui so populamos as propriedades marcadas.
GodotDictionaryParser.ApplyTo(node, record);

parent.AddChild(node);   // dispara _EnterTree -> Register
```

`ApplyTo` é o irmão do `ToResource`: mesma varredura de `[GodotDictionaryField]`, mesma conversão de `Variant`, só que **popula um objeto existente em vez de criar**:

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

Os dois caminhos convivem por motivo, não por acidente:

| | reconstruído por | tipo vem de |
|---|---|---|
| `Resource` (mundo, personagem, mutação) | `Activator.CreateInstance` | `$type` |
| node (portal, item, npc) | `PackedScene.Instantiate` | `ScenePath` |

O passo a passo completo, com o JSON de exemplo, está em 3.5.

#### Por que id próprio e não o nome do tipo

O `$type` antigo gravava `AssemblyQualifiedName`, **com versão do assembly**:

```
Jogo25D.…PropSaveData, Game, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
```

Para RPC tanto faz — os dois lados são o mesmo build. Para save é mina: bumpar versão derruba mundo antigo. Com id estável, renomear classe, mover arquivo ou trocar namespace não quebra nada — que era exatamente a dor deixada pelo `PortalSaveData`.

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

Entidade não usa `Resource` de save (ver 3.3): as propriedades marcadas moram no próprio node, e o registro é o dicionário. Os únicos campos que o streaming acrescenta por fora são os que existem sem o node:

| campo | de onde vem |
|---|---|
| `ScenePath` | `node.SceneFilePath`, preenchido pelo Godot |
| `InstanceId` | gerado no primeiro registro |
| `DimensionId` | o parent em que o node está |

O resto do objeto JSON é o que a classe declarou com `[GodotDictionaryField]`.

### 3.5 Do JSON ao node registrado — exemplo completo

Um portal em `(864, -128)` na dimensão `upsidedown`, que cai no chunk `(0, -1)`.

#### 1. A classe declara o que é dela

```csharp
public partial class Prop : Area2D
{
    [GodotDictionaryField] public string PropId { get; set; } = "";
    [GodotDictionaryField] public Vector2 Position { get; set; }   // vem de Node2D
}

public partial class Portal : Prop
{
    [GodotDictionaryField] public string TargetDimension { get; set; } = "";
}
```

Nenhuma classe de dado. Nenhum `Capture()`/`Restore()`.

#### 2. O que está no disco

`user://saves/worlds/<id>/upsidedown.json`

```json
{
	"$type": "dimension",
	"Chunks": [ ... mutações de tile ... ],
	"Entities": [
		{
			"InstanceId": 41,
			"ScenePath": "res://Scenes/World/Props/Portal.tscn",
			"DimensionId": "upsidedown",
			"Position": { "x": 864.0, "y": -128.0 },
			"PropId": "portal",
			"TargetDimension": "overworld"
		}
	]
}
```

`PropId` e `Position` vêm do `Prop`; `TargetDimension` vem do `Portal`. A reflexão junta os dois porque `Portal : Prop`.

Não há `$type` aqui: **o `ScenePath` é o tipo.** A cena carrega o script, e é o Godot que resolve.

#### 3. Entrar no mundo: registro sim, node não

```csharp
// WorldManager, ao carregar o mundo.
var save = SaveStorage.LoadDimensionState(worldId, "upsidedown");

Game.Managers.TileStreamingManager.Node.ImportState("upsidedown", save);
Game.Managers.EntityStreamingManager.Node.ImportState("upsidedown", save);
```

```csharp
public void ImportState(string dimensionId, DimensionSaveData save)
{
    foreach (var record in save.Entities)
    {
        var position = record["Position"].AsGodotDictionary();
        var chunk = CoordinateUtilities.WorldToChunk(
            new Vector2(position["x"].AsSingle(), position["y"].AsSingle()),
            Dimensions.TileSize);

        Records(dimensionId, chunk).Add(record);
    }
}
```

Só indexa por chunk. **Nenhum node é criado.**

#### 4. O chunk entra no raio

```csharp
private void OnChunkLoaded(string dimensionId, Vector2I chunkCoord)
{
    var parent = Game.Managers.DimensionManager.Node.ResolveParent(dimensionId);

    foreach (var record in Records(dimensionId, chunkCoord))
    {
        var instanceId = record["InstanceId"].AsInt64();

        if (_live.ContainsKey(instanceId))
        {
            continue;
        }

        var node = GD.Load<PackedScene>(record["ScenePath"].AsString()).Instantiate<Node2D>();

        // Popula as propriedades marcadas direto no node. Nao cria nada:
        // o node ja existe, veio da cena.
        GodotDictionaryParser.ApplyTo(node, record);

        _restoring = instanceId;   // avisa o Register que este veio do save

        parent.AddChild(node);     // dispara _EnterTree -> Register

        _restoring = 0;
    }
}
```

#### 5. A entidade se registra sozinha

```csharp
// Prop.cs — duas linhas, herdadas por Portal e qualquer prop futuro.
public override void _EnterTree() => EntityStreaming.Register(this);
public override void _ExitTree()  => EntityStreaming.Unregister(this);
```

E o `Register` distingue **restaurado** de **nascido agora** — senão o portal recém-carregado entraria de novo na lista e duplicaria:

```csharp
public void Register(Node2D node)
{
    if (!GodotDictionaryParser.HasSerializableFields(node))
    {
        return;   // efeito, label, hitbox: streaming nao tem nada com isso
    }

    if (_restoring != 0)
    {
        // Veio do save: o record ja existe, so liga o node vivo a ele.
        _live[_restoring] = node;

        return;
    }

    // Nasceu agora: alguem colocou um portal. Cria o record.
    var instanceId = InstanceIdGenerator.NextInstanceId();
    var record = GodotDictionaryParser.ToDictionary(node);

    record["InstanceId"] = instanceId;
    record["ScenePath"] = node.SceneFilePath;
    record["DimensionId"] = Dimensions.ResolveDimensionIdOf(node);

    Records(record["DimensionId"].AsString(), ChunkOf(node)).Add(record);

    _live[instanceId] = node;
}
```

#### 6. O chunk sai do raio

```csharp
private void OnChunkUnloaded(string dimensionId, Vector2I chunkCoord)
{
    foreach (var record in Records(dimensionId, chunkCoord))
    {
        var instanceId = record["InstanceId"].AsInt64();

        if (!_live.TryGetValue(instanceId, out var node))
        {
            continue;
        }

        // O estado mora no node: serializa ANTES de liberar, ou ele se perde.
        Merge(record, GodotDictionaryParser.ToDictionary(node));

        _live.Remove(instanceId);

        node.QueueFree();
    }
}
```

O record continua na lista; o node morre. Quando o chunk voltar, o passo 4 refaz tudo a partir do mesmo dicionário.

**Esquecer é o outro caminho**, e só ele mexe na lista:

```csharp
public void Forget(long instanceId)   // item recolhido, prop quebrado
{
    // tira de _records e de _live; o node se vira com o proprio QueueFree
}
```

#### 7. Salvar

O `SaveManager` já emite `Saving` antes de serializar — é onde o estado vivo entra no record:

```csharp
private void OnSaving()
{
    foreach (var (instanceId, node) in _live)
    {
        Merge(_recordById[instanceId], GodotDictionaryParser.ToDictionary(node));
    }
}
```

Entidade descarregada não precisa de nada: o record dela já está atualizado desde o unload.

#### O ciclo fechado

```
JSON --ScenePath--> Portal.tscn --Instantiate--> node --ApplyTo--> propriedades
  ^                                               |
  |                                          AddChild
  |                                               |
  |                                          _EnterTree --> Register --> _live
  |                                                                        |
  +---- ToDictionary(node) <---- unload / Forget / Saving <----------------+
```

Em nenhum ponto alguém pergunta "que tipo de entidade é essa". O `ScenePath` resolve a cena, o atributo resolve o que serializa, e o `_EnterTree` resolve o registro.

### 3.6 `DimensionManager` — de 18 métodos de spawn a 1 RPC

Com auto-registro e tipo no resource, sobra do manager só o que é mesmo dele: **saber onde é o lugar**.

```
DimensionManager
├─ ResolveParent / ResolveLayer / ResolveBaseLayer / ShowOnly
├─ FindGroundSpawnPosition
└─ SpawnReceive(Dictionary record)  ← o único RPC que sobra
```

`RestoreProps`, `CollectProps`, `SpawnTestNPC` e os 18 métodos de spawn somem. Quem restaura é o `EntityStreamingManager` ao carregar o chunk; quem coleta é o `ExportState` dele.

**Por que ainda sobra um RPC:** o RPC do Godot exige que o node já exista nos dois lados, no mesmo caminho. Um node que ainda não existe não pode receber RPC — então **criação não pode ser self-service**, mesmo com auto-registro. Registro, save, quebra e interação podem; criação, não.

**Questão em aberto:** o `MultiplayerSpawner` nativo resolve exatamente isso — aponta pra um parent, lista as cenas spawnáveis, e um `AddChild` no servidor replica sozinho. O projeto não usa nenhum (`MultiplayerSpawner`/`MultiplayerSynchronizer`: 0 ocorrências). Se adotado, o último RPC também some e o auto-registro cobre o ciclo inteiro. **Precisa de investigação antes de decidir.**

### 3.7 `MinimapSystem` (novo, ~50 linhas)

Assina `ChunkLoaded`, varre as células do chunk e pinta a imagem de descoberta. Guarda `_discoveredOverworld`/`_discoveredUpsidedown` e expõe `GetDiscoveredTexture`. É o que sai do `TileStreamingManager`.

---

## 4. Como o save fica

```
user://saves/worlds/<id>/
├─ world.json              meta, semente, personagens
├─ overworld.json
│    ├─ Chunks[] → ChunkEntryData { coord, ChunkStateData { Mutations[] } }
│    └─ Entities[] → um dicionário por entidade  ← NOVO, chaveado por chunk
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

Hoje o registry guarda `WorldSaveData` e `CharacterSaveData`. Passa a guardar também as entidades de cada dimensão, que o `EntityStreamingManager` atualiza no evento `Saving`:

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
Portal._EnterTree()  → EntityStreamingManager.Register(this)
Portal._ExitTree()   → EntityStreamingManager.Unregister(this)
```

Não há passagem obrigatória por um método de spawn: quem entra na árvore está registrado, venha de onde vier — do streaming, de um RPC, ou de código de gameplay.

O `EntityStreamingManager` é quem fala com o `SaveManager`, registrando o `DimensionSaveData` de cada dimensão. Cadeia preservada: **entidade → manager → system**.

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
| `GodotDictionaryParser.ApplyTo` + `[GodotDictionaryField]` nas entidades | — | ~40 | +40 |
| `MinimapSystem` | — | ~50 | +50 |
| `CoordinateUtilities` | — | ~30 | +30 |
| `WorldManager` | 218 | ~230 | +12 |
| 4 telas (`FindLocalPlayer`) | — | — | −40 |
| **total** | | | **≈ −236 linhas** |

Menos código, e o que sobra tem uma responsabilidade cada.

---

## 8. Ordem de execução

Cada passo compila e roda sozinho.

> **Feito em 2026-08-26:** migração do save para JSON (`SaveStorage`, `GodotDictionaryParser`, `[SaveType]`), e remoção da última chamada de UI dentro do `SessionManager` (`CompleteLocalCreation`), que estava quebrando o build.

1. **`CoordinateUtilities`** — extrai `WorldToCell`/`CellToChunk`, sem mudar comportamento.
2. **`WorldManager`** — adiciona `GetAllPlayers`/`GetPlayersInDimension`; remove `FindLocalPlayer` das 4 telas.
3. **`MinimapSystem`** — extrai `RecordDiscovered`/`GetDiscoveredTexture`; `TileStreamingManager` passa a emitir `ChunkLoaded`.
4. **Rename `ChunkStreamingManager` → `TileStreamingManager`** — e tira `RecordMutation`, `ApplyMutations`, `ResolveBiome`, `PreloadSpawnAreaAsync` para os donos certos.
5. **`ApplyTo` no parser + `[GodotDictionaryField]` no `Prop`** — o `Prop` ganha as duas linhas de `_EnterTree`/`_ExitTree`; nada mais muda ainda.
6. **`DimensionManager.SpawnReceive(Dictionary)`** — o RPC genérico, convivendo com os antigos.
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
