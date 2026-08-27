# Status: o que foi feito, o que falta

Índice do refactor de streaming, save e limpeza. Atualizado em 2026-08-27.

**Último commit:** `721eb44` (Save game to json). Há trabalho não commitado — ver seção 3.

---

## 1. Documentos

| arquivo | conteúdo |
|---|---|
| **status-implementacao.md** | este índice: o que está feito e o que falta |
| [contabilidade-refactor.md](contabilidade-refactor.md) | quanto código entrou e saiu de verdade: **+814 linhas** |
| [plano-implementacao.md](plano-implementacao.md) | as 9 implementações, com exemplo de cada; e o comparativo `Data` × sem `Data` (seção 10) |
| [arquitetura-streaming.md](arquitetura-streaming.md) | o raciocínio: por que chunk é a unidade das duas correntes, as medições do Godot |
| [streaming.md](streaming.md) | **como o streaming de tile e entidade funciona hoje** — o documento de referência |
| [managers-architecture-redesign.md](managers-architecture-redesign.md) | as regras de manager/system/entidade que valem no projeto |
| [node-registry-bootstrap.md](node-registry-bootstrap.md) | registro `Game` + `Bootstrap`, e a armadilha do `Reset()` |
| [fluxo-mundo.md](fluxo-mundo.md) | stack traces de entrada e saída de mundo |
| [save-e-sessao.md](save-e-sessao.md) | como o save funciona |
| [world-generation.md](world-generation.md) | geração de mundo, bioma, autotile — **seções 1-2 desatualizadas** |
| [diagramas/](diagramas/) | fontes Mermaid dos diagramas do FigJam |

---

## 2. Implementado

### ✅ Save em JSON (plano §7)

O save deixou de ser `.tres` e virou JSON puro, escrito pelo `GodotDictionaryParser` — o mesmo serializador que já trafega por RPC. Um formato só para disco e rede.

```json
{
	"$type": "dimension",
	"Chunks": [{ "$type": "chunk_entry", "ChunkCoordX": -2, "State": { ... } }]
}
```

- `$type` é id curto declarado pela classe (`[SaveType("prop")]`), resolvido por mapa montado uma vez por reflexão. **Sem factory, sem switch.**
- Trocou o `AssemblyQualifiedName` anterior, que carregava versão do assembly — inofensivo para RPC, mina para save.
- `Vector2` é serializado como `{"x":…, "y":…}` **no parser**: `Json.Stringify` com `Vector2` cru vira a string `"(12.5, -3.0)"` e `AsVector2()` na volta devolve `Zero`, sem erro.
- Regra que ficou: **nada de inteiro acima de 2^53** em campo de save.

**Verificado em runtime:** seed, enum, bool, int, arrays aninhadas, `PlayerData` aninhado e os dois `Vector2` — round-trip idêntico.

Arquivos: `SaveStorage`, `GodotDictionaryParser`, `SaveTypeAttribute` (novo), `SavesConstants`, e 11 resources anotados.

### ✅ `CoordinateUtilities` (plano §1)

43 linhas. `WorldToCell`, `CellToChunk`, `WorldToChunk`, `ChunkToCell`, `ChunkDistance` (Chebyshev — é a métrica certa porque o raio de carga é quadrado, não círculo).

Eram métodos privados do streaming; viraram utility porque o streaming de entidade vai precisar das mesmas contas.

### ✅ Queries de player no `WorldManager` (plano §2)

- `GetAllPlayers()` e `GetPlayersInDimension(dimensionId)` novos
- `FindPlayerByPeerId` passou a reusar `GetAllPlayers`
- `DeathScreenUI` e `SkillTreeUI` perderam o wrapper `FindLocalPlayer`
- **Bug corrigido:** `GetLocalPlayer` fazia 3 `GD.Print` por chamada, e `DeathScreenUI._Process` chama isso **todo frame** enquanto não há player. Eram ~180 linhas de log por segundo em menu.

`HudUI` e `FullscreenMapUI` mantiveram o método — eles fazem trabalho real (re-assinatura de eventos, wiring do `MapView`), não são wrapper.

### ✅ `MinimapSystem` (plano §3)

97 linhas. Mapa de descoberta por dimensão, saiu de dentro do streaming de tile.

Não é node e não conhece rede — é system. A UI continua falando com o manager, que fala com o system (`GetDiscoveredTexture` virou fachada de 3 linhas).

### ✅ `TileStreamingManager` (plano §4)

`ChunkStreamingManager` → `TileStreamingManager`. O nome passou a dizer a responsabilidade em vez do mecanismo: ele só mexe em célula de tilemap.

