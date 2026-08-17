# Redesenho dos managers — plano de ataque (EXECUTADO)

As features já estão prontas e funcionando. Este plano redistribui responsabilidades **sem mudar comportamento**: o que é de entidade volta pra entidade, e sobra manager só pro que não tem dono na árvore.

Estado inicial: `WorldManager` com **2029 linhas, 94 métodos, 23 RPCs**.
Estado final: **233 linhas**. Os 9 passos foram executados em 2026-08-16; ver seção 8 pro resultado e o que ainda não foi testado.

---

## 1. A regra

> **Manager cuida do que está fora do comportamento de uma entidade.**
> Se a ação tem um alvo que já existe na árvore, ela é método (e RPC) desse alvo.

Entidade também é `Node` — faz RPC, tem `_Process`, acessa a árvore. O `Player` já prova isso com **19 RPCs próprios**. Foi supor o contrário ("replicou, então é manager") que engordou o `WorldManager`.

| Regra | |
|---|---|
| **R1 — Alvo existe? É dele.** | Ação com alvo endereçável na árvore é método e RPC do alvo. |
| **R1b — Criar é de quem tem o lugar.** | O nó novo não existe pra receber o RPC, então quem cria é o dono do lugar onde ele nasce — um só pra todos os tipos (`DimensionManager`), nunca um por tipo. |
| **R1c — Buscar é estático da classe.** | `Player.FindByPeerId(...)`, `WorldItem.FindById(...)`. Consulta não precisa de nó na árvore. |
| **R2 — Nenhum manager leva nome de entidade.** | `XManager` onde `X` é um **tipo de entidade** (`Player`, `Portal`, `WorldItem`, `Terrain`) é sinal de que o comportamento é do próprio `X` ou do pai dele. Não vale pra manager de estado de mundo (`World`, `Session`, `Dimension`), que não tem entidade correspondente. |
| **R3 — Um dono por estado.** | Ninguém guarda cópia nem cache do estado de outro. |
| **R4 — Entidade não chama manager que chama alguém.** | Precisa avisar alguém acima? `[Signal]`. A exceção é consulta sem dependência (`GetLocalPlayer`, `FindPlayerByPeerId`): quem não chama ninguém não fecha ciclo. É o que impede o ciclo `ChunkStreaming ↔ World` de voltar. |
| **R5 — Chamada só desce.** | Camadas fixas (seção 3). |
| **R6 — System não é depósito.** | Classe C# pura só pra algoritmo autossuficiente (`ChunkGeneratorSystem`, `Inventory`). Código que sai de manager vai pra entidade, nunca pra um system novo. |

**Âncora técnica do R1/R1b:** um RPC só chega num nó que exista com o mesmo caminho nos dois peers. `TerrainLayer`, `Portal` e `WorldItem` são tão endereçáveis quanto o `Player` — nunca receberam o papel, só isso. Criação é a exceção real: o alvo ainda não existe, então o RPC fica no `DimensionManager`.

---

## 2. O que está errado hoje

**a) Comportamento de entidade morando no manager.** ~239 linhas de terreno (`EraseBlockAndReconnect`, `PaintBlockAndReconnect`, `RepaintDependentLayerForCells`, `ReconnectDecorationsNear`, `ApplyChunkMutation`, `EraseCellWithTerrainConnect`, `BreakDecorationOnly`, `PlaceBlockOnCorrectLayer`) recebem `TerrainLayer` como **primeiro parâmetro** e não tocam em mais nada — três já são `static`. Mesmo padrão em `SpawnPortal`/`RestorePortals` (portal) e `FindWorldItem` (item).

**b) `dimensionId` viaja por 24 assinaturas** só no `WorldManager` (72 no projeto), quase sempre pra reencontrar a layer que o chamador já tinha em mãos.

**c) Ciclo `ChunkStreamingManager` ↔ `WorldManager`** (6 pontos numa direção, 11 na outra): os dois disputam a posse do estado do mundo e, como nenhum ganhou, os dois mexem em tudo.

**d) Lookup por grupo onde bastava endereçar.** `TeleportPlayer(peerId, …)` varre `GetNodesInGroup("players")` pra achar o alvo do próprio RPC.

**e) Política de save espalhada.** `SaveManager` (362 linhas) é repositório limpo; o *quando/o quê/de quem* salvar está no `WorldManager`.

---

## 3. Arquitetura alvo

