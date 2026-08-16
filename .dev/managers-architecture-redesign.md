# Redesenho: SaveManager, ChunkStreamingManager e WorldManager

Avaliação do estado atual dos três gerenciadores e proposta de reimplementação. Escrito depois que as features já estão prontas e funcionando — o objetivo é reorganizar responsabilidades sem mudar comportamento.

---

## Parte A — Diagnóstico

### A.1 `WorldManager` virou god object

**2029 linhas, ~95 métodos, 8+ responsabilidades distintas.** Levantamento por região:

| Responsabilidade | Métodos | Onde deveria estar |
|---|---:|---|
| Rede (create/join/disconnect, eventos de peer) | ~15 | `NetworkManager` |
| Ciclo de vida do mundo (spawn/enter/leave/return) | ~10 | `WorldManager` (o que sobra) |
| Spawn/lookup/replicação de player + NPC | ~10 | `PlayerSpawnSystem` |
| **Edição de terreno (break/place + autotile)** | **~25** | `TerrainEditSystem` |
| World items (spawn/remove/find) | ~7 | `WorldItemSystem` |
| Portais (place/break/restore/collect) | ~8 | `PortalSystem` |
| Teleport / troca de dimensão | ~6 | `PlayerSpawnSystem` |
| Fluxo de personagem no join (RPC) | ~12 | `CharacterSessionSystem` |
| Autosave (timer + política do que salvar) | ~7 | `AutosaveSystem` |

O maior bloco isolado é **edição de terreno** — `EraseBlockAndReconnect`, `PaintBlockAndReconnect`, `RepaintDependentLayerForCells`, `GetExpandedNeighborCells`, `ReconnectDecorationsNear`, `EraseCellWithTerrainConnect`… ~800 linhas de lógica de domínio de terreno morando dentro de um gerenciador que, pelo nome e pelo resto do conteúdo, é de rede/mundo.

### A.2 Dependência circular `ChunkStreamingManager` ↔ `WorldManager`

Não é impressão — está no grafo de dependências:

```
ChunkStreamingManager ──► WorldManager     (6 pontos)
  · WorldManager.OverworldParent / UpsidedownParent   (_Process, 2x)
  · WorldManager.ResolveDimensionParent(...)          (3x)
  · WorldManager.ApplyChunkMutation(...)              (1x)

WorldManager ──► ChunkStreamingManager     (11 pontos)
  · ResolveLayer / ResolveBaseLayer / ResolveBiome
  · RecordMutation / ExportState / ImportState / SetWorldSeed
  · PreloadSpawnAreaAsync / CatchUpPeer / RemovePeer / ResetState
```

Nenhum dos dois existe sem o outro. Isso é a causa raiz do "as responsabilidades se misturaram": os dois disputam a posse do **estado do mundo** (quem é dono das layers? quem é dono das mutações?), e como nenhum ganhou, os dois acabaram mexendo em tudo.

Sintoma visível disso: `WorldManager.ResolveDimensionLayer` e `ChunkStreamingManager.ResolveLayer` resolvem exatamente as mesmas duas layers, por caminhos diferentes, cada um com seu cache.

### A.3 `SaveManager` está limpo — o problema é a política, não o repositório

`SaveManager` (362 linhas) é um repositório puro: lê e escreve `.tres`, não depende de nenhum outro manager, não tem estado além do `CachedProfile`. **Não precisa ser reescrito.**

O que está espalhado é a *política* de save, que hoje mora no `WorldManager`:
- quando salvar (`AutosaveTimer`, `PersistBeforeLeaving`)
- o que salvar (`SaveCurrentWorld` → dimensões + portais + meta + personagem local + personagens de peers)
- de quem salvar (`SaveOwnLocalCharacter`, `SaveRemotePeerCharacters`, `SavePeerCharacterOnDisconnect`)
- regra de modo (`WorldCharacterMode.ServerCharacters` → `SaveServerCharacter`, senão `SaveBackup`)

### A.4 Zero autoloads, resolução de node ad-hoc em todo lugar

**54 lookups `GetTree().Root.GetNode(OrNull)<T>(...)` espalhados por 22 arquivos.** Cada classe procura suas dependências sozinha, no próprio `_Ready`, e depois se defende com `?.` em cada uso — porque não existe nenhuma garantia de ordem de inicialização.

Consequências:
- funciona hoje por **sorte da ordem da árvore** (`Managers` vem antes de `Ui` no `Main.tscn`), não por contrato
- ~40 `?.` defensivos que nunca deveriam ser necessários (esses nós existem desde o estado inicial e nunca são reinstanciados)
- se alguém renomear/mover um nó, o erro aparece como `NullReferenceException` aleatório em runtime, longe da causa
- não existe momento definido de "o jogo está pronto"

---

## Parte B — Proposta

### B.1 Composition root + registro de nodes — ✅ IMPLEMENTADO

Extraído para documento próprio: **[node-registry-bootstrap.md](node-registry-bootstrap.md)**.

Resumo do que ficou pronto: `Bootstrap` no root do `Main.tscn` resolve e valida os 23 nós estáticos de uma vez, popula o registro `Game` (que espelha a árvore — `Game.Managers.WorldManager.Node`), e só então abre a `StartUI`. Classes acessam direto, sem propriedade espelhada e sem null-check; quem precisa agir na inicialização usa `Game.WhenReady(...)`.

Isso removeu 54 lookups, ~20 propriedades e ~40 `?.` defensivos — e já expôs um bug latente (caminho errado do `Minimap`, que era sempre null).

### B.2 Quebrar o ciclo com uma camada de dados no meio

