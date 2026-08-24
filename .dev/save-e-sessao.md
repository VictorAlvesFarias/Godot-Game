# Sessão, save e o ciclo com a rede

Levantamento do estado atual e proposta. Escrito depois da extração do `SaveStorage`. **Nada foi alterado no código por causa deste documento.**

Estado medido:

| peça | linhas | RPCs |
|---|---:|---:|
| `SaveManager` | 550 | 8 |
| `NetworkManager` | 373 | 0 |
| `WorldManager` | 245 | 0 |
| `SessionManager` | 75 | 0 |
| `SaveStorage` (system) | 372 | 0 |

---

## Parte 1 — O problema de posse

### 1.1 A sessão tem três donos parciais

| dado | onde está | quem mais usa |
|---|---|---|
| `PendingWorld`, `PendingCharacter` | `SessionManager` | `SaveManager` (7 refs) |
| `CharacterMode`, `_peerCharacters`, `_pendingProfileByPeer` | `SaveManager` | — |
| `CurrentWorldSave` | `WorldManager` | `SaveManager` (**25 refs**) |

O desequilíbrio é medível: quem declara `CurrentWorldSave` lê 4 vezes; quem não declara lê 25. E o `SessionManager`, que tem o nome do assunto, ficou com 75 linhas e sem o estado da sessão.

Consequência prática: pra responder "em que mundo estou, com que personagem, em que modo", é preciso perguntar a três objetos. Toda regra que combine esses três dados nasce espalhada.

### 1.2 O ciclo `SaveManager` ↔ `NetworkManager`

```
SaveManager    → NetworkManager   5 pontos
NetworkManager → SaveManager      2 pontos
```

| direção | onde | por quê |
|---|---|---|
| N→S | `Disconnect()` → `PersistBeforeLeaving()` | antes de fechar a conexão, salvar |
| N→S | `OnConnectedToServer()` → `RequestJoinInfo()` | ao conectar, perguntar o modo de personagem |
| S→N | `_Ready` → `PeerLeft +=` | saber quando um peer cai, pra persistir o personagem dele |
| S→N | `SelectCharacter` → `IsConnected()` | decidir entre mandar pro servidor ou entrar no mundo |
| S→N | 3× `*ServerReceive` → `FinishPeerJoin` | personagem fechado: spawnar o player e sincronizar chunk |

**A causa não é acoplamento acidental — é um conceito sem dono.** O join é uma sequência única que alterna de assunto a cada passo:

```
1. cliente conecta                     rede
2. pergunta o modo de personagem       sessão
3. servidor responde o modo            sessão
4. cliente escolhe/cria personagem     sessão
5. servidor recebe e valida            sessão
6. spawna o player, sincroniza chunk   rede
7. peer cai: persiste o personagem     sessão + disco
```

Como ninguém é responsável pela sequência, os dois participantes se chamam mutuamente pra empurrá-la adiante. É a mesma forma do antigo `ChunkStreaming ↔ World`: lá o assunto órfão era "o estado do mundo", aqui é "a entrada no mundo".

### 1.3 O `SaveManager` acumulou três assuntos

| assunto | métodos | linhas |
|---|---:|---:|
| rede (RPC de personagem) | 14 | 183 |
| política de autosave | 7 | 112 |
| decisão/sessão | 9 | 93 |
| fachada do `SaveStorage` | 7 | 28 |

Disco já saiu (virou `SaveStorage`). O que sobra tem três razões independentes pra mudar.

---

## Parte 2 — Como o save funciona hoje

### 2.1 O que é salvo

| coisa | mecanismo | onde vai parar |
|---|---|---|
| **terreno** | não salva tile: salva **mutações por chunk** (`ChunkStreamingManager.ExportState`), reaplicadas sobre o terreno regerado pelo seed | `dimension_<id>.tres` |
| **props (portais)** | `DimensionManager.CollectProps()` varre os filhos das dimensões; cada `Prop` tem `ToSave()` | `WorldSaveData.Props` |
| **personagem local** | `PendingCharacter.Data = localPlayer.Data` → `SaveLocalCharacter` | `characters/<id>.tres` |
| **personagens de peers** | `_peerCharacters` + `player.Data`, por modo (servidor ou backup) | pasta do mundo / backup |
| **meta do mundo** | nome, seed, modo, `IsProcedural`, autosave, timestamps | `world.tres` |