```
┌─ SessionManager ────────────────────────────────┐  o que a UI enxerga
├─ WorldManager ──────────────────────────────────┤  entrada/saída de mundo
├─ NetworkManager · ChunkStreamingManager ────────┤  peers e streaming
├─ DimensionManager · SaveManager · ScreenManager ┤  base: não chamam ninguém
└─ entidades ─────────────────────────────────────┘
     TerrainLayer · Player · Portal · WorldItem
```

**Entidades** (chamadas por qualquer camada acima; não chamam manager):

| Entidade | Dona de |
|---|---|
| `TerrainLayer` | edição de célula, autotile, RPC de bloco |
| `Player` | próprio estado, teleporte, troca de dimensão, kit inicial, buscas estáticas |
| `Prop` (base nova) → `Portal` | colocar/quebrar/persistir **qualquer prop**; `Portal` fica só com a troca de dimensão |
| `WorldItem` | remover a si mesmo, `FindById` estático |

**Managers** (7, nenhum com nome de tipo de entidade):

| Manager | Fica com | ~linhas |
|---|---|---:|
| `SessionManager` (novo) | `PendingWorld`/`PendingCharacter`/`PendingWorldIsDefault`, `EnterPendingWorld`, `ReturnToMainMenu` | ~120 |
| `WorldManager` | instanciar/liberar `World.tscn`, entrar/sair de mundo, `CurrentWorldSave` | ~150 |
| `NetworkManager` (novo) | peers e join com personagem — **peer não é nó** | ~400 |
| `ChunkStreamingManager` | loop global de load/unload + replicação de chunk | ~640 |
| `DimensionManager` (novo) | parents, layers `Base`/`Compose`, containers, clear, **spawn no mundo** | ~380 |
| `SaveManager` | repositório **+** política de autosave | ~490 |
| `ScreenManager` | inalterado | — |

**Por que dimensão é manager e não entidade** (decidido): as raízes `Overworld`/`Upsidedown` nascem e morrem junto com o `World.tscn`, então um script nelas nunca entraria no registro `Game` — precisaria de grupo e de resolução por sessão. O manager é nó estático em `Managers.tscn`, entra no registro, e **sobrevive à destruição do mundo**: é o dono estável que re-resolve as referências quando o mundo volta. Dimensão aqui é configuração de mundo, não entidade — por isso `DimensionManager` não fere a R2.

Custo aceito: o `dimensionId` continua nas assinaturas de spawn (`SpawnPlayerReceive(dimensionId, …)`) e o spawn passa por um terceiro. O ganho do passo 2 não é afetado — a `TerrainLayer` sabe qual dimensão ela é, então as assinaturas de bloco ficam limpas do mesmo jeito.

---

## 4. Plano de ataque

Ordem obrigatória: **1 → 6 esvaziam o `WorldManager` sem criar manager nenhum.** Só depois os managers restantes são recortados, já pequenos.

### Passo 1 — Terreno puro → `TerrainLayer`

Mover os 8 métodos da seção 2a (~239 linhas) + os privados que só eles usam (`GetSolidNeighborCells`, `GetExpandedNeighborCells`). Vira `layer.EraseBlockAndReconnect(cell)` — sem `TerrainLayer` no primeiro parâmetro, sem `dimensionId`.

*Risco: baixo* — recorte mecânico, nenhum caminho de RPC muda.
*Valida com:* `dotnet build` + smoke headless (mesma contagem de células geradas).

### Passo 2 — RPC de bloco → `TerrainLayer`

`BreakBlockClientRequest`, `BreakBlockServerReceive`, `ProcessBreakBlock`, `BreakBlockBroadcast`, `PlaceBlockAuthoritative`, `PlaceBlockBroadcast`, `ResolveBiomeForCell`. Atualizar os chamadores (`ToolDefinition`, `BlockItemDefinition`, `Player.PlaceBlockReceive`).

Aqui morre o `dimensionId` das assinaturas: a layer sabe qual dimensão ela é.

*Risco: médio* — muda caminho de RPC.
*Valida com:* build + **teste host + cliente**: quebrar e colocar bloco nas duas pontas, conferir autotile na borda.

### Passo 3 — `DimensionManager`

Nó novo em `Managers.tscn` + entrada em `Game.cs`. Dono único das referências que hoje estão espalhadas:

