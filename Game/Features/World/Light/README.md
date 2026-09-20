# Iluminação

Este é o ponto de entrada da feature. Aqui está **como o sistema funciona hoje**:
quem calcula o quê, em que ordem, e o que dispara cada etapa. Os documentos em
`Game/Docs/` detalham cada cálculo; este arquivo é o mapa.

Nenhum `Light2D`, `LightOccluder2D` ou `CanvasModulate` da engine participa. A luz
é calculada a partir do terreno lógico e aplicada por um shader que reescreve a cor
da cena.

---

## As duas perguntas que o sistema responde

1. **Quanto de céu chega em cada célula?** Depende de onde há sólido, onde há
   parede de fundo (wall) e a que distância está a abertura mais próxima.
2. **Quanta luz própria chega em cada célula?** Depende dos emissores — blocos
   com `LightRadius` e nós `LightSource2D` — propagados em RGB.

As duas respostas são texturas. O shader combina as duas com a sombra projetada do
sol e escreve o resultado sobre a cor da cena.

---

## Onde cada peça mora

```
Features/World/Light/
├── Entities/     LightMap2D, LightSource2D, LightingEditorPreview   (nós da cena)
├── Managers/     LightMapManager, LightingManager                   (ciclo e coordenação)
├── Systems/      LogicalLightWorld, SkyAccessField, AnalyticShadowGeometry,
│                 WindowBeamCache, LightPropagation*, LightSourceScanner,
│                 SceneLightSources, EditorTileRevision               (cálculo puro)
└── Resources/    LightMapData, LightChunkOverlay, TerrainLightMask,
                  LightTextureOutput, LightSource                     (dados e apresentação)

Assets/Shaders/   terrain_light_overlay, window_beam_cache, projected_shadow*,
                  wall_projected_shadow, light_propagation.glsl
Features/World/Blocks/Entities/BackgroundWallLayer.cs   (as walls)
Assets/Data/LightMap.tres                               (a regulagem)
```

Regra de leitura: `Systems/` não conhece cena nem nó — recebe dados e devolve
dados. `Entities/` e `Managers/` cuidam de ciclo de vida, invalidação e de quando
chamar os sistemas.

---

## O caminho da luz

```
Tiles (Base, Compose, BackgroundWalls)
        │
        ▼
LogicalLightWorld ──────────────┬──────────────────────┐
  colunas comprimidas de        │                      │
  terreno + edições + walls     ▼                      ▼
                        SkyAccessField          AnalyticShadowGeometry
                        (opacidade, wall,        (retângulos de material
                         acesso ao céu)           + índice espacial)
                                │                      │
                                ▼                      │
                        WindowBeamCache                │
                    (feixe solar e feixe local,        │
                     2 SubViewports)                   │
                                │                      │
LightSourceScanner ──► LightPropagation (GPU/CPU)      │
  céu + blocos + LightSource2D      │                  │
                                    ▼                  ▼
                            terrain_light_overlay.gdshader
                                    │
                                    ▼
                            cor da cena reescrita
```

### 1. Mundo lógico — `LogicalLightWorld`

Guarda o terreno como **colunas comprimidas** (`Run: início, fim, terreno`), não
como células soltas. Isso permite perguntar "o que há em (x, y)" em qualquer
coordenada, inclusive fora dos chunks desenhados: o terreno procedural é gerado
sob demanda pelo `ChunkGeneratorSystem` e as edições do jogador entram por cima.

Consequência importante: **a luz não depende do que está carregado na tela**. Uma
montanha fora da viewport continua projetando sombra.

Walls entram como um conjunto de células (`SetBackground`) — elas fecham o acesso
ao céu, mas não são sólidas no plano XY.

### 2. Acesso ao céu — `SkyAccessField`

Constrói uma textura RGBA8 da região, onde cada célula carrega opacidade (R),
presença de wall (G) e **luz de céu** (B). O cálculo é uma difusão em duas etapas,
com fila de prioridade:

1. **No ar**: toda célula sem sólido e sem wall começa com 1 e perde
   `AIR_LIGHT_LOSS` (1/24) por tile, sem atravessar cantos diagonais fechados.
