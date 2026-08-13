# Revisão — Geração de mundo / terreno / biomas

Escopo: `ChunkGenerator.cs`, `ChunkStreamingManager.cs`, `WorldRandom.cs`, `BiomeDB.cs`,
`BiomeDefinition.cs`, `BiomeResolver.cs`, `TerrainLayer.cs`, `StructureDefinition.cs`,
`StructureDB.cs`, `TreeStructureDefinition.cs`, `ChunkStreamingConstants.cs`,
`TreeDebugConstants.cs`.

## TL;DR

- Dois arquivos (`TreeStructureDefinition.cs`, `TreeDebugConstants.cs`) usam indentação
  diferente (TAB) do resto do subsistema (4 espaços).
- Duas linhas com indentação literalmente quebrada (`StructureDB.cs:14`,
  `ChunkGenerator.cs:342-344`), sinal de edição manual sem formatar.
- `ChunkGenerator.cs` perdeu a estrutura de `#region` fina que o resto do projeto usa.
- Constantes soltas no meio de classes em vez de em `Constants/`.
- **O mais importante**: ferramental de debug (export de PNG + overlay de texto) ficou
  cravado dentro do caminho de geração REAL do jogo, ligado por padrão — não é só estética,
  é comportamento em produção que ninguém pediu.

---

## 1. Indentação inconsistente

Checagem objetiva (linhas que começam com TAB vs 4 espaços) em todo arquivo do subsistema:

| Arquivo | TAB | 4 espaços |
|---|---|---|
| `ChunkGenerator.cs` | 0 | 3+ |
| `ChunkStreamingManager.cs` | 0 | 3+ |
| `WorldRandom.cs` | 0 | 3+ |
| `BiomeDB.cs` | 0 | 3+ |
| `BiomeDefinition.cs` | 0 | 3+ |
| `BiomeResolver.cs` | 0 | 3+ |
| `TerrainLayer.cs` | 0 | 7+ |
| `StructureDefinition.cs` | 0 | 9+ |
| `StructureDB.cs` | 0 | 3+ |
| `ChunkStreamingConstants.cs` | 0 | 3+ |
| **`TreeStructureDefinition.cs`** | **399** | 0 |
| **`TreeDebugConstants.cs`** | **5** | 0 |

Os dois últimos são os únicos do subsistema inteiro em TAB. Precisa reformatar pra 4 espaços,
igual ao resto.

## 2. Indentação literalmente quebrada (não é so tab/espaço, é bug)

`StructureDB.cs`:

```csharp
Id = "tree",

    Chance = 0.82f,
```

`ChunkGenerator.cs`:

```csharp
        private static void AddSolidBorderNeighbors(...) { ... }

            private const int MinStructureBoundsGapTiles = 1;

            private const int MaxStructureSpacingLookback = 32;
        private static int ResolveLastRightEdgeBefore(...)
```

As duas constantes ficam um nível a mais que o resto do arquivo, encaixadas entre dois
métodos — claramente colado sem passar por um formatador.

Padrão recorrente à parte (não quebra nada, mas foge do estilo do projeto): linha em branco
logo depois de abrir chave, em vários pontos (`ChunkGenerator.cs:151,259`,
`TreeStructureDefinition.cs:137,301,365`, `StructureDefinition.cs:6`). O resto do projeto não
faz isso.

## 3. `#region` degenerado

O padrão do projeto (visto em `ItemDefinition.cs`, `ActionDefinition.cs`, `Player.cs`, etc) é
uma region por responsabilidade: `#region Dinamic properties`, `#region Core - Setup`,
`#region Core - Rpc`, etc. `ChunkGenerator.cs` hoje tem **uma region só**
(`#region Core - Generation`) cobrindo geração de terreno, placement de estrutura, lookback de
espaçamento e erase de chunk — quase o arquivo inteiro (linha 22 a 508). Fica difícil navegar
e não bate com o resto do projeto.

## 4. Constantes soltas em vez de em `Constants/`

O projeto já tem o lugar certo pra isso (`ChunkStreamingConstants`, `TreeDebugConstants`), mas:

- `ChunkGenerator.cs`: `ReferenceTileSize`, `MinStructureBoundsGapTiles`,
  `MaxStructureSpacingLookback` — declaradas como `private const` soltas na classe.
- `TreeStructureDefinition.cs`: `PreviewTileSize`, e o pior caso — um **`16` mágico sem nome**
  dentro de `GetMaxRightExtent()`:

```csharp
public override int GetMaxRightExtent(int worldScale)
{
    return 16; // de onde vem esse numero?
}
```

Isso devia ser uma constante nomeada (`Constants/StructurePlacementConstants.cs` ou similar),
não um literal solto que ninguém sabe justificar seis meses depois.

## 5. Nome desatualizado

`BiomeDefinition.BorderCapTerrainSet` ainda se chama "BorderCap" mesmo com a layer Bordercap
já removida/fundida em "Compose" (`ChunkStreamingConstants.PROCEDURAL_LAYER_NAME = "Compose"`).
O nome não corresponde mais ao conceito atual — devia virar algo como `ComposeTerrainSet`.

---