```
/root/Main
├── Managers
│   └── DimensionManager                    ← estático, entra no registro Game
└── World                                   (World.tscn, instanciado em runtime)
    └── Levels                              CanvasLayer
        ├── OverworldViewportContainer      SubViewportContainer  ← visibilidade
        │   └── OverworldViewport           SubViewport
        │       └── Overworld               ← "parent" da dimensão
        │           ├── Base · Compose      TerrainLayer
        │           └── (Player, NPC, Portal, WorldItem em runtime)
        └── UpsidedownViewportContainer/UpsidedownViewport/Upsidedown
```

Estado interno por R4 (dimensão é parâmetro, não par de campos):

```csharp
private readonly Dictionary<string, DimensionData> _dimensions;   // parent, container, layer, baseLayer
```

Recebe `ResolveWorldReferences`, `ClearWorldLayers` + RPC, `FindGroundSpawnPosition`, `ResolveDimensionParent`, visibilidade dos containers e resolução das layers.

Some `WorldManager.OverworldParent`/`UpsidedownParent`/`OverContainer`/`UpContainer` e as 4 `TerrainLayer` cacheadas no `ChunkStreamingManager` (R3). **Aqui o ciclo do 2c se resolve**: os dois passam a perguntar pro `DimensionManager`, que não chama ninguém.

Ponto de atenção: o `World.tscn` é destruído no `LeaveWorld`, então o manager precisa de `Reset()` zerando **as quatro** referências de cada dimensão — não só as duas layers compostas, como o `ChunkStreamingManager.ResetState()` faz hoje.

*Risco: médio* — toca inicialização e `LeaveWorld`.
*Valida com:* build + entrar/sair de mundo 2x seguidas (as referências têm que resolver de novo).

### Passo 4 — Spawn no mundo → `DimensionManager`

`SpawnPlayer`, `SpawnPlayerReceive`, `SpawnPlayerRequest` (2 sobrecargas), `SpawnNpcReceive`, `SpawnNpcRequest`, `SpawnTestNPC`, `SpawnWorldItem`, `SpawnWorldItemReceive`, `SpawnWorldItemRequest` (2), `PlacePortalAuthoritative`, `PlacePortalBroadcast`, `SpawnPortal`, `RestorePortals`.

Um lugar só pra "criar entidade dentro de uma dimensão", em vez de um manager por tipo (R2). O que **não** vem junto: alterar e destruir entidade já criada — isso é dela, e sai nos passos 5 e 6.

*Risco: médio* — muda caminho de RPC de todo spawn.
*Valida com:* build + host + cliente: entrar no mundo, ver player remoto, dropar item, colocar portal.

### Passo 5 — `Prop` base + `WorldItem`

**Não existe "quebrar portal".** Colocar, quebrar e persistir são o ciclo de vida de **qualquer prop** — portal é só o primeiro. Hoje não há classe base: `Portal : Area2D` direto, e o `PropDefinition` é só dado + `Spawn`.

Nasce `Prop : Area2D` (`Features/World/Props/Entities/Prop.cs`) com o que é genérico:

```csharp
public partial class Prop : Area2D
{
    public string PropId { get; set; }          // qual PropDefinition gerou

    public void BreakClientRequest();           // \
    public void BreakServerReceive();           //  } RPC genérico, serve qualquer prop
    public void BreakBroadcast();               // /

    public virtual PropSaveData ToSave();       // coletar pro save
    public static void Restore(PropSaveData s); // restaurar do save
}
```

`Portal : Prop` fica **só** com o que é dele: interagir e trocar de dimensão (`_PhysicsProcess`, cooldown, `RequestTrade`).

Migram do `WorldManager`: `BreakPortalClientRequest`, `BreakPortalServerReceive`, `ProcessBreakPortal`, `BreakPortalBroadcast`, `CollectPortals`, `RestorePortals` — todos perdendo o "Portal" do nome.

**Muda formato de save:** `PortalSaveData` (hoje `PositionX`/`PositionY`/`DimensionId`) vira `PropSaveData` com `PropId`, e `WorldSaveData.Portals` vira `Props`. Mundos já salvos têm a chave antiga — ou entra migração na leitura (`Portals` → `Props` com `PropId = "portal"`), ou os saves atuais perdem os portais. **Decidir antes de começar o passo.**

`WorldItem` no mesmo passo: `RemoveWorldItemReceive`/`RemoveWorldItemRequest` → `WorldItem`; `FindWorldItem` → estático da classe. (`WorldItem` é item no chão, não prop — continua separado.)

