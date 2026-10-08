# De managers a contextos estáticos

Decisão de arquitetura tomada em 2026-09-20. Estado: **migração concluída em 2026-09-20**,
revista no mesmo dia — a segunda passada está em "O critério" e "Como ficou".

## O que muda

Os managers deixam de ser nós sob `/root/Main/Managers` e passam a ser classes
estáticas de contexto, fora da árvore. Os RPCs saem dos managers e vão para os nós
que são donos do estado que o RPC altera. O `Bootstrap` e o registro `Game` são
removidos no fim da migração.

## Por quê

Uma cena de level aberta sozinha (F6 na `Upsidedown.tscn`) não tem sistema nenhum:
o `Bootstrap` resolve tudo por caminho absoluto a partir de `/root/Main`, e sem essa
árvore `Game.IsReady` nunca vira true — todo `WhenReady` fica na fila e os nós da
cena leem managers nulos. O resultado prático é mapa sem iluminação, sem streaming
e sem spawn.

O registro também não se sustenta pelo que carrega. Das 130 entradas:

| Ramo | Entradas | Observação |
| --- | ---: | --- |
| `/root/Main/Ui` | 118 | 254 dos 261 usos de `Game.Ui.*` partem de dentro da própria UI |
| `/root/Main/Managers` | 11 | vira contexto estático |
| `/root/Main` | 1 | usado em 2 lugares do `WorldContext` |

Ou seja: quase todo o registro é tela procurando os próprios filhos, o que um
`GetNode` em `_Ready` resolve sem catálogo global. Os 7 usos que cruzam a árvore são
do mundo abrindo tela (`Router.Open(HudUI)`, `LoadingUI`) — a direção que a
convenção inverte: a interface conhece a árvore, a árvore não conhece a interface.

Removidos os managers e invertida a UI, o `Bootstrap` fica sem função.

## O que continua precisando de nó

- **RPC.** O Godot roteia RPC por caminho de nó; classe estática não recebe.
  - Conteúdo de mundo (`LoadChunkReceive`, `UnloadChunkReceive`, `SetWorldSeedReceive`,
    `ReceiveLightWorld`, `ReceiveBackgroundLight`, `Spawn*Receive`, `DespawnReceive`,
    `ClearLayersReceive`) vai para os nós de dimensão/camada, que já existem nos dois
    peers. O parâmetro `dimensionId` some: vira identidade do nó.
  - Ação de tela (`SubmitLocalCharacter`, `RequestServerCharacterList`,
    `CreateServerCharacter`, `SelectServerCharacter`, `DeleteServerCharacter` e a
    resposta `ServerCharacterListReceive`) vai para a tela que dispara a ação. O par
    `Request` + `ServerReceive` já existe no `SessionContext`; só muda o nó dono.
  - Handshake de conexão (`RequestJoinInfo` / `JoinInfoReceive`) **não** é ação de tela:
    dispara em `ConnectionSucceeded`. Ficou na `MultiplayerUI`, a tela que inicia a conexão.

- **Tick.** Nenhum. Contexto não tem loop: quem tem frame é o nó (ver abaixo).

### Duas regras que isso fixa

1. Tela dona de RPC não pode virar nó instanciado em runtime. As 17 telas são filhas
   estáticas da `Ui.tscn`, então existem nos dois peers no mesmo caminho mesmo fechadas;
   instanciar em runtime quebra o caminho e o RPC falha em silêncio.
2. Método `*ServerReceive` numa tela roda com a tela fechada, no servidor. Ele só pode
   falar com o contexto — nunca com estado visual.
3. **O contexto nunca chama a tela.** A tela lê o contexto e chama o contexto; o contexto
   só guarda estado e responde pergunta. Quando o mundo precisa avisar a interface, ele
   emite evento (`CharacterSelectionRequired`, `SessionEnded`) e quem assina é a tela.

   Isso decide quem manda cada RPC: **quem recebeu um pedido manda a resposta.** O
   `RequestServerCharacterListServerReceive` pergunta ao contexto se aceita o perfil e
   qual é a lista, e então dá o `RpcId` de volta ele mesmo. Antes o contexto recebia o
   controle no meio e chamava `CharacterSelectUI.SendServerCharacterList(...)` — a tela
   chamava o contexto que chamava a tela de novo.

   Pelo mesmo motivo a `CreateCharacterUI` pede à `CharacterSelectUI` para entrar com o
   personagem local (`UseCharacter`): o RPC de submissão mora lá, e RPC é roteado por
   caminho de nó. Tela conhecer tela é permitido; contexto conhecer tela não.

   Montar e desmontar o mundo segue a mesma regra. O `SessionContext` emite `WorldEntered`,
   `LoadingStarted`, `LoadingFinished` e `SessionEnded`; a `HudUI`, a `LoadingUI` e a
   `StartUI` assinam no `_Ready` e abrem ou fecham a si mesmas. Nenhum contexto tem
   `using Jogo25D.UI`.