O ciclo existe porque os dois disputam a posse do estado do mundo. Extraindo esse estado para dois donos explícitos, o grafo vira acíclico:

```
        ChunkStateStore              DimensionRegistry
     (mutações por chunk)      (parents + layers Base/Compose)
        ▲          ▲                  ▲            ▲
        │          └────────┐  ┌──────┘            │
        │                   │  │                   │
  ChunkStreamingManager ────┘  └──── TerrainEditSystem
        │
        └──────────► ChunkGeneratorSystem
```

**`DimensionRegistry`** — dono único de `OverworldParent`/`UpsidedownParent` e das layers `Base`/`Compose` de cada dimensão.

```csharp
public class DimensionRegistry
{
    public Node2D ResolveParent(string dimensionId);
    public TerrainLayer ResolveLayer(string dimensionId);
    public TerrainLayer ResolveBaseLayer(string dimensionId);
}
```
Mata de vez a duplicação `WorldManager.ResolveDimensionLayer` × `ChunkStreamingManager.ResolveLayer`.

**`ChunkStateStore`** — dono único das mutações. Dados puros, zero dependência.

```csharp
public class ChunkStateStore
{
    public ChunkStateData GetOrCreate(string dimensionId, Vector2I chunkCoord);
    public void RecordMutation(string dimensionId, Vector2I cell, string type, string extraData);
    public DimensionSaveData Export(string dimensionId);
    public void Import(string dimensionId, DimensionSaveData save);
    public void Clear();
}
```

**`ChunkStreamingManager`** passa a fazer só o que o nome diz: decidir o que carregar/descarregar e replicar via RPC.

**`TerrainEditSystem`** (novo, ~800 linhas vindas do `WorldManager`) — só edita terreno: quebrar, colocar, reconectar autotile, aplicar mutação salva.

```csharp
public class TerrainEditSystem
{
    public bool PlaceBlock(Vector2I cell, string blockId, string dimensionId);
    public bool BreakBlock(Vector2I cell, string dimensionId);
    public void ApplyMutation(ChunkMutationData mutation, string dimensionId);
}
```

Com isso, `ChunkStreamingManager` chama `TerrainEditSystem.ApplyMutation` (uma direção só), e `TerrainEditSystem` grava em `ChunkStateStore` (outra direção, sem voltar) — **sem ciclo**, e `WorldManager` sai completamente da equação de terreno.

### B.3 Save: manter o repositório, extrair a política

`SaveManager` fica como está (repositório). Nasce um `AutosaveSystem` com o que hoje está no `WorldManager`:

```csharp
public class AutosaveSystem
{
    public void Start(WorldSaveData save);   // cria o Timer
    public void Stop();
    public void SaveNow();                   // ex-SaveCurrentWorld
}
```

Ele orquestra: `ChunkStateStore.Export` → `SaveManager.SaveDimensionState`, `PortalSystem.Collect` → `SaveManager.SaveWorldMeta`, personagens → `SaveManager.SaveLocalCharacter`/`SaveServerCharacter`/`SaveBackup`.

### B.4 Resultado final da decomposição

```
Bootstrap ──► Game (registro espelhando a árvore)   ← já implementado

WorldManager (slim, ~200 linhas)   ciclo de vida do mundo
NetworkManager                     peer lifecycle, create/join/disconnect
PlayerSpawnSystem                  spawn/lookup/replicação de player e NPC
TerrainEditSystem                  break/place/autotile/mutação
WorldItemSystem                    itens no chão
PortalSystem                       portais
CharacterSessionSystem             fluxo de personagem no join (RPC)
AutosaveSystem                     política de save
SaveManager                        repositório (inalterado)
ChunkStreamingManager              load/unload + replicação de chunk
ChunkGeneratorSystem               geração (inalterado)
DimensionRegistry                  parents + layers
ChunkStateStore                    mutações por chunk
```

---

## Parte C — Ordem de execução

| # | Passo | Risco | Ganho |
|---|---|---|---|
| 1 | ~~**Bootstrap + registro `Game`**~~ ✅ **feito** — ver [node-registry-bootstrap.md](node-registry-bootstrap.md) | Baixo | Removeu 54 lookups, ~20 propriedades espelhadas e ~40 `?.`; criou o ponto de "jogo pronto" (`Game.WhenReady`); facilita todos os passos seguintes (classe nova já nasce acessível) |
| 2 | **`DimensionRegistry` + `ChunkStateStore`** | Médio | Quebra o ciclo; mata a duplicação de resolve de layer |
| 3 | **Extrair `TerrainEditSystem`** | Médio | Tira ~800 linhas (25 métodos) do `WorldManager` de uma vez |
| 4 | **Extrair `NetworkManager`** | Médio | As UIs já chamam o campo de `NetworkManager` — o nome já existe, só falta a classe |
| 5 | `PlayerSpawnSystem`, `WorldItemSystem`, `PortalSystem`, `CharacterSessionSystem`, `AutosaveSystem` | Baixo cada | Conforme a dor; o `WorldManager` fica com ~200 linhas no fim |

**Por que o passo 1 vem primeiro:** os passos 3–5 criam classes novas que precisam ser alcançáveis por quem já existe. Com o registro pronto, isso é uma propriedade no `ManagersRegistry` + uma linha no `Bootstrap`; sem ele, cada classe nova reabre o problema de "como é que eu acho esse cara na árvore".

**Validação sugerida a cada passo:** `dotnet build` limpo + smoke test headless (criar `Game/Tools/*.cs` + `.tscn` temporário, rodar `godot --headless`, conferir que a geração de chunk continua com a mesma contagem de células, e apagar o `Tools/` depois).