*Risco: médio* — muda caminho de RPC e formato de save.
*Valida com:* build + host + cliente: colocar e quebrar portal nas duas pontas + salvar/recarregar mundo com portal.

### Passo 6 — `Player` recebe o que é dele

`TeleportPlayer`, `TeleportPlayerServerReceive`, `TeleportPlayerClientRequest`, `TradeDimension`, `TradeDimensionServerReceive`, `TradeDimensionClientRequest`; a metade de `RespawnLocalSoloPlayer` que preenche `Data`/`Loaded`/kit inicial vira `Player.ApplyCharacter(...)`/`GiveStartingKit()`.

Aqui somem os lookups por grupo (2d) **dentro dos RPCs**: o alvo do RPC é o receptor, não precisa procurar.

**`GetLocalPlayer` e `FindPlayerByPeerId` NÃO vão pro `Player`** — ficam em manager. Motivo: são a pergunta "quem é o jogador local desta sessão", não comportamento de um player, e são o ponto de entrada de **10 arquivos de UI + `CameraController`**. Ficam onde já estão (`WorldManager`) até o `SessionManager` existir (passo 8); depois é escolha de qual dos dois — nenhum churn de chamador enquanto isso, porque a UI já chama `Game.Managers.WorldManager.Node.GetLocalPlayer()`.

O que **não** pode acontecer: virar dependência de outro manager. As duas só leem grupo e `Multiplayer.GetUniqueId()` — não chamam ninguém, e é isso que deixa qualquer camada (inclusive entidade, ver R4) chamá-las sem criar ciclo.

*Risco: médio* — troca de dimensão mexe em reparent + visibilidade.
*Valida com:* build + host + cliente: portal nas duas pontas, conferir que só o dono troca de container.

### Passo 7 — `NetworkManager`

`CreateServer`, `JoinServer`, `Disconnect`, `OnPeerConnected`, `OnPeerDisconnected`, `OnConnectedToServer`, `OnConnectionFailed`, `OnServerDisconnected`, `FinishPeerJoin`, `SavePeerCharacterOnDisconnect`, toda a região `Rpc - Entrada com personagem`, `IsConnected`/`IsServer`/`IsHostOrSolo`. As UIs já chamam o campo de `NetworkManager` — o nome já existe, falta a classe.

*Risco: médio.* *Valida com:* build + criar servidor, entrar, sair, cair a conexão.

### Passo 8 — `SessionManager`

`PendingWorld`, `PendingCharacter`, `PendingWorldIsDefault`, `EnterPendingWorld`, `ReturnToMainMenu`, `SpawnWorldAndJoin`. A UI passa a conversar só com ele; o `WorldManager` deixa de carregar estado de menu.

*Risco: baixo.* *Valida com:* build + pipeline `WorldSelectUI → CharacterSelectUI → mundo → menu`.

### Passo 9 — Política de save → `SaveManager`

`StartAutosaveTimer`, `StopAutosaveTimer`, `SaveCurrentWorld`, `SaveOwnLocalCharacter`, `SaveRemotePeerCharacters`, `PersistBeforeLeaving`.

*Risco: baixo*, independente dos outros — pode vir a qualquer momento.

### Sobra no `WorldManager`

`SpawnWorld`, `CreateProceduralWorldAndPlayer`, `SpawnLocalWorldAndPlayer`, `LeaveWorld`, `SetChunkStreamingEnabled`, `CurrentWorldSave`. ~150 linhas.

---

## 5. Validação

A cada passo, antes de começar o próximo:

- `dotnet build --no-incremental` limpo (0 erros, 0 warnings novos)
- smoke headless: `Game/Tools/*.cs` + `.tscn` temporário, `godot --headless`, conferir a mesma contagem de células geradas, e apagar o `Tools/` depois
- **passos 2, 4, 5 e 6 mudam caminho de RPC** — build não pega isso. Precisa de host + cliente de verdade.
- commit por passo, pra dar pra voltar um passo sem desfazer os outros

---

## 6. Descartado (decidido, não esquecido)

