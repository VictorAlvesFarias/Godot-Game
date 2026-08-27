# Contabilidade do refactor: quanto código entrou, quanto saiu

Medido em 2026-08-27, contra o commit `721eb44`. O refactor foi vendido como "reduzir complexidade". Os números dizem outra coisa.

---

## 0. Depois da passada de remoção (2026-08-27)

| | antes da limpeza | depois |
|---|---:|---:|
| removidas de arquivos existentes | −230 | **−386** |
| saldo em arquivos existentes | +303 | **+122** |
| saldo total | **+814** | **+687** |
| API pública do `DimensionManager` | 51 (era 43) | **46** |

**O que foi apagado:**

| item | linhas |
|---|---:|
| `PortalSaveData.cs` (shim do `.tres`) | 15 |
| `MigrateLegacyPortals` + chamada | ~25 |
| `WorldSaveData.Props` e `.Portals` | ~12 |
| `DimensionManager.CollectProps` | 28 |
| `DimensionManager.RestoreProps` | 26 |
| `DimensionManager.FindWorldItem` (wrapper) | ~13 |
| fachada `SpawnWorldItemRequest(WorldItem, peerId)` | ~10 |
| loop manual de catch-up de item no `SessionManager` | ~9 |
| regions vazias no `SaveManager` | ~9 |
| `Player.FindByPeerId` | 5 |
| `DimensionManager.ResolveContainer` | 7 |
| `RouterManager.CloseAll` | 17 |
| `SessionManager.ForgetAllPeers` | 6 |
| `TerrainLayer.ReconnectForeignBorderDependent` (wrapper síncrono) | 5 |
| **total** | **~187** |

**Um bug encontrado durante a limpeza:** `EntityStreamingManager.OnChunkLoaded` chamava `Dimensions.Spawn` (local) em vez de `SpawnRequest` — **entidades nunca eram replicadas para os peers**. Corrigido. Junto entrou `EntityStreamingManager.CatchUpPeer`, que substituiu o loop manual de `WorldItem` no `FinishPeerJoin`.

**Falsos positivos filtrados:** a varredura acusou 41 métodos públicos "sem chamador". A maioria é RPC chamado por `nameof`, handler ligado com `+=`, ou `override` que o engine chama. Só 5 eram mortos de verdade.

O saldo continua positivo. A diferença é que agora **não há caminho duplicado**: uma persistência de entidade, um catch-up, uma API de spawn.

---

## 1. O total

| | linhas |
|---|---:|
| adicionadas em arquivos existentes | +533 |
| removidas de arquivos existentes | −230 |
| arquivos novos | +511 |
| **saldo** | **+814** |

Removi **10 linhas líquidas** de código que já existia (os dois wrappers `FindLocalPlayer`). Todo o resto do saldo negativo veio de reescrita interna, não de apagamento.

---

## 2. Por arquivo

| arquivo | saldo |
|---|---:|
| `EntityStreamingManager` (novo) | +360 |
| `DimensionManager` | +110 |
| `MinimapSystem` (novo) | +97 |
| `GodotDictionaryParser` | +68 |
| `TileStreamingManager` | **+48** |
| `CoordinateUtilities` (novo) | +43 |
| `WorldManager` | +37 |
| `WorldItem` | +15 |
| `IHasInstanceId` (novo) | +11 |
| `SaveManager` | +10 |
| `Prop` | +9 |
| `Game.cs` | +7 |
| `DimensionSaveData` | +5 |
| `Managers.tscn` | +4 |
| `SessionManager`, `NetworkManager`, `Player`, `TerrainLayer`, `MinimapUI`, `ChunkGridOverlay` | 0 |
| `SkillTreeUI` | −5 |
| `DeathScreenUI` | −5 |

---

## 3. Separando funcionalidade de "limpeza"

### Capacidade que não existia — preço legítimo

| item | linhas |
|---|---:|
| `EntityStreamingManager` | 360 |
| correções de streaming 9.1–9.4 | ~60 |
| `IHasInstanceId` | 11 |
| **subtotal** | **~431** |

Streaming de entidade não existia. Isso é preço de funcionalidade nova, não de refactor.

### O que era pra ser limpeza