## O que NÃO é útil pra geração de terreno (ferramental de debug)

Isso é a parte que mais importa. Tem código no subsistema que **não gera terreno nenhum** —
é ferramental de debug que, por estar mal isolado, acabou rodando **durante o jogo de
verdade**, não só quando alguém pede.

### 5.1. Export de PNG de árvore (`TreeStructureDefinition.ExportTreePreview` + `DrawOverlayText`)

~120 linhas: cria uma imagem, desenha tronco/copa com cor sólida, desenha um número (dígitos
bitmap 3x5 desenhados na mão, dentro do próprio arquivo) e salva um `.png` em
`res://.temp/godot/tree/`.

**Problema**: é chamado de dentro de `CollectCells()` — o MESMO método que
`ChunkGenerator.PlaceStructures` chama pra gerar árvore de verdade no jogo. O guard é só
`TreeDebugConstants.EnableTreePreviewExport (= true por padrão) && OS.IsDebugBuild()`. Ou
seja: **em qualquer build de debug (o normal, rodando pelo editor), toda árvore que nasce no
mundo grava um PNG em disco**, sem ninguém ter pedido isso.

Isso também duplica o que já existe: `.dev/tune_hybrid_tree.py` já é uma ferramenta de preview
de árvore, fora do runtime do jogo, sem custo de I/O em produção.

**Recomendação**: `EnableTreePreviewExport` devia default pra `false`. Melhor ainda: tirar essa
chamada de dentro de `CollectCells()` (que é caminho de geração real) e mover pra um lugar que
só a ferramenta de editor (`UpsidedownLevel.GenerateEditorTerrain`) aciona explicitamente — ou
remover de vez, já que o `.dev/tune_hybrid_tree.py` cobre a mesma necessidade sem gravar nada
em disco durante o jogo.

### 5.2. Overlay de texto por estrutura (`TerrainLayer.AddDebugOverlayAnnotation` + `TreeDebugConstants.EnableTreeDebugOverlay`)

Toda estrutura colocada (`ChunkGenerator.PlaceStructures`) registra uma anotação de texto
(o `worldX` dela) que fica desenhada por cima do tile no jogo, via um `DebugOverlay` node
dedicado.

**Problema**: `EnableTreeDebugOverlay = true` por padrão, e pior — o overlay já existente
(`TerrainLayer.ShowTerrainSetDebug`, um `[Export] bool` que o dev liga manualmente pelo
Inspector, desligado por padrão) é **ignorado** por esse sistema novo:

```csharp
if (!_showTerrainSetDebug && _debugOverlayAnnotations.Count == 0)
{
    ...
    return; // so pula o desenho se AMBOS estiverem vazios/false
}
```

Ou seja, mesmo com `ShowTerrainSetDebug` desligado no Inspector, se existir qualquer anotação
registrada (o que sempre acontece, já que `EnableTreeDebugOverlay` é `true` por padrão), o
overlay desenha mesmo assim. O toggle manual que já existia virou decorativo.

**Recomendação**: `EnableTreeDebugOverlay` devia default `false`, e o `RedrawDebugOverlay`
devia respeitar `_showTerrainSetDebug` como o único controle de verdade (anotação sem o
toggle ligado não devia desenhar nada).

### 5.3. `TreeRandom` (Mersenne Twister próprio)

Não é "debug", mas é código que não precisava existir: `TreeStructureDefinition` reimplementou
um MT19937 inteiro (~80 linhas: `Initialize`, `Twist`, `NextUInt32`, `NextDouble`, `NextInt`,
`NextBool`) só pra gerar números pseudo-aleatórios pra árvore — quando `WorldRandom` (hash
determinístico por seed+coluna+salt) já faz exatamente esse trabalho pro resto do
subsistema inteiro (`BiomeResolver`, `ChunkGenerator`, ruído de altura). Agora tem DUAS formas
de gerar aleatoriedade determinística convivendo no mesmo subsistema, sem necessidade
funcional pra isso — é manutenção extra (esse código nunca muda, mas também nunca devia ter
sido escrito) sem ganho.

**Recomendação**: trocar `TreeRandom` por `WorldRandom.StructureRandomInt`/`StructureRandom01`,
do jeito que já era antes dessa reescrita — reduz o arquivo em ~80 linhas sem mudar
comportamento nenhum de geração.

---

## Prioridade sugerida

1. **`EnableTreePreviewExport` e `EnableTreeDebugOverlay` → `false` por padrão.** Uma linha
   cada, resolve o problema de comportamento em produção hoje mesmo.
2. Reformatar `TreeStructureDefinition.cs` e `TreeDebugConstants.cs` pra 4 espaços.
3. Mover as constantes soltas pra `Constants/`.
4. Corrigir as duas indentações quebradas (`StructureDB.cs`, `ChunkGenerator.cs`).
5. Recomposição de `#region` em `ChunkGenerator.cs` (menor prioridade, cosmético).
6. Avaliar se remove `TreeRandom` em favor de `WorldRandom` (mudança maior, mexe em toda a
   lógica de `TreeStructureDefinition` — só fazer com aprovação explícita, já que reseed toda
   árvore existente no mundo).