Rename propagado para `Managers.tscn`, `Game.cs` (path `/root/Main/Managers/TileStreamingManager`) e 10 arquivos.

**Eventos novos**, que sustentam o resto do plano:

```csharp
public event System.Action<string, Vector2I> ChunkLoaded;
public event System.Action<string, Vector2I> ChunkUnloaded;
```

Disparados nos 4 pontos — carga e descarga, lado servidor e lado cliente.

---

## 3. Estado do repositório

Build limpo (0 erros), smoke headless passando (`118 nodes estaticos registrados`).

**Não commitado:**

```
novos:      Game/Utils/Coordinates/CoordinateUtilities.cs
            Game/Features/World/Chunks/Systems/MinimapSystem.cs
            .dev/plano-implementacao.md
            .dev/status-implementacao.md

renomeado:  ChunkStreamingManager.cs -> TileStreamingManager.cs

alterados:  WorldManager, Game.cs, Managers.tscn, SessionManager, SaveManager,
            NetworkManager, TerrainLayer, Player, ChunkGridOverlay,
            MinimapUI, DeathScreenUI, SkillTreeUI
```

---

## 4. Três estimativas do plano que não se confirmaram

Medido depois de implementar. Registrado aqui porque o plano original está corrigido, mas o erro é instrutivo.

**"`TileStreamingManager`: 623 → ~250 linhas."** Deu **600**. Saiu o que era de fora (minimapa, conversões: ~90 linhas); o resto é trabalho legítimo de streaming:

```
162  Load/Unload     126  Evaluation     61  RPC
 42  Persistência     42  Godot impl     33  Catch-up
```

O arquivo nunca foi "5 responsabilidades de 120 linhas cada".

**"`RecordMutation` vai pro `TerrainLayer`."** Não vai. `ApplyChunkMutation` — a parte que sabe o que é bloco — **já está lá**. `RecordMutation` só faz append na lista de mutações do chunk, que é estado do streaming e é o que vai pro save. Estava no lugar certo.

**"`FindLocalPlayer` duplicado idêntico em 4 telas, ~40 linhas."** Falso, e o erro foi metodológico: grep de assinatura em vez de corpo. Dois eram wrapper de uma linha; os outros dois fazem trabalho próprio. E o lookup já passava pelo `WorldManager` nos quatro.

---

## 5. Implementado (parte 2)

### ✅ Serialização de node (plano §5)

```csharp
public static Dictionary ToDictionary(GodotObject source)         // era Resource
public static void ApplyTo(GodotObject target, Dictionary dict)   // novo: popula, não cria
public static bool HasSerializableFields(GodotObject source)      // discriminador do streaming
```

Node serializado **não leva `$type`** — quem reconstrói node é a cena, não o `Activator`. `$type` continua só para `Resource` puro.

O parser também passou a serializar `Array<Dictionary>` (a lista de records), que antes estourava em "tipo não suportado".

### ✅ Spawn genérico (plano §6)

Um caminho para `Prop` e `WorldItem`:

```csharp
public Node2D Spawn(Dictionary record)
public Node2D SpawnRequest(Dictionary record, long targetPeerId = 0)
[Rpc] public void SpawnReceive(Dictionary record)
public void DespawnRequest(long instanceId)   /   [Rpc] DespawnReceive
public Node2D FindByInstanceId(long instanceId)
public Dictionary BuildRecord(Node2D node)
```

`IHasInstanceId` dá a identidade — infraestrutura, não payload. O nó recebe nome `E{instanceId}`, e é por ele que `FindByInstanceId` acha em qualquer dimensão.

**RPCs do `DimensionManager`: 6 → 5.** `SpawnWorldItemReceive` + `SpawnPropBroadcast` + `RemoveWorldItemReceive` viraram `SpawnReceive` + `DespawnReceive`.

**Player e NPC ficaram de fora, de propósito:** são conteúdo de sessão, e o nome do nó deles (`Player{peerId}`) é caminho de RPC — mudar quebra replicação.

**Custo honesto:** `DimensionManager` foi de **535 para ~640 linhas**. A infraestrutura genérica (~180 linhas) é maior que o código específico que substituiu (~70). O ganho é para frente: entidade nova precisa de **zero método e zero RPC** — antes eram 3 a 5 métodos e 1 a 2 RPCs.

### ✅ `EntityStreamingManager` (plano §8)

Assina `ChunkLoaded`/`ChunkUnloaded` e faz pela entidade o que o tile já faz.