## O critério

A primeira passada transformou os sete managers em contexto de uma vez. Na revisão, o
corte passou a ser um só:

> Contexto se justifica quando guarda **estado que não existe na árvore**.

Quatro passam: `RouterContext` (o histórico de telas não existe em lugar nenhum da
árvore), `NetworkContext` (peer, porta, último erro, os cinco eventos), `SessionContext`
(quem é cada peer, qual save está aberto) e `SaveContext` (registro de recursos e
intervalo de autosave).

Três não passavam e deixaram de existir:

- `WindowContext` não tinha estado nenhum — dois métodos de cursor e um de fullscreen,
  todos chamada pura de engine. Virou `WindowUtilities`, em `Utils/Window/`: não conhece
  nenhum domínio, e a pasta `Features/UI/Window/` só existia para segurar o manager.
- `WorldContext` também não tinha estado: era fachada sobre a árvore. `Streaming` fazia
  um `GetNodeOrNull` por acesso, e `Root` lia `Tree.CurrentScene`, o que é falso justamente
  na cena aberta sozinha.
- `DimensionContext` só guardava cache de nós que já estavam na árvore, e junto disso
  carregava mais quatro assuntos em 588 linhas.

### A direção entre nós: de fora para dentro

Nó de fora escreve no nó de dentro; o de dentro nunca lê o de fora. É o que deixa a cena
de um nível abrir sozinha e funcionar.

A `Dimension` lia `WorldStreaming.Current` em três pontos (semente em dois, autorado x
procedural num terceiro) e o `LightMap2D` num quarto (`TileStreamingEnabled`). Agora a
`Dimension` tem `Seed`, `Procedural` e `TileStreaming` como campos próprios com default, e
o `WorldStreaming` empurra por `PushConfig()` em cada ponto onde algum deles muda —
`SetWorldSeed`, `UseProceduralWorlds`, `UseAuthoredWorlds`, `DropWorlds`,
`SetStreamingEnabled`, `ResetTileStreaming` e o `_Ready`. O `_Ready` do `WorldStreaming`
roda depois do das dimensões (o Godot chama `_ready` de baixo para cima), então o registro
já está cheio quando ele empurra.

Aberta sozinha, a cena não recebe nada e fica com `Seed = 0`, `Procedural = true`,
`TileStreaming = false` — os mesmos valores que o `?.` com fallback dava antes.

`WorldSeed` virou `{ get; private set; }` e só muda por `SetWorldSeed`, porque escrever a
propriedade direto não empurraria nada e a property não pode esconder esse trabalho.

Estado de runtime que precisa ser lido por várias regiões em vários níveis continua sendo
caso de contexto — é o que a luz faz. A regra aqui é só sobre nó lendo nó.

### O contra-peso: quem segura nó morre com a cena

O critério sozinho responderia "sim" para o `WorldStreaming`, e a resposta é não. Ele
guarda mais estado fora da árvore que o `SaveContext` — `_unloaded`, `_dimensionOf`,
`_peers`, `WorldSeed`, `AuthoredWorlds`, as duas chaves de streaming.

Mas `_unloaded` é `Dictionary<long, Node2D>` de nós que **saíram** da árvore: o `Unload`
faz `RemoveChild` sem `QueueFree`, e o nó continua vivo, fora da árvore, preso só pela
referência do dicionário — é assim que uma entidade longe do player para de custar frame
sem perder estado. Em Godot, nó fora da árvore sem referência vaza. Quem libera é o
`_ExitTree`, que dá `QueueFree` em todos e limpa o dicionário.

Daí a segunda metade da regra:

> Se o estado fora da árvore são **referências a objetos da engine**, o dono precisa ser
> nó, para morrer com a cena.

Estático não tem esse gancho: dependeria de alguém lembrar de chamar um reset. É o mesmo
furo que `_pendingProfileByPeer` teve no `SessionContext` (ver adiante), só que ali o
custo de esquecer era dado velho, e aqui seria vazamento de nó.

Tick e RPC **não** entram nessa conta. Os dois pesam a favor de nó, mas fracamente: a
`Dimension` já recebe RPC e já é chamada por tick de fora. Foi o `_unloaded` que decidiu.
O `[SaveScene("world", …)]` confirma por outro lado: o `WorldStreaming` é o nó raiz que
o `WorldDocument.Write` serializa, parte do documento do mundo, não um serviço por fora.