### 2.2 O que NÃO é salvo

- **`WorldItem`** (item no chão) — zero código de persistência. Dropou e saiu do mundo, perdeu.
- **`NPC`** — idem; o `NPC_Dummy` é recriado por código a cada entrada.
- Qualquer entidade futura (inimigo, baú, plantação) nasce fora do save por padrão.

### 2.3 O mecanismo, e por que ele não escala

`SaveCurrentWorld()` é um método central que **sabe o nome de cada coisa** e puxa de três managers:

```csharp
SaveDimensionState(... ChunkStreamingManager.ExportState(OVERWORLD) ...)
SaveDimensionState(... ChunkStreamingManager.ExportState(UPSIDEDOWN) ...)
CurrentWorldSave.Props = DimensionManager.CollectProps();
SaveWorldMeta(CurrentWorldSave);
SaveOwnLocalCharacter();
SaveRemotePeerCharacters();
```

Três problemas nisso:

1. **Toda entidade nova exige editar este método.** Salvar `WorldItem` significa mexer no `SaveManager`, não no `WorldItem`.
2. **É push, não pull.** O `SaveManager` conhece a estrutura interna de quem ele salva: sabe que existe `ExportState`, que prop vem do `DimensionManager`, que personagem vem do player local.
3. **`Prop.ToSave()` já existe e o autosave não usa.** Quem chama é o `CollectProps`. Metade do contrato certo já está escrita, mas não há um lugar que a consuma de forma genérica.

---

## Parte 3 — Formulação

Três frentes **independentes**. Podem ser feitas em qualquer ordem, e cada uma tem valor sozinha. A ordem sugerida no fim é por custo/benefício, não por dependência técnica.

O princípio que costura as três, e que saiu das correções ao longo da discussão:

> Dependência não se elimina, se **inverte**. Quem sabe da regra é quem é dono do dado; quem não é dono **notifica** em vez de perguntar.

---

### Frente A — Quebrar o ciclo `Save ↔ Network` (2 linhas)

**Problema:** 5 chamadas `Save → Network` e 2 `Network → Save`. As duas de volta são o que fecha o ciclo.

**Mudança:** o `NetworkManager` passa a só notificar.

| hoje | depois |
|---|---|
| `Disconnect()` chama `SaveManager.PersistBeforeLeaving()` | emite `Disconnecting`; o save assina |
| `OnConnectedToServer()` chama `SaveManager.RequestJoinInfo()` | já emite `ConnectionSucceeded` **na linha seguinte**; o save assina |

O padrão já existe no próprio arquivo (`PeerLeft`, `ConnectionSucceeded`), e o segundo caso é literalmente mover a chamada pra dentro de um `+=`.

**Resultado:** grafo acíclico. Sobram só as 5 de `Save → Network`, uma direção só. Nenhum método muda de lugar, nenhum caminho de RPC muda.

**Risco:** baixo. Só a ordem de execução muda (o assinante roda depois do emissor), e nos dois casos isso é indiferente.

---

### Frente B — Um dono para a sessão

**Problema:** `CurrentWorldSave` está no `WorldManager` (4 leituras próprias, 25 externas), `CharacterMode`/`_peerCharacters`/`_pendingProfileByPeer` estão no `SaveManager`, `PendingWorld`/`PendingCharacter` estão no `SessionManager` (75 linhas).

**Mudança:** todo estado de sessão vai pro `SessionManager`. E, pela regra que já vale no resto do projeto — **o RPC mora com o dono do estado** —, os 8 RPC de personagem vão junto.

| peça | fica com | some dela |
|---|---|---|
| `SessionManager` | `CurrentWorldSave`, `CharacterMode`, personagens por peer, `Pending*`, os 8 RPC, a condução do join | — |
| `SaveManager` | **quando** salvar e **o quê** coletar; delega o disco ao `SaveStorage` | RPC, estado de sessão |
| `WorldManager` | a cena: instanciar/liberar `World.tscn`, spawn do player local, lookup | `CurrentWorldSave` |
| `NetworkManager` | o canal; notifica, não pergunta | nada (já feito na Frente A) |