2. **Nos sólidos**: o ar já iluminado entrega luz para dentro do terreno com
   absorção exponencial `pow(SOLID_FALLOFF, distância * 3 / TerrainLightDepthTiles)`.
   Sólidos não devolvem luz para o ar de outro compartimento — é isso que impede
   uma sala vedada de "vazar" claridade do lado de fora.

### 3. Sombra projetada — `AnalyticShadowGeometry`

Em vez de traçar raios, agrupa o terreno em **retângulos de material** e os
indexa em bins de 16 px, empacotados numa textura `Rgbaf` de 1024 de largura. O
shader lê os retângulos do bin do pixel e faz a união analítica dos intervalos
angulares que o disco solar encontra bloqueados.

A construção é incremental: `Process(3 ms)` por frame até `Complete`. Enquanto não
termina, o overlay continua exibindo a geometria anterior (`geometry_ready`).

### 4. Aberturas — `WindowBeamCache`

Dois `SubViewport` em 4× a resolução da região, rodando `window_beam_cache.gdshader`:

- **feixe solar**: procura, na direção do sol, uma abertura por onde a luz entra;
- **feixe local**: aponta para cada emissor externo e exige caminho livre até ele.

Só entram no feixe local as fontes que estão em célula sem wall e sem sólido. As
fontes internas continuam iluminando pelo mapa de emissão (etapa 5).

O cache só recalcula quando muda alguma coisa que importa: revisão do mundo, das
walls, das fontes, da região, do ângulo ou da penumbra.

### 5. Emissão local — `LightPropagation*`

`LightSourceScanner` reúne as fontes da região: céu (quando pedido), blocos com
`LightRadius > 0` no `BlockDB` e os `LightSource2D` registrados em
`SceneLightSources`. A propagação RGB roda no compute shader
`light_propagation.glsl` (ping-pong, 24 iterações = `ceil(1/AIR_LIGHT_LOSS)`), com
fallback idêntico em CPU (`LightPropagationSystem`) quando não há
`RenderingDevice`. O `LightPropagationDispatcher` escolhe o caminho e devolve
sempre um `LightTextureOutput`.

### 6. Composição — `terrain_light_overlay.gdshader`

Ordem exata do que o shader faz por pixel:

1. `terrain_mask` diz se aquele pixel é tile de verdade (segue o alpha do sprite,
   construído por `TerrainLightMask`) — fora da máscara nada é alterado;
2. luz de céu colorida por `SunColor`;
3. em pixels de wall sem sólido primário, a sombra projetada abre espaço conforme
   `ShadowStrength`, e `AmbientLightInfluence` preenche essa sombra com a luz
   disponível (preenche, não cria: sala vedada continua escura);
4. o feixe solar entra por **máximo**, depois dessa etapa;
5. soma-se `max(emissão propagada, feixe local)`;
6. `AMBIENT_MIN` (0.04) é um piso visual aplicado por último — não é fonte de luz;
7. o resultado multiplica `scene_color`.

O shader usa `hint_screen_texture` e `blend_disabled`: ele reescreve o pixel, então
ganhos acima de 1 realmente clareiam superfícies já claras.

---

## Quem dispara o cálculo

### Em jogo — `LightingManager`

Trabalha por chunk, acompanhando o streaming:

- `ChunkLoaded` / `ChunkUnloaded` do `TileStreamingManager` e `OnCellChanged` de
  cada edição marcam o chunk **e os 8 vizinhos** como sujos (a luz atravessa
  fronteiras);
- por frame, no máximo `MAX_CHUNK_REBUILDS_PER_FRAME` (2) chunks são reconstruídos;
- cada reconstrução usa a região do chunk com `CHUNK_PADDING` (24 células) de
  folga, para a luz vizinha entrar corretamente;
- o resultado vira um `LightChunkOverlay` — um `Sprite2D` sob o nó `LightOverlay`
  da dimensão, com o material de composição.

Em mundos autorados (sem streaming), `EnsureAuthoredChunks` cobre o retângulo
pintado uma vez e reage a edições.

### No editor — `LightingEditorPreview`

