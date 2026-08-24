# Contexto para retomar em outra sessão

Estado do projeto em 2026-08-24. Tudo aqui foi verificado no código, não é memória de conversa.

**Projeto:** jogo 2.5D em Godot 4.6 (.NET 8, C#), pasta `Game/`, namespace `Jogo25D.*`.
**Commit atual:** `0ff6894`. Há alterações não commitadas (ver seção 6).

---

## 1. Como buildar e testar

```bash
cd Godot-Game-2
dotnet build -v q -nologo                                   # 0 erros esperado
cd Game && godot --headless --quit-after 200                # smoke: espera "118 nodes estaticos registrados"
```

O smoke headless valida boot, registro de nós e ausência de erro de script. **Não valida RPC nenhum** — nada de multiplayer foi testado nesta linha de trabalho.

Se aparecer erro de import (fonte/textura faltando): `godot --headless --import` regenera o cache.

---

## 2. Arquitetura atual

### Registro de nós (`Game.cs` + `Bootstrap.cs`)

Todo nó estático da árvore é registrado num registro estático `Game` que espelha a cena: `Game.Managers.WorldManager.Node`, `Game.Ui.StartUI.Node`. O `Bootstrap` (script da raiz do `Main.tscn`) varre, valida e só então libera o jogo. Quem precisa agir na inicialização usa `Game.WhenReady(...)`.

**Armadilha conhecida:** `Game.Reset()` **não pode** limpar a fila do `WhenReady`. O `_Ready` das telas roda antes do `Bootstrap` (Godot propaga de baixo pra cima), então limpar ali descarta todos os `Initialize` em silêncio — nenhum botão fica ligado e o menu não responde, sem erro no log. Já aconteceu.

Detalhe em `.dev/node-registry-bootstrap.md`.

### Managers (todos em `Managers.tscn`)

| peça | linhas | RPCs | responsabilidade |
|---|---:|---:|---|
| `SessionManager` | 554 | 8 | mundo atual, personagem, modo, peers, join, política de autosave |
| `NetworkManager` | 378 | 0 | o canal: criar servidor, entrar, cair. **Só notifica por evento** |
| `WorldManager` | 218 | 0 | cena do mundo: instanciar/liberar, spawn do player local, lookup |
| `SaveManager` | 176 | 0 | recebe o que gravar e delega ao `SaveStorage` |
| `DimensionManager` | 535 | 6 | parents, layers, containers, spawn no mundo |
| `ChunkStreamingManager` | 623 | 3 | loop de load/unload de chunk |
| `RouterManager` | — | 0 | abre/fecha tela; **único que escreve `Visible` de tela** |
| `WindowManager` | — | 0 | fullscreen/F11 (era `ScreenManager`) |

### Entidades com comportamento próprio

| | linhas | RPCs | |
|---|---:|---:|---|
| `Player` | 1704 | 22 | estado, teleporte, troca de dimensão, uso de item |
| `TerrainLayer` | 1383 | 3 | edição de célula, autotile, RPC de bloco |
| `Prop` → `Portal` | — | 3 | base de prop: colocar/quebrar/persistir |

### Systems (classe C# pura, sem nó)

`SaveStorage` (IO de `.tres`), `ChunkGeneratorSystem`, `Inventory`, `DiscoveredMapImage`.

---

## 3. Regras que valem no projeto

Estabelecidas ao longo do trabalho e aplicadas no código. Ver `.dev/managers-architecture-redesign.md` para o raciocínio.

1. **Alvo existe? É dele.** Ação com alvo endereçável na árvore é método e RPC do alvo — foi assim que o RPC de bloco foi pra `TerrainLayer` e o de personagem pro dono do estado.
2. **Criar é de quem tem o lugar.** O nó novo não existe pra receber RPC, então quem cria é o dono do lugar (`DimensionManager`) — nunca um manager por tipo de entidade.
3. **Nenhum manager leva nome de tipo de entidade.** `PlayerManager`, `PortalManager`, `TerrainManager` foram considerados e descartados.
4. **UI → manager → system.** A tela nunca fala com o system direto; quem decide se o dado vem do disco ou de RPC é o manager.
5. **Tela não se abre.** Nenhuma tela chama `Open` em si mesma nem `Close` antes de abrir outra — o `RouterManager.Open` já fecha a atual. Contexto é setado por quem vai abrir; a tela se monta no `OnOpened()`.
6. **Estado inicial é da cena.** `layer` e `visible` das telas vivem no `.tscn`, não no `_Ready`.
7. **A rede notifica, não pergunta.** `NetworkManager` emite `Disconnecting`, `ConnectionSucceeded`, `ServerDisconnected`, `PeerLeft`; o `SessionManager` assina. É o que mantém o grafo acíclico.
8. **`WorldManager` recebe por parâmetro** (`save`, `character`) em vez de ler a sessão — foi o que quebrou o ciclo `Session ↔ World`.

**O grafo de managers está acíclico.** Não recriar seta de volta.

---

## 4. Como o save funciona

```
SessionManager   quando salvar e o que entra
      ↓
SaveManager      recebe e grava
      ↓
SaveStorage      .tres e pastas (system)
```

**Gatilhos:** timer de autosave (`AutosaveIntervalMinutes × 60`), sair do mundo (`SessionManager.LeaveWorld`), fechar a janela (`CloseRequested`).

**O que é salvo:** terreno como **mutações por chunk** (nunca os tiles — o mapa é regerado pelo seed e as mutações reaplicadas), props/portais, personagem local, personagens de peers, meta do mundo.

**O que NÃO é salvo:** `WorldItem` (item no chão) e `NPC`. Nenhum dos dois tem código de persistência.

**Arquivos:** `user://profile.tres`, `user://saves/worlds/<id>/{world,dimension_*}.tres`, `user://saves/characters/`, `server_characters/`, `peer_backups/`.

Detalhe e proposta de evolução em `.dev/save-e-sessao.md` (lista de `Resource` registrada, registro por sinal da árvore).

---

## 5. Fluxo de entrada e saída de mundo

Stack traces completos em `.dev/fluxo-mundo.md` — entrada solo/host, entrada de cliente num servidor, saída pelo menu, desconexão. Resumo:

```
CharacterSelectUI.SelectLocal → SessionManager.SelectCharacter → EnterPendingWorld
   → WorldManager.CreateProceduralWorldAndPlayer(save, character)
   → SessionManager.StartAutosave(save)

SessionManager.LeaveWorld
   → PersistBeforeLeaving → StopAutosave → limpa sessão
   → NetworkManager.CloseSession
   → WorldManager.DespawnWorld
```

---

## 6. Alterações não commitadas

**Uso de item no mundo virou genérico** (feito nesta sessão):

- `ItemDefinition.UseAt(Player, ItemData, Vector2)` — novo gancho virtual, roda no lado autoritativo.
- `Player.UseItemAtReceive/Request` — **um** RPC para qualquer item, no lugar de `PlaceBlockReceive/Request` + `PlacePortalReceive/Request`.
- `BlockItemDefinition.UseAt` e `PortalItemDefinition.UseAt` fazem validação, posicionamento e consumo do item.
- **`Player` perdeu 142 linhas** e não menciona mais `PortalItemDefinition`, `BlockItemDefinition`, `PlaceBlock`, `PlacePortal` nem `SpawnProp`.

Item novo que se coloca no mundo agora é só uma definição — zero linha no `Player`.

Também aparecem ~323 `.import` modificados: efeito do `godot --headless --import`, não é mudança de código.

---

## 7. Pendências e riscos conhecidos

### Bug ativo, não corrigido

```
NetworkManager.cs:228
Game.Managers.WorldManager.Node.CallDeferred("RespawnLocalSoloPlayer");
```

`RespawnLocalSoloPlayer` passou a exigir `(CharacterSaveData)`. Como a chamada é por nome, **o compilador não pega** — falha em runtime ao desconectar de um servidor. A correção provável não é só passar o argumento: recriar o player solo é decisão de sessão, então isso devia sair do `NetworkManager` e virar reação ao evento `Disconnecting`.

### Nada de multiplayer foi testado

Os 8 RPCs de personagem mudaram de nó duas vezes (`NetworkManager` → `SaveManager` → `SessionManager`), o RPC de bloco foi pra `TerrainLayer`, o de uso de item é novo. **Nenhum deles rodou com dois peers.** Checklist mínima:

- [ ] quebrar/colocar bloco nas duas pontas, conferindo autotile na borda entre biomas
- [ ] colocar e quebrar portal; trocar de dimensão pelo portal
- [ ] entrar com 2 peers: player remoto, NPC, item dropado
- [ ] criar/selecionar/deletar personagem em mundo modo servidor
- [ ] carregar um mundo salvo **antigo** (valida a migração `Portals` → `Props`)
- [ ] entrar e sair de mundo 2x seguidas

### Débitos menores

- `SaveManager` tem 3 regions vazias, resíduo de recorte.
- `SaveStorage.CachedProfile` é estado estático sem invalidação.
- `PortalSaveData` existe só como classe obsoleta herdando de `PropSaveData`, porque os `.tres` salvos gravam o **caminho do script** — apagar o arquivo quebra o carregamento de mundos antigos.
- 185 arestas entre features, várias circulares (`UI/CharacterSelect` ↔ `UI/WorldSelect`); 18 telas compartilham o namespace `Jogo25D.UI`.
- `Game.cs` referencia o tipo concreto de todos os managers — núcleo e features se dependem mutuamente.

---

## 8. Armadilhas do ambiente (custaram tempo)

- **`.tscn`/`.tres` não podem ter BOM.** Script que reescreve cena precisa gravar em UTF-8 puro, senão o parser do Godot rejeita o arquivo inteiro com `Parse Error: Expected '['`.
- **Cena resolve script por `uid://` antes do caminho.** Ao mover arquivo, limpar `.godot/uid_cache.bin` e `.godot/global_script_class_cache.cfg`, senão o Godot continua achando o caminho velho.
- **Heredoc com crase quebra** neste shell; para escrever markdown com bloco de código, usar a ferramenta de escrita de arquivo em vez de `cat <<EOF`.
- **`[Export]` de tipo genérico não compila** (`GD0102`); `is ISavable<>` também não (`CS7003`). Testado.
- **Godot só descobre script C# no assembly principal.** Separar features em projetos referenciados não funciona no 4.6 (PR #117452 em aberto resolve). Uma tentativa de reestruturar em módulos foi feita e revertida.

---

## 9. Documentos em `.dev/`

| arquivo | conteúdo |
|---|---|
| `node-registry-bootstrap.md` | registro `Game` + Bootstrap, e a armadilha do `Reset()` |
| `managers-architecture-redesign.md` | as regras, o diagnóstico e o plano que gerou a arquitetura atual |
| `fluxo-mundo.md` | stack traces de entrada e saída de mundo |
| `save-e-sessao.md` | como o save funciona e a proposta de evolução |
| `world-generation.md` | geração de mundo, chunk, bioma, persistência de terreno |
| `world-manager-redundancy-review.md` | duplicações antigas (parcialmente resolvidas) |

**Regra de trabalho do dono do projeto:** decisão de arquitetura fechada em conversa vai pro `.md` correspondente no mesmo turno. E, desde o meio desta sessão, **só alterar código quando ele pedir explicitamente**.