**Por que isso resolve, e não só reorganiza:** o fluxo de entrada ganha dono. Hoje ninguém é responsável pela sequência do join, e por isso os participantes se chamam mutuamente. Com o `SessionManager` conduzindo, `Save` e `Network` param de precisar um do outro — a Frente A vira consequência em vez de remendo.

**Risco:** médio-alto. Muda o caminho de nó dos 8 RPC (`Managers/SaveManager` → `Managers/SessionManager`). **Exige teste host + cliente.**

---

### Frente C — Save e load por lista de `Resource` registrada

**Problema:** `SaveWorld()` sabe o nome de cada coisa e puxa de dois managers; `WorldItem` e `NPC` não são salvos; `Prop.ToSave()` existe e **ninguém chama** — o `CollectProps` monta o `PropSaveData` na mão, enfiando a mão na entidade.

**Desenho:** o manager guarda uma **lista de `Resource`**. Quem cria a entidade registra o `Data` dela; quem destrói desregistra. Sem interface, sem varredura de árvore, sem o manager conhecer entidade.

```csharp
// no manager
private readonly List<Resource> _registrados = new();

public void Register(Resource data);
public void Unregister(Resource data);
```

```csharp
// no DimensionManager, que ja e quem cria tudo que vive no mundo
public void SpawnWorldItem(WorldItem item, string dimensionId)
{
    ResolveParent(dimensionId)?.AddChild(item);

    Game.Managers.SaveManager.Node.Register(item.Data);
}
```

Salvar = serializar a lista. O manager não pergunta nada a ninguém, e a entidade não conhece o manager.

#### C.1 Por que não é anêmico

A anemia era o manager **ler e escrever o estado da entidade**. Aqui ele não conhece a entidade: só tem uma referência ao `Resource`, que é o estado vivo que a própria entidade muta durante o jogo. Ninguém enfia a mão em ninguém.

`PlayerData` já é assim — `CurrentHealth`, `EquippedItemId`, `Inventory` são o estado real, não uma cópia montada na hora de salvar.

#### C.2 Requisito: o `Data` tem que bastar sozinho

Como a lista é só de `Resource`, o manager **não tem como perguntar nada ao nó**. Tudo que for necessário pra recriar a entidade tem que estar dentro do próprio dado:

| resource | estado | posição | dimensão | qual cena |
|---|:--:|:--:|:--:|:--:|
| `PropSaveData` | ✓ | ✓ | ✓ | ✓ (`PropId`) |
| `ItemData` | ✓ | ✗ | ✗ | ✗ |
| `PlayerData` | ✓ | ✗ | ✗ | ✗ |

O `PropSaveData` é o modelo — e não por acaso é o único que hoje volta do save. Posição, dimensão e id de cena precisam entrar nos outros dois.

**Consequência direta:** posição mora no nó e muda o tempo todo. Alguém tem que manter `Data.Position` atualizado, porque na hora do save não há a quem perguntar. Duas saídas:

- a entidade escreve `Data.PositionX/Y` quando se move (custo irrisório, mas é trabalho constante para um evento raro);
- o manager emite um sinal `Saving` antes de serializar e quem tem estado fora do `Data` se atualiza — mantém o comportamento na entidade e não paga por frame.

#### C.3 O ponto fraco: desregistro

É o custo real desta variante, e não tem como contornar por checagem: **um `Resource` não sabe se o nó dele morreu.** Com o nó na lista dava pra testar `IsInstanceValid`; com o dado, não. Se o unregister falhar, o save grava fantasma — entidade que não existe mais volta no próximo load.

Onde isso tem que estar coberto:

| morte | onde desregistrar |
|---|---|
| `Prop.ProcessBreak` / `BreakBroadcast` | no próprio `Prop`, antes do `QueueFree` |
| `RemoveWorldItemReceive` | no `DimensionManager`, junto do `QueueFree` |
| player de peer que caiu (`OnPeerDisconnected`) | junto do `QueueFree` |
| `LeaveWorld` (mata tudo de uma vez com `world.QueueFree()`) | `SaveManager.ResetRegistry()` — limpa a lista inteira |

O último é o que salva o dia: como a saída de mundo libera tudo junto, limpar a lista ali cobre qualquer esquecimento pontual. Mas dentro de uma sessão, cada morte precisa do seu.