## O que se perde, e a compensação

- **Validação de tudo no arranque.** O `Bootstrap` hoje confere as 130 entradas e
  lista todos os problemas de uma vez. Depois, cada tela resolve os próprios filhos
  em `_Ready` e falha ali — a falha fica na cena que você abriu, não num relatório
  global.
- **Limpeza pela árvore.** Estado estático sobrevive à troca de cena. Os contextos
  com estado de sessão (`_loadedOverworld`, `_worlds`, `_overlaysByDimension`, `Nodes`)
  precisam de um ponto único de reset, chamado no despawn do mundo e na desconexão.
  Metade disso já existe em `ResetState()` e `Detach()`.

## Posse do personagem (corrigido na migração)

`ApplyServerCharacterDelete` e `ApplyServerCharacterSelect` aceitavam um `characterId` de
qualquer peer sem conferir o dono, embora a lista fosse filtrada por `OwnerProfileId` — um
cliente conseguia apagar o personagem de outro ou entrar com ele. Agora ambos validam com
`OwnsCharacter`/`OwnsProfile`, cruzando o `senderId` com o perfil que o peer declarou no
pedido da lista.

## Como ficou

Quatro classes estáticas de contexto, com o sufixo `Context` e em pastas `Context/`:
`RouterContext`, `SaveContext`, `NetworkContext` e `SessionContext`.
`LightingManager`, `TileStreamingManager` e `LightMapManager` foram dissolvidos nos nós
(ver abaixo). `WorldStreaming` continua nó: é a raiz da cena do mundo.

O que não era contexto virou classe com o nome do que faz:

| Classe | Pasta | O que é |
| --- | --- | --- |
| `WindowUtilities` | `Utils/Window/` | cursor e fullscreen |
| `Players` | `World/Characters/Singletons/` | consulta e spawn de player e NPC |
| `EntityRecord` | `World/Entities/Singletons/` | formato do registro de entidade |
| `EntitySpawner` | `World/Entities/Singletons/` | spawn/despawn genérico, item no chão, prop |

`GameLoop` (`Features/World/Core/Singletons/GameLoop.cs`) é a ponte com a engine, e só
isso: `Tree` e `Multiplayer`.

### O nó resolve os próprios filhos

`DimensionContext` mantinha os caminhos `Main/World/Levels/…` e resolvia tudo a partir de
`/root` — o mesmo acoplamento do `Bootstrap`, num arquivo novo. Pior: o `Dimension`
perguntava a ele onde estavam os próprios filhos (`DimensionContext.ResolveLayer(DimensionId)`
descia de `/root` de volta até o nó que fez a pergunta).

Agora o `Dimension` se registra sozinho por `DimensionId` no `_EnterTree`, sai no
`_ExitTree`, e resolve `Layer`, `BaseLayer`, `Walls`, `Entities` e o container em
`ResolveChildren()`, chamado no `_Ready`. É o mesmo padrão de `Ui.Register` e de
`WorldStreaming.Current`. Quem precisa de outra dimensão usa `Dimension.Get(id)`.
Nenhum caminho absoluto sobrou.

**Contexto não tem tick nem regra de loop.** Intervalo e orçamento são do nó:

| Nó | Regra que guarda | O que faz |
| --- | --- | --- |
| `WorldStreaming` | intervalo de `EVALUATE_INTERVAL_SECONDS` (0,25 s) | chama `Evaluate()` de cada dimensão |
| `LightMap2D` | orçamento de `MAX_CHUNK_REBUILDS_PER_FRAME` (2) por frame | reconstrói os chunks de luz da própria dimensão |

O autosave é a exceção combinada: se agenda sozinho com `Tree.CreateTimer` rearmado.

### Contextos que deixaram de existir

`LightingManager` e `TileStreamingManager` foram dissolvidos: o estado dos dois era todo
chaveado por `dimensionId`, ou seja, um objeto por dimensão fingindo ser um só.

- A luz por chunk (carregados, sujos, overlays, em voo) virou campo do `LightMap2D`, um por
  dimensão. O dispatcher de GPU e o índice de blocos emissores, que são compartilhados,
  ficaram estáticos ali mesmo.
- O `LogicalLightWorld` também é da dimensão: virou campo do `Dimension`, criado sob demanda
  por `EnsureWorld()`. Autorado x procedural é de mundo e ficou no `WorldStreaming`. O
  `LightMap2D` usa o mundo da dimensão quando existe; sem sessão, monta o seu pelos tiles.