```
_records   : dim -> chunk -> List<Dictionary>    o que o save conhece
_live      : instanceId -> Node2D                o que está na árvore
_recordById: instanceId -> Dictionary            atalho
```

- **Registro automático:** `Prop` e `WorldItem` chamam `Register`/`Unregister` no `_EnterTree`/`_ExitTree`. Quem entra na árvore está registrado, venha de onde vier.
- **Restaurado × nascido agora:** a identidade viaja no próprio node (`InstanceId != 0` e record já conhecido = restaurado). Sem estado ambiente — imune a `CallDeferred`.
- **`_ExitTree` só faz membership.** Captura acontece em `OnChunkUnloaded`; teardown é anunciado por `BeginTeardown()` antes do `DespawnWorld` liberar o `World`.
- **Descarregar ≠ esquecer:** os dois matam o node; só `Forget` mexe no registro. Prop quebrado e item recolhido chamam `Forget`.

`RestoreProps` saiu do caminho procedural — quem materializa prop agora é o streaming. Continua no mundo desenhado à mão, que não tem streaming.

### ✅ Correções de streaming (plano §9)

**9.1 — entidade que se move.** `WorldItem` é `CharacterBody2D`: cai e escorrega. O `_Process` do `EntityStreamingManager` reavalia o chunk das entidades vivas a cada tick e re-chaveia quem mudou. Sem isso, o item morre quando o chunk velho descarrega — com o player do lado — e reaparece na posição antiga.

**9.2 — decisão por peer.** `SendPendingChunksToPeers` compara quem precisa do chunk com quem já recebeu. Antes, chunk já pintado por causa de outro player nunca chegava em quem chegou depois — o filtro olhava o `loaded` global do servidor.

**9.3 — chunk vazio não vai pro arquivo.** `ExportState` pula chunk sem mutação. Medido antes: 78 chunks para 15 mutações.

**9.4 — catch-up filtrado por raio.** `CatchUpPeer(peerId, aroundPosition)` manda só o que está a ≤ `UNLOAD_RADIUS_CHUNKS` do spawn do peer, em vez de todo chunk carregado das duas dimensões.

### Verificação em runtime

Probe pendurado no `Main.tscn`, depois removido:

```
spawn genérico   : Portal instanciado, nome E4242, PropId e posição aplicados
lookup           : FindByInstanceId achou o mesmo nó
duplicata        : recusada
descarregar      : nó morreu, record ficou (1)
recarregar       : nó voltou, posição e PropId corretos
esquecer         : record sumiu (0)
round-trip JSON  : record sobreviveu ao arquivo, com scene/PropId/posição
```

Smoke headless: **119 nós estáticos** (era 118 — o `EntityStreamingManager` novo).

---

## 5b. Falta

**Investigar `MultiplayerSpawner`.** O projeto tem 0 ocorrências. Se cobrir o caso, o `SpawnReceive` do `DimensionManager` some e o registro automático cobre o ciclo inteiro. É a pergunta mais barata com o maior efeito no que sobrou.

**Testar com 2 peers.** Nada de multiplayer foi exercitado. E agora a superfície é maior: `$type` mudou de formato, o spawn de prop/item mudou de RPC, e o catch-up mudou de assinatura.

**`CollectProps` ainda monta `PropSaveData` na mão** para o `WorldSaveData.Props` — usado só pelo mundo desenhado à mão. Com o streaming, esse caminho ficou redundante para mundo procedural.

## 6. Dívidas conhecidas, fora do plano

- **Multiplayer nunca foi testado com 2 peers.** Os 8 RPCs de personagem mudaram de nó duas vezes, o de bloco foi pro `TerrainLayer`, o `UseItemAt` é novo, e o `$type` mudou de formato nas duas pontas.
- `NetworkManager.Disconnect` chama `CallDeferred("RespawnLocalSoloPlayer")` por nome; o método passou a exigir `CharacterSaveData` e o compilador não pega. **Quebra em runtime ao desconectar.**
- `CharacterSelectUI.OnBackPressed` não tem `return` no ramo `OwnWorld`: abre `WorldSelectUI` e em seguida cai no `Disconnect()` + abre `MultiplayerUI`.
- `PortalSaveData` e `MigrateLegacyPortals` viraram código morto com o JSON — existiam porque o `.tres` gravava o caminho do script.
- `SaveManager` tem 3 regions vazias.
- `SaveStorage.CachedProfile` é estático sem invalidação.
- `NPC : Player` faz o NPC herdar os 22 RPCs do Player sem precisar de nenhum.