#### C.4 O load

Salvar fica trivial; carregar é onde mora a decisão. O caminho de volta **já existe e funciona**: `SpawnWorldItemReceive` instancia a cena, atribui `Data` e adiciona na árvore — é exatamente o que o restore precisa fazer.

Então o load é **puxado por quem cria**, não empurrado pelo save:

```
DimensionManager.RestoreFrom(save)
├─ pede a lista de dados ao SaveManager
├─ para cada um: instancia a cena que o proprio dado indica
├─ atribui o Data
└─ AddChild + Register
```

O `SaveManager` nunca instancia nada, e a dependência continua numa direção só.

#### C.5 O segundo portão

`Game.IsReady` cobre "os nós estáticos existem". Falta "o mundo carregou e o save foi aplicado". A entidade restaurada nasce com `ProcessMode = Disabled` e é liberada quando o `Data` foi atribuído — assim "não processar antes de carregar" deixa de ser disciplina e vira consequência.

> Cuidado herdado: o `Game.Reset()` **não pode** limpar a fila do `WhenReady`. O `_Ready` das entidades roda antes do `Bootstrap`, e limpar ali descarta tudo em silêncio — foi o que deixou o botão Jogar sem efeito.

#### C.6 Por que não interface

Foi considerado e descartado. Uma `ISavable<TData>` daria tipagem e um verbo (`Save()`/`Restore()`), mas:

- obriga um marcador não-genérico junto, porque `is ISavable<>` não compila (`CS7003`), e o manager precisa iterar sem saber o `T`;
- o manager volta a conhecer a **entidade**, não só o dado — que é de onde vinha a sensação de modelo anêmico;
- não resolve nada que a lista de `Resource` não resolva, desde que o `Data` baste sozinho (C.2).

Registro fica: `[Export]` de membro genérico numa base genérica **não compila** no Godot 4.6 (`GD0102`); interface genérica implementada por classe concreta compila. Testado neste projeto, caso a decisão seja revista.

### Ordem sugerida

| # | passo | frente | risco | ganho |
|---|---|---|---|---|
| 0 | **testar multiplayer como está** | — | — | os 8 RPC já mudaram de nó e nada foi verificado; qualquer passo daqui piora o diagnóstico se algo já estiver quebrado |
| 1 | inverter as 2 chamadas em evento | A | baixo | grafo acíclico, 2 linhas |
| 2 | lista de `Resource` + `Register`/`Unregister`, com `Prop` como piloto | C | baixo | `Prop` já tem `ToSave()` morto esperando uso; e o `CollectProps` some |
| 3 | completar `ItemData`/`PlayerData` (posição, dimensão, cena) e migrar `WorldItem` | C | baixo | passa a salvar o que hoje se perde |
| 4 | load puxado pelo `DimensionManager` | C | médio | tira do `SaveCurrentWorld` o conhecimento de quem existe |
| 5 | estado de sessão → `SessionManager` | B | médio | zera as 25 referências cruzadas de `CurrentWorldSave` |
| 6 | RPC de personagem + condução do join → `SessionManager` | B | alto | resolve a causa do ciclo; **muda caminho de RPC** |
| 7 | segundo portão (`ProcessMode` até o restore) | C | baixo | fecha a corrida entre spawn e restore |

### O que se espera de resultado

| | hoje | depois |
|---|---:|---|
| ciclos no grafo | 1 (`Save ↔ Network`) | 0 |
| refs do `SaveManager` a outros managers | 44 | ~10 |
| donos do estado de sessão | 3 | 1 |
| entidades persistidas | 3 tipos | qualquer uma que implemente o contrato |
| custo de salvar algo novo | editar `SaveWorld` | registrar o `Data` no spawn |

---

## Pendências que não são de arquitetura

- **Nada disso foi testado com dois peers.** Os 8 RPC de personagem já mudaram de nó uma vez (`NetworkManager` → `SaveManager`); o passo 6 mudaria de novo. Testar **antes** de mexer mais.
- `SaveStorage.CachedProfile` é estado estático sem invalidação.
- `SaveManager` referencia `Game.Ui.CharacterSelectUI` em 3 pontos — acoplamento com uma tela específica, não com "UI".
