# Iluminação 2D/2.5D

O nó `LightMap2D` coordena a iluminação de cada dimensão. A configuração fica em
`Assets/Data/LightMap.tres`; a prévia interna e os overlays de chunks usam a mesma
composição. Não há Light2D da engine no cálculo ativo.

## Responsabilidades

| Componente | Responsabilidade |
| --- | --- |
| `LogicalLightWorld` | Ocupação primária em colunas comprimidas, edições e presença de walls, independentemente dos chunks visíveis. |
| `Dimension` | Mundo lógico da própria dimensão (`EnsureWorld`), streaming de tile e os RPCs de chunk e de luz. |
| `AnalyticShadowGeometry` | Retângulos de material e índice espacial da sombra. |
| `SkyAccessField` | Entrada de céu pelo fundo, transporte no ar e absorção no terreno. |
| `WindowBeamCache` | Texturas de transmissão solar e de fontes externas por aberturas. |
| `LightSource2D` / `SceneLightSources` | Registro de emissores de cena e revisões isoladas por nível. |
| `LightMap2D` | Fontes e overlays dos chunks carregados em runtime, um nó por dimensão. |
| `LightingEditorPreview` | Reconstrução da prévia após mudanças; descarte de resultados assíncronos obsoletos. |
| `LightPropagationDispatcher` | Caminho GPU de emissão local e fallback CPU. |
| `TerrainLightMask` | Cobertura dos pixels reais dos tiles, separada da resolução do mapa de luz. |
| `TerrainLightOverlay.gdshader` | Composição final sobre a cor da cena. |

`Base` e `Compose` fornecem sólidos. Na importação da cena para o mundo lógico,
Base é aplicada por último e tem prioridade nas células compartilhadas. Walls
fecham o acesso pelo fundo, mas não se tornam obstáculos sólidos no plano XY.

## Cálculos ativos

1. **Projeção:** a união analítica dos intervalos bloqueados do disco solar
   produz oclusão por pixel. Não há exclusão de corpo próprio, escadas ou receptor.
   A composição aplica essa sombra somente às células com wall e sem sólido primário.
2. **Céu:** uma célula sem sólido e sem wall recebe luz. Ela se propaga pelo ar
   com perda de `1/24` por tile, respeitando sólidos e cantos diagonais fechados.
   Depois, o ar iluminado transmite luz para sólidos, que não retransmitem ao ar
   de outro compartimento. A absorção é exponencial:
   `luz * pow(0.5, distância * 3 / TerrainLightDepthTiles)`.
3. **Aberturas:** o feixe solar procura uma abertura na direção do sol. O feixe
   local aponta para o emissor e exige uma abertura e caminho livre até a fonte.
   Só fontes sem wall e sem sólido na célula de origem participam dessa projeção.
   Fontes internas continuam iluminando pelo mapa de emissão local.
4. **Emissão:** blocos emissores e fontes de cena alimentam a propagação RGB,
   calculada por compute shader ou pelo fallback CPU equivalente.
5. **Composição:** a luz de céu é colorida por `SunColor`. Nas walls, a opacidade
   projetada é preenchida pela luz disponível conforme `AmbientLightInfluence`.
   O feixe solar é combinado por máximo após essa etapa. A contribuição local é
   `max(emissão propagada, feixe local)` e é somada à iluminação solar. O piso
   visual `AMBIENT_MIN` é aplicado por último, não como fonte de luz.

O overlay usa `hint_screen_texture` e `blend_disabled`: ganhos maiores que 1
podem iluminar superfícies já claras. A máscara segue o alpha dos tiles de Base,
Compose e BackgroundWalls. O céu recebe a modulação de `SunColor` pelo nó
Background, com restauração da cor anterior ao desativar a iluminação.

## Configuração

| Propriedade | Efeito |
| --- | --- |
| `SunColor` | Cor solar do ambiente, feixe e background. |
| `SunAngleDegrees` | Direção da projeção e do feixe solar. |
| `SunPenumbra` | Abertura angular: 0 abre o disco; 1 gera raios paralelos. |
| `SunPenumbraShadowCurve` / `SunPenumbraAmbientCurve` | Perfil das duas bordas, sem expandir a sombra. |
| `ShadowStrength` | Opacidade da sombra projetada nas walls. |
| `AmbientLightInfluence` | Preenchimento da sombra pela luz disponível, sem criar luz em salas vedadas. |
| `TerrainLightDepthTiles` | Escala da absorção exponencial dentro dos sólidos. |
| `DebugShadow` | Projeção bruta em tons de cinza, independente da máscara e da cor da cena. |
| `IncludeSkylight` no nó | Controla a contribuição solar na composição; não desliga os emissores locais. |

Os setters do recurso emitem `Changed`. As revisões de tiles, geometria, walls
e fontes invalidam os caches correspondentes. O estado lógico não depende de
quais chunks estão desenhados. A criação de um mundo procedural descarta walls
e entidades autoradas antes de aplicar o save; mundos autorados conservam a cena.

## Organização

Os arquivos C# usam namespaces em bloco, quatro espaços e regions de propriedades,
construtores, ciclo de vida Godot e operações. `.editorconfig` registra a formatação.
Os cálculos permanecem nos sistemas e shaders; os nós coordenam seu ciclo de vida.

As pastas seguem a convenção do projeto: nós em `Entities/`, coordenação em
`Context/`, cálculo em `Systems/`, dados e apresentação em `Resources/`. Shaders
ficam em `Assets/Shaders/`, inclusive `terrain_light_overlay.gdshader` e
`light_propagation.glsl`. As paredes de fundo ficam em `Blocks/Entities/`.

Veja [performance](LightingPerformance.md),
[paredes](BackgroundWalls.md), [projeção](SombraProjetada.md) e
[restauração da entrada pelo fundo](DepthSkylight-Restauracao.md).