- O streaming de tile virou campo do `Dimension`: chunks carregados, mutações, peers por
  chunk, minimapa, carga/descarga e os RPCs. O nível de mundo — semente, liga/desliga,
  catch-up de peer e remoção de peer — ficou no `WorldStreaming`.
- As mutações agora moram no nó e morrem com ele; por isso o `LeaveWorld` grava
  (`SaveContext.SaveAll()`) antes de `SessionContext.DespawnWorld()`.

O que não era trabalho por frame saiu do frame: os sinais do `MultiplayerApi` são ligados
em `NetworkContext.EnsureBound()`, chamado onde o peer nasce (`CreateServer`/`JoinServer`);
as assinaturas da sessão são estáticas e vão no construtor de `SessionContext`, com o
`CloseRequested` ligado por `BindWindowClose()`; o autosave virou `Tree.CreateTimer`
rearmado; e o F11 voltou a ser `_UnhandledInput` no `ScreenUI`, com `SetInputAsHandled`.
O `LightMap2D` assina as camadas e os chunks da própria dimensão ao entrar em cena, em vez
de sondar a cada frame.

Os RPCs foram para os nós donos do estado:

| Antes | Agora | Nó |
| --- | --- | --- |
| `LoadChunkReceive` e `UnloadChunkReceive` | idem, sem `dimensionId` | `Dimension` |
| `ReceiveLightWorld` e `ReceiveBackgroundLight` | idem, sem `dimensionId` | `Dimension` |
| `SetWorldSeedReceive` | idem | `WorldStreaming` |
| `Spawn*Receive` e `DespawnReceive` | idem | `Dimension` |
| `ClearLayersReceive` | idem | `WorldStreaming` |
| `SessionContext` — 5 ações de personagem | idem | `CharacterSelectUI` |
| `SessionContext.CreateServerCharacterServerReceive` | idem | `CreateCharacterUI` |
| `SessionContext` — handshake de entrada | idem | `MultiplayerUI` |

Do lado do contexto, cada endpoint virou um `Apply*` comum. O handshake ficou na
`MultiplayerUI` (e não num nó de rede dedicado) porque é a tela que inicia a conexão.

`Ui` (`Features/UI/Common/Abstractions/Ui.cs`) é o registro de telas: `ScreenUI` se
registra no `_EnterTree` e sai no `_ExitTree`; quem precisa de outra tela usa
`Ui.Get<HudUI>()`. Cada tela resolve os próprios filhos em `ResolveChildren()`, chamado
no `_Ready` — as 118 entradas do registro antigo saíram.

`Bootstrap.cs`, `Game.cs`, `Managers.tscn` e o `WhenReady` foram removidos. A `Main.tscn`
tem só o `Ui`; a `StartUI` se abre sozinha no `_Ready`.

O reset de sessão tem dois pontos, os dois chamados por `SessionContext.LeaveWorld`:

- `SessionContext.ResetState()` limpa o estado estático da sessão — `CurrentWorldSave`,
  `PendingCharacter`, `CharacterMode`, `ServerCharacterSummaries` e os dois dicionários
  por peer. Sem isso, `_pendingProfileByPeer` sobrevivia à saída do mundo e a validação de
  posse (`OwnsProfile`) passava a cruzar o `senderId` com o perfil de uma sessão anterior,
  já que os ids de peer são reatribuídos.
- `SessionContext.DespawnWorld()` desmonta a árvore: `WorldStreaming.ResetState()`,
  `Dimension.ClearAllEntities()` e `Dimension.ClearAllLayers()`. A parte de interface é da
  interface: a `StartUI` ouve `SessionEnded`, fecha o HUD e zera o router.

## O que ficou verificado

- Build sem erro; os 3 avisos restantes são anteriores (`Hitboxes`).
- `Main.tscn` sobe e desenha a tela inicial.
- `Upsidedown.tscn` e `Overworld.tscn` abrem **sozinhas, com iluminação** — era o objetivo.
  Duas coisas separam quem é quem no `LightMap2D`: `DimensionId` é identidade e sai do nó
  `Dimension` pai (existe sempre); `Attached` diz se a sessão assumiu o mapa. Sem sessão o
  mundo lógico vem dos tiles pintados, mas os chunks de luz são reconstruídos do mesmo
  jeito. Conferido pelo nó `LightOverlay`: 32 sprites na Upsidedown, 24 na Overworld.
- Não verificado por falta de teste automatizado: partida real, join com dois processos,
  save/load e troca de dimensão.