| Ideia | Por que não |
|---|---|
| `TerrainEditSystem`, `ChunkStateStore`, `DimensionRegistry` | seriam depósito de código de manager, não algoritmo (R6) |
| `TerrainManager` | edição de terreno é comportamento da `TerrainLayer` |
| **`Dimension` como entidade** (script na raiz de `Overworld`/`Upsidedown`) | nasceria e morreria com o `World.tscn`, fora do registro `Game`, exigindo grupo + resolução por sessão. Preferido o `DimensionManager` estático, que sobrevive à destruição do mundo. Existe um rascunho antigo em `Features/World/Dimensions/Managers/DimensionManager.cs` — serve de ponto de partida pro passo 3 |
| `PlayerManager`, `WorldItemManager`, `PortalManager` | manager por entidade (R2): criar é do pai, buscar é estático |
| `SpawnManager` | mesmo erro, só que agrupado |
| **`World.tscn` estático** (filho fixo do `Main.tscn`) | colocaria parents e layers no registro `Game`, mas exige limpeza explícita de toda entidade no `LeaveWorld` + snapshot do `tile_map_data` autoral do `Upsidedown.tscn`. Adiado |
| **Renomear pastas** (`Singletons/TerrainLayer.cs` → entidade, `Systems/PlayerInput.cs` → entidade filha) | cosmético, fica pro fim |

---

## 7. Contexto relacionado

- **[node-registry-bootstrap.md](node-registry-bootstrap.md)** — registro `Game` + Bootstrap (feito): removeu 54 lookups, ~110 propriedades espelhadas e ~40 `?.` defensivos. É o que faz classe nova já nascer alcançável.
- **[world-manager-redundancy-review.md](world-manager-redundancy-review.md)** — duplicações pontuais entre `WorldManager` e `ChunkStreamingManager`; os itens 1–3 se resolvem sozinhos nos passos 1–3 daqui.
- **[world-generation.md](world-generation.md)** — como o mundo é gerado e salvo hoje; referência pra não mudar comportamento sem perceber.

---

## 8. Resultado da execução (2026-08-16)

Todos os 9 passos aplicados. `dotnet build --no-incremental` limpo (3 warnings pré-existentes de `CS0108` nas hitboxes) e boot headless sem erro: `[Bootstrap._Ready] 116 nodes estaticos registrados, jogo pronto`.

| Arquivo | Linhas | |
|---|---:|---|
| `WorldManager` | **233** | era 2029 |
| `TerrainLayer` | 1383 | +390: edição de bloco, autotile e RPC de bloco |
| `NetworkManager` | 607 | novo |
| `DimensionManager` | 535 | novo |
| `SaveManager` | 517 | +155 de política de autosave |
| `ChunkStreamingManager` | 623 | perdeu o cache de layer |
| `Portal` | 106 | só a troca de dimensão |
| `Prop` | 85 | novo: colocar/quebrar/persistir genérico |
| `SessionManager` | 71 | novo |

Total: **1089 inserções, 2074 remoções** em 20 arquivos.

### Decisões tomadas durante a execução

- **`dimensionId` sumiu das assinaturas de bloco.** A `TerrainLayer` deduz a dimensão do nome do próprio pai (`Overworld`/`Upsidedown`), sem `[Export]` novo nas cenas.
- **`BaseLayer` é irmã, não pergunta pra manager.** A layer resolve a `Base` pelo próprio pai e cacheia. Quando `this` já é a `Base`, resolve pra si mesma — mesmo comportamento de antes.
- **Save migrado, não quebrado.** `PortalSaveData` → `PropSaveData` (com `PropId`), `WorldSaveData.Portals` → `Props`. `SaveManager.MigrateLegacyPortals` converte na leitura; mundos salvos antes continuam com os portais.
- **`CloseSession()` no `NetworkManager`** — o `LeaveWorld` não conhece mais `Peer` nem os dicionários de peer.
- **`GetLocalPlayer`/`FindPlayerByPeerId` ficaram no `WorldManager`**, como combinado: 10 arquivos de UI + `CameraController` não mudaram uma linha.
- **`Player.FindByPeerId(SceneTree, long)`** existe como estático, usado internamente; a versão do manager segue como ponto de entrada da UI.

### O que NÃO foi testado

`dotnet build` e boot headless não exercitam nenhum caminho de RPC. Os passos 2, 4, 5 e 6 **mudaram o nó que recebe o RPC** — precisa de teste host + cliente:

- [ ] quebrar/colocar bloco nas duas pontas (autotile na borda entre biomas)
- [ ] colocar e quebrar portal; trocar de dimensão pelo portal
- [ ] entrar com 2 peers: ver player remoto, NPC, item dropado
- [ ] salvar e recarregar um mundo **antigo** (valida a migração `Portals` → `Props`)
- [ ] entrar e sair de mundo 2x seguidas (o `DimensionManager.Reset` tem que re-resolver)