`LightMap2D` cria esse nó interno (`TerrainPreview`) sozinho. Ele cobre o retângulo
pintado das camadas com `Padding` de folga e recalcula quando o hash do estado
visual muda (tiles, transform, modulate, visibilidade, fontes). Resultados
assíncronos obsoletos são descartados pelo contador de ciclo de vida. Acima de
500×500 células ele desliga e avisa — em jogo esse limite não existe, porque lá o
cálculo é por chunk.

### O nó que amarra tudo — `LightMap2D`

Um por dimensão. A cada frame ele: resolve as camadas, acompanha o mundo lógico,
recalcula a região a partir da câmera (view arredondada para chunk + margem),
avança a geometria de sombra, atualiza o `WindowBeamCache`, tinge o `Background`
com `SunColor` e **religa os parâmetros nos materiais** dos overlays — tanto os do
runtime quanto o da prévia. Essa religação é protegida por uma chave: se nada
mudou, nenhum `SetShaderParameter` é chamado.

---

## Como pôr luz numa coisa

- **Bloco que brilha**: no `BlockDB`, defina `LightRadius` (a intensidade é
  `LightRadius / 15`, limitada a 1) e `LightColor`. O scanner acha sozinho.
- **Prop/objeto que brilha**: adicione um `LightSource2D` como filho, com
  `LightColor` e `Energy`. Ele se registra no nível (`Dimension` ou nó com
  `LightMap`) e se desregistra ao sair da árvore. Veja `Scenes/World/Props/Campfire.tscn`.
- **Parede de fundo**: `BackgroundWallLayer` — fecha o céu e recebe a sombra
  projetada. Detalhes em [BackgroundWalls.md](../../../Docs/BackgroundWalls.md).

Mudar a cor ou a energia de um `LightSource2D` invalida o chunk e os vizinhos.
É barato para eventos; para tremular a cada frame, meça antes.

---

## Regulagem

Tudo em `Assets/Data/LightMap.tres` (`LightMapData`), com os valores atuais:

| Propriedade | Hoje | Efeito |
| --- | --- | --- |
| `SunColor` | branco | Cor do céu, do feixe e do background. |
| `SunAngleDegrees` | -3.5 | Direção da projeção e do feixe. |
| `SunPenumbra` | 0 | 0 abre o disco solar; 1 dá raios paralelos. |
| `SunPenumbraShadowCurve` | 0.85 | Perfil da borda de sombra. |
| `SunPenumbraAmbientCurve` | 0.8 | Perfil da borda de ambiente. |
| `ShadowStrength` | 1.0 | Opacidade da sombra nas walls. |
| `AmbientLightInfluence` | 0.5 | Quanto a luz disponível preenche a sombra. |
| `TerrainLightDepthTiles` | 5.25 | Profundidade da absorção dentro do sólido. |
| `DebugShadow` | false | Projeção crua em cinza, sem máscara nem cor da cena. |

Os setters emitem `Changed`, então mexer no inspetor atualiza a prévia na hora.
As constantes de algoritmo (perda no ar, falloff, limiar, padding, orçamento por
frame) ficam em `Constants/LightingConstants.cs`.

---

## Rede

O mundo lógico é replicado, não recalculado por peer: `TileStreamingManager`
envia as edições (`ReceiveLightWorld`, em lotes de 3072 inteiros) e as células de
wall (`ReceiveBackgroundLight`, lotes de 2048) ao peer que entra. Cada cliente
então calcula sua própria iluminação a partir do mesmo estado lógico.

---

## Custo

A propagação, a sombra analítica e a composição rodam na GPU. Ainda usam CPU: a
preparação dos dados, o campo de acesso ao céu e as máscaras de tiles. Os freios
são o orçamento de 3 ms por frame da geometria e o limite de 2 chunks
reconstruídos por frame. Números medidos em
[LightingPerformance.md](../../../Docs/LightingPerformance.md).

---

## Leitura detalhada

- [LightMap.md](../../../Docs/LightMap.md) — referência de responsabilidades e composição
- [SombraProjetada.md](../../../Docs/SombraProjetada.md) — a união angular da sombra
- [BackgroundWalls.md](../../../Docs/BackgroundWalls.md) — paredes de fundo
- [DepthSkylight-Restauracao.md](../../../Docs/DepthSkylight-Restauracao.md) — entrada de luz pelo fundo
- [LightingPerformance.md](../../../Docs/LightingPerformance.md) — medições
