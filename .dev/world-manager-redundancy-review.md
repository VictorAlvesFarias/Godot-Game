# Redundâncias entre `WorldManager` e `ChunkStreamingManager`

Levantamento dos métodos/padrões duplicados entre `Features/World/Core/Managers/WorldManager.cs` e `Features/World/Chunks/Managers/ChunkStreamingManager.cs`. Nenhuma mudança aplicada ainda — só o mapeamento.

---

## 1. `ResolveDimensionParent` — duplicado de verdade

- `ChunkStreamingManager.cs`:
  ```csharp
  private Node2D ResolveDimensionParent(string dimensionId)
  {
      return dimensionId == ChunkStreamingConstants.OVERWORLD_ID ? WorldManager?.OverworldParent : WorldManager?.UpsidedownParent;
  }
  ```
- `WorldManager.cs`:
  ```csharp
  private Node2D ResolveDimensionParent(string dimensionId)
  {
      return dimensionId == ChunkStreamingConstants.OVERWORLD_ID ? OverworldParent : UpsidedownParent;
  }
  ```

Mesmo nome, mesma lógica, mesma assinatura. A única diferença é o caminho de acesso: `ChunkStreamingManager` não é dono de `OverworldParent`/`UpsidedownParent` (são propriedades do `WorldManager`), então busca via a referência `WorldManager` que ele já guarda; o `WorldManager` acessa direto porque é o dono.

**Correção possível:** `ChunkStreamingManager` já tem `public WorldManager WorldManager { get; set; }` — dava pra matar a cópia dele e chamar `WorldManager.ResolveDimensionParent(dimensionId)` direto, bastando tornar o método do `WorldManager` `public` (hoje é `private`).

---

## 2. Padrão `dimensionId == OVERWORLD_ID ? X : Y` repetido 6x dentro do próprio `ChunkStreamingManager`

Não é redundância entre os dois arquivos, é dentro de um só — o mesmo formato de ternário resolvendo qual dimensão é qual, só que pra pares de campos diferentes:

| Método | Resolve |
|---|---|
| `ResolveDimensionParent` | `WorldManager.OverworldParent` / `UpsidedownParent` |
| `ResolveLoaded` | `_loadedOverworld` / `_loadedUpsidedown` |
| `ResolveState` | `_overworldState` / `_upsidedownState` |
| `ResolveLoadedPeers` | `_overworldLoadedPeers` / `_upsidedownLoadedPeers` |
| `GetBaseLayer` | `OverworldBaseLayer` / `UpsidedownBaseLayer` |
| corpo de `GetOrCreateLayer` | `OverworldLayer`+`OverworldBaseLayer` / `UpsidedownLayer`+`UpsidedownBaseLayer` |

Cada um resolve um par diferente de campos pro mesmo `dimensionId`, então não dá pra colapsar em um método só sem mudar a estrutura de dados (ex.: trocar os pares de campo solto por um `Dictionary<string, DimensionState>` indexado por `dimensionId`, ou uma classe `DimensionRuntimeState` com uma instância por dimensão). Mais estrutural que os outros itens.

---

## 3. `ResolveDimensionLayer`/`ResolveDimensionBaseLayer` (WorldManager) vs `GetOrCreateLayer`/`GetBaseLayer` (ChunkStreamingManager)

Resolvem os mesmos dois nomes de layer (`ChunkStreamingConstants.PROCEDURAL_LAYER_NAME`/`PROCEDURAL_BASE_LAYER_NAME`), mas **não são 100% equivalentes**:

- `WorldManager.ResolveDimensionLayer/BaseLayer` — só leitura (`GetNodeOrNull`), assume que a layer já existe. Usado nos fluxos de mutação de bloco (`BreakBlockReceive`, `PlaceBlockAuthoritative`, etc.), onde o chunk já foi carregado antes.
- `ChunkStreamingManager.GetOrCreateLayer/GetBaseLayer` — cria a layer se não existir (`GetOrCreateChildLayer`), usado no streaming (`LoadChunkAsync`, `LoadChunkReceive`).

Só valeria unificar se a intenção for centralizar **toda** leitura/criação de layer no `ChunkStreamingManager` e o `WorldManager` passar a chamar através dele — troca de responsabilidade, não só remoção de duplicata.

---

## 4. Lookup repetido de `ChunkStreamingManager` no `WorldManager`

`GetTree().Root.GetNodeOrNull<ChunkStreamingManager>(StaticNodePathsConstants.ChunkStreamingManager)` aparece copiado e colado umas 10+ vezes ao longo do `WorldManager.cs`:

- `CreateProceduralWorldAndPlayer`
- `SetChunkStreamingEnabled`
- `LeaveWorld`
- `FinishPeerJoin`
- `OnPeerDisconnected`
- `BreakBlockReceive` (2x no mesmo método)
- `PlaceBlockAuthoritative`
- `ResolveBiomeForCell`
- `TeleportPlayerClientRequest`
- `TradeDimensionClientRequest`
- (possivelmente mais — arquivo tem 2027 linhas, levantamento não cobriu 100%)

Não é "método redundante" no sentido de duas implementações da mesma lógica — é a mesma expressão de lookup repetida em vez de centralizada. Duas opções:
- **Método privado** `ResolveChunkStreamingManager()` só encapsulando a busca (sem cache) — mínimo esforço, resolve a repetição de código sem mudar timing/lifecycle.
- **Propriedade cacheada**, resolvida uma vez em `_Ready()` (como o `WorldManager` já faz consigo mesmo dentro do `ChunkStreamingManager`) — mais rápido em runtime, mas precisa cuidado com timing de inicialização (`ChunkStreamingManager` pode não existir ainda no `_Ready()` do `WorldManager`, dependendo da ordem dos autoloads/nodes na árvore).

---

## Resumo — o que vale a pena mexer

| # | Item | Tipo | Risco da mudança |
|---|---|---|---|
| 1 | `ResolveDimensionParent` duplicado | duplicata exata | Baixo — só tirar uma cópia e tornar a outra `public` |
| 2 | Ternário `dimensionId == OVERWORLD_ID` × 6 | duplicação estrutural interna | Médio/alto — pede reestruturação de dados, não é só apagar linha |
| 3 | Resolve de layer (leitura vs get-or-create) | overlap parcial, não duplicata | Médio — é decisão de arquitetura (quem é dono de resolver layer), não limpeza |
| 4 | Lookup de `ChunkStreamingManager` repetido | repetição de expressão | Baixo — helper method ou propriedade cacheada |

Itens 1 e 4 são limpeza direta, baixo risco. Itens 2 e 3 são decisões de arquitetura maiores — vale conversar antes de mexer.