---

## 9. Passo 10 — RouterManager (navegação de telas)

Feito depois dos 9 passos, mesmo critério: *qual tela está aberta* é estado que não pertence a nenhuma tela — logo, manager.

**Antes:** 31 trocas de `Visible` espalhadas por 13 telas, e `Open()` existindo em só 4 delas — cada tela inventava seu jeito de aparecer:

```csharp
Visible = false;
Game.Ui.WorldSelectUI.Node.Open();
```

**Depois:** `Visible` de tela é escrito **só** pelo `RouterManager` (R3 aplicado à visibilidade):

```csharp
Game.Managers.RouterManager.Node.Open(Game.Ui.WorldSelectUI.Node);
```

### As peças

**`ScreenUI : CanvasLayer`** (`Features/UI/Common/Abstractions/ScreenUI.cs`) — base das 15 telas:

| Membro | Papel |
|---|---|
| `IsOverlay` | overlay aparece por cima (Hud, Pause, Console, Loading…); exclusiva substitui e empilha |
| `CanOpen()` | a tela valida se pode abrir; `false` cancela a transição |
| `OnOpened()` / `OnClosed()` | reação, não decisão |

**`RouterManager`** (`Features/UI/Router/Managers/RouterManager.cs`) — `Open`, `Replace`, `Close`, `Back`, `CloseAll`, com pilha de histórico.

**`ScreenManager` → `WindowManager`.** O antigo `ScreenManager` não era de telas: 32 linhas de fullscreen/F11, ou seja, janela. Renomeado (`Features/UI/Window/Managers/WindowManager.cs`), o que libera o vocabulário e evita ferir a R2 com o novo `ScreenUI`.

### O primeiro uso real do `CanOpen`

`CharacterSelectUI.CanOpen()` exige mundo escolhido antes de abrir no contexto `OwnWorld`. É o fluxo sequencial `WorldSelect → CharacterSelect` virando invariante checada, em vez de convenção.

### Tela não se abre

Nenhuma tela chama `Open` em si mesma, e ninguém chama `Close` antes de abrir outra — `Open` já fecha a atual. Métodos como `OpenForOwnWorld()`, `OpenServer()`, `ReopenLocal()` foram apagados: quem vai abrir define o contexto e pede ao router.

```csharp
// errado - a tela decidindo que aparece, e o Close redundante zerando o historico
Game.Managers.RouterManager.Node.Close(this);
Game.Ui.CharacterSelectUI.Node?.OpenForOwnWorld();

// certo - contexto + uma chamada ao router
Game.Ui.CharacterSelectUI.Node.CurrentContext = CharacterSelectContext.OwnWorld;
Game.Managers.RouterManager.Node.Open(Game.Ui.CharacterSelectUI.Node);
```

A tela monta a si mesma em `OnOpened()`, lendo o contexto — e o que fazer com a escolha do usuário também é dela (`SelectLocal` decide por contexto), o que apagou o callback `OnLocalSelected` que os chamadores injetavam.

Duas armadilhas fechadas junto: `Close(this)` antes de `Open` zerava `Current` e **quebrava o histórico**; e abrir a tela imediatamente anterior agora **consome** o histórico em vez de empilhar, senão um vai-e-volta cresceria pra sempre.

### Estado inicial é da cena, não do código

`layer` e `visible` de tela vivem no `.tscn`, não no `_Ready`. Isso resolve de vez o problema de o `_Ready` das telas rodar **antes** do `Bootstrap` — não dá pra chamar o router ali (`RouterManager.Node` ainda é `null`), e também não faz sentido: layout é decisão de cena.

As 15 cenas de tela têm `visible = false` e o `layer` que antes estava hardcoded (`20` para menus, `30` para Loading/ErrorModal, `25` DeathScreen, `15` Console/Map, `10` Inventory). `Hud` e `Pause` seguem sem `layer` explícito, como sempre estiveram.

**Nenhuma tela escreve o próprio `Visible`** — nem no `_Ready`, nem em toggle. Os toggles de overlay (`Inventory`, `SkillTree`, `Map`, `Console`) e a regra do `DeathScreen` viraram `Open`/`Close` no router.