| item | linhas |
|---|---:|
| `DimensionManager` | +110 |
| `MinimapSystem` | +97 |
| `GodotDictionaryParser` | +68 |
| `TileStreamingManager` | +48 |
| `CoordinateUtilities` | +43 |
| `WorldManager` | +37 |
| `WorldItem` / `Prop` / resto | +30 |
| **subtotal** | **~433** |

**433 linhas adicionadas em nome de reduzir complexidade.** É o número que condena o refactor.

---

## 4. O caso mais claro: `TileStreamingManager`

O objetivo declarado era 623 → ~250. Resultado: **cresceu 48 linhas**.

| movimento | linhas |
|---|---:|
| saiu: minimapa (`RecordDiscovered`, `GetDiscoveredTexture`, 2 campos) | −60 |
| saiu: conversões de coordenada | −15 |
| entrou: eventos `ChunkLoaded`/`ChunkUnloaded` | +10 |
| entrou: `SendPendingChunksToPeers` (correção 9.2) | +40 |
| entrou: catch-up com raio, `ExportState` filtrado (9.3, 9.4) | +30 |
| entrou: comentários e fachada do minimapa | +40 |
| **saldo** | **+45** |

As correções de bug se justificam sozinhas — mas **não são "reduzir complexidade"**, e eu misturei as duas coisas no mesmo item do plano.

---

## 5. O erro que se repete

Quatro previsões erradas, todas na mesma direção:

| previsão | resultado |
|---|---|
| `TileStreamingManager` 623 → ~250 | 600, depois 648 |
| `DimensionManager` 535 → ~310 | 645 |
| `FindLocalPlayer`: ~40 linhas duplicadas | 10 linhas, 2 wrappers |
| total do refactor: **−236 linhas** | **+814 linhas** |

O padrão: **prevejo redução onde vou fazer extração.**

Extrair não remove código — move e adiciona indireção. `MinimapSystem` é mais coeso que o bloco de onde saiu, mas são 97 linhas novas contra ~60 removidas do outro lado. `CoordinateUtilities` são 43 linhas contra ~15 extraídas.

Reduzir de verdade é **apagar**: método morto, caminho não usado, caso especial que virou geral. Disso fiz pouco. Mesmo o item que mais parecia apagamento — 18 métodos de spawn → 4 — trocou ~70 linhas específicas por ~180 genéricas.

---

## 6. O que ainda dá pra apagar de verdade

Aqui o saldo inverte, porque é remoção, não movimentação.

| candidato | linhas | por quê pode sair |
|---|---:|---|
| métodos de spawn de Player + NPC | 89 | absorvíveis pelo caminho genérico, se resolvido o acoplamento do nome do nó com o path de RPC |
| `CollectProps` | 28 | redundante: quem coleta agora é `EntityStreamingManager.ExportState` |
| `RestoreProps` | 26 | redundante no mundo procedural; só o mundo desenhado à mão usa |
| `PortalSaveData` | 15 | shim que só existia porque o `.tres` gravava o caminho do script — com JSON, não faz mais nada |
| 3 regions vazias no `SaveManager` | ~9 | resíduo de recorte |
| **subtotal certo** | **~167** | |
| 582 métodos com ≤ 2 chamadas (levantamento inicial) | ? | nunca revisitados; a maioria é falso positivo, mas não sei quantos |

Com os ~167 confirmados, o saldo de "limpeza" cai de +433 para ~+266. Ainda positivo.

---

## 7. Conclusão honesta

O refactor entregou:

- streaming de entidade, que não existia
- 4 bugs de streaming corrigidos, dois deles de dessincronização em multiplayer
- save em JSON com tipo estável
- um caminho de spawn em vez de 18 métodos
- 1 RPC a menos no `DimensionManager`

E custou **+814 linhas**.

Chamar isso de "redução de complexidade" foi errado. O que houve foi **troca de complexidade**: menos casos especiais, mais infraestrutura genérica. Isso se paga quando a terceira, quarta e quinta entidade entrarem sem escrever método nem RPC — mas hoje, com duas entidades, é prejuízo.

A parte que você pediu originalmente — método duplicado, morto, criado à toa — foi a que menos aconteceu.