> Sintoma de quando isso quebra: tela sem `visible = false` na cena aparece empilhada com as outras no boot, e o `CanvasLayer` de cima come os cliques do menu.

O `HudUI` entra pelo router junto com o mundo (`SpawnLocalWorldAndPlayer`/`CreateProceduralWorldAndPlayer`) e sai no `LeaveWorld`.

### Estado

`dotnet build` limpo, boot headless `[Bootstrap._Ready] 117 nodes estaticos registrados, jogo pronto`. **Navegação não testada em jogo** — soma-se à checklist da seção 8: menu → seleção de mundo → personagem → jogo → pause → menu, e o botão voltar de cada tela.

---

## 10. Mundo padrão vira flag do save

O "Mundo Padrão" era uma linha fixa na lista do `WorldSelectUI` que não correspondia a save nenhum: escolhê-la setava `PendingWorldIsDefault = true` e `PendingWorld = null`, e o mundo resultante não tinha id, não tinha props salvos e não tinha autosave.

Agora é uma propriedade de cada mundo, escolhida na criação:

- **`WorldSaveData.IsProcedural`** (default `true`)
- **checkbox "Mundo procedural"** no `CreateWorldUI`, marcada por padrão (`ProceduralCheck`, registrada no `Game.cs`)
- `SaveManager.CreateWorld(..., bool isProcedural = true)`
- `SessionManager.EnterPendingWorld()` decide pelo save: `!PendingWorld.IsProcedural` → `SpawnLocalWorldAndPlayer(save)`, senão `CreateProceduralWorldAndPlayer(save)`

**`PendingWorldIsDefault` deixou de existir**, e com ele o caso especial de "mundo sem save" — a linha "Mundo Padrão" saiu da lista.

`SpawnLocalWorldAndPlayer` passou a receber o save e a se comportar como qualquer outro mundo (props restaurados, autosave ligado). A única diferença que resta é a que importa: **não liga streaming e não chama `ClearLayers()`** — o terreno é o que está desenhado à mão nas cenas de nível, e limpar apagaria justamente isso.

Boot headless: `[Bootstrap._Ready] 118 nodes estaticos registrados, jogo pronto`. Falta testar em jogo: criar mundo com a checkbox desmarcada e conferir que o mapa desenhado aparece, e que salvar/recarregar preserva a flag.

---

## 11. Personagem sai do NetworkManager e vira responsabilidade do SaveManager

O que estava errado: a **tela** decidia se a ação ia pro servidor ou pro disco. `CreateCharacterUI.IsServerMode` e `CharacterSelectUI.CurrentContext` ramificavam entre `NetworkManager` e `SaveManager` em 6 pontos.

Agora **os RPC de personagem moram no `SaveManager`** — request e receive, os dois lados — e ele resolve sozinho:

```csharp
// tela: nao sabe que existe rede
Game.Managers.SaveManager.Node.CreateCharacter(name);
Game.Managers.SaveManager.Node.DeleteCharacter(characterId);
Game.Managers.SaveManager.Node.SelectCharacter(character);
```

Dentro, a decisão é do `CharacterMode` da sessão: `ServerCharacters` → RPC pro host; senão → disco local.

**De onde vem o `CharacterMode`:** host/solo pega do save no momento em que o mundo é escolhido (setter de `SessionManager.PendingWorld` — a tela de personagem abre depois, já com o modo certo); cliente recebe pela rede no `JoinInfoReceive`. Não dá pra deduzir de `CurrentWorldSave` em todo mundo: **quem acabou de conectar não tem save do mundo.**

### A direção da dependência

Movi 13 métodos + `_peerCharacters`/`_pendingProfileByPeer` + o evento `ServerCharacterListAvailable` pro `SaveManager` (373 linhas no `NetworkManager`, 849 no `SaveManager`).

Pra não fechar ciclo `Save ↔ Network`, o `NetworkManager` **parou de saber o que é personagem**: quando um peer cai, ele emite `PeerLeft(peerId, player)` e o `SaveManager` assina, persiste e esquece o peer. Sobraram duas chamadas do `NetworkManager` pro `SaveManager` (`PersistBeforeLeaving`, `RequestJoinInfo`), ambas pedidos de ação, não de dado.

Divisão final:

| | fica com |
|---|---|
| `NetworkManager` | peer: criar servidor, entrar, cair, `FinishPeerJoin`, `CloseSession` |
| `SaveManager` | personagem (local e de servidor), incluindo os RPC, e a política de autosave |
