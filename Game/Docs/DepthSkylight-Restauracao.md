# Iluminação pelo fundo: implementação e restauração

Este documento reúne a entrada de luz ambiente pelas aberturas no fundo e o
feixe direcional acrescentado para iluminar paredes internas. São mecanismos
separados e podem ser revertidos separadamente. Não precisam de mudanças em
`Game/Assets/Data/LightMap.tres`.

## 1. Fontes de luz ambiente na dimensão de profundidade

Em `LightSourceScanner.CollectSources`, uma célula recebe luz branca do céu
quando `includeSkylight` está ligado, ela não contém sólido e não tem wall.
O argumento opcional `Func<Vector2I,bool> hasBackground` permite fornecer a
ocupação do fundo; na ausência dele o scanner resolve BackgroundWalls no pai.

Regra: `includeSkylight && !isSolid(cell) && !hasBackground(cell)`.

Isso substitui a antiga semeadura por coluna que começava no topo e parava
no primeiro sólido. Um teto não bloqueia mais o céu visível atrás de uma área
vazia. Em contrapartida, qualquer buraco sem wall, mesmo subterrâneo, é uma
abertura para essa dimensão: o sistema não diferencia céu de caverna pelo
nome ou pela posição vertical.

`LightingEditorPreview` e `LightingManager` fornecem a ocupação das walls ao
scanner. O sólido é a união de Base e Compose; a wall fecha a entrada de céu,
mas não vira obstáculo sólido à propagação lateral da luz ambiente.

### Reaplicar

1. Adicionar o argumento opcional hasBackground ao scanner e resolver a layer
   BackgroundWalls como fallback.
2. Durante a visita às células da região, adicionar LightSource branco nas
   células que satisfazem a regra acima; manter fontes emissivas existentes.
3. Passar o predicado de walls nos dois consumidores: preview e manager.
4. Manter a invalidação da iluminação quando tiles primários ou walls mudam.
   O preview usa EditorTileRevision; edições runtime notificam o manager.

### Reverter somente a entrada pelo fundo

Restaurar a semeadura antiga por coluna no scanner: percorrer de cima para
baixo, semear céu enquanto não houver sólido e parar ao primeiro sólido.
Remover os callbacks hasBackground dos consumidores se também remover esse
argumento da assinatura. Manter a coleta de fontes emissivas e não desfazer
as otimizações do dispatcher, máscaras ou atualização no editor.

Não reverta arquivos inteiros sem revisar o diff: eles contêm outras mudanças.
A revisão 7a14bf, mencionada durante o trabalho, é apenas contexto histórico;
não é um commit isolado que possa ser revertido integralmente com segurança.

## 2. Propagação ambiente no ar

`LightingConstants.AIR_LIGHT_LOSS = 1f / 24f` define a perda linear por tile.
No ar, cada canal é `max(0, canal - AIR_LIGHT_LOSS * distânciaDoPasso)`.
Dentro de sólido permanece a atenuação multiplicativa SOLID_FALLOFF = 0.5.
Diagonais usam distância sqrt(2). A propagação toma o máximo entre caminhos;
não soma energia de fontes sobrepostas.

Arquivos sincronizados:

- `Systems/LightPropagationSystem.cs`: referência CPU.
- `Resources/LightPropagation.glsl`: compute shader, parâmetro air_loss.
- `Systems/LightPropagationGpu.cs`: upload da constante e número de iterações.
- `Constants/LightingConstants.cs`: perda e alcance.

O alcance de luz branca no ar é aproximadamente 24 tiles. A alteração também
se aplica às fontes emissivas; não somente às janelas. MIN_LIGHT_THRESHOLD e
AMBIENT_MIN continuam fazendo parte da composição existente.

Para voltar à atenuação anterior, usar `canal * pow(AIR_FALLOFF, distância)`
com AIR_FALLOFF = 0.85 tanto na CPU quanto no shader. Atualizar o parâmetro
GPU para esse valor e restaurar a contagem de iterações baseada no logaritmo
do limiar de luz (cerca de 25 passos), em vez de ceil(1 / AIR_LIGHT_LOSS).
Não alterar só um backend: isso quebra a equivalência CPU/GPU.

## 3. Feixe direcional pelas aberturas

`Systems/WindowBeamCache.cs` cria um SubViewport sem 3D com quatro texels por
tile. `Assets/Shaders/window_beam_cache.gdshader` percorre células na direção
do sol: terreno primário bloqueia; ausência de wall em célula vazia admite
luz. Somente células vazias com wall recebem o campo direcional.

O alcance atual é 24 tiles, com atenuação suave. A abertura angular usa
radians(8) * (1 - SunPenumbra)^2 e o ângulo usa SunAngleDegrees. É uma
aproximação direcional 2.5D, não uma simulação física de dispersão em volume 3D.

O campo é calculado na GPU quando mudam mundo, região, terreno, background,
ângulo ou penumbra. Não há leitura da GPU pela CPU no caminho normal.
São 15 direções do disco solar e até 64 travessias por texel nas atualizações.
Nos quadros seguintes, os materiais apenas consultam a textura. Mudanças
grandes ainda podem causar um pico de recomputação.

`Entities/LightMap2D.cs` publica o campo nas walls e nos overlays de editor e
runtime. `window_beam_sampling.gdshaderinc` converte coordenadas globais,
inclusive para os overlays recortados por chunk. A luz reduz a sombra
projetada da wall e estabelece um piso direcional no mapa de luz final:

- Wall: fator = 1 - shadow_strength * shadow * (1 - beam).
- Overlay: luz = max(luz ambiente, beam).

Assim as duas multiplicações não apagam novamente o feixe. A luz ambiente
existente continua ativa. Fora da região em cache não há contribuição do
feixe; uma borda da região não é tratada como abertura de céu.

### Remover somente o feixe

1. Remover o campo WindowBeamCache, Update/Bind e DisableWindowBeam de LightMap2D.
2. Em wall_projected_shadow.gdshader, remover o include window_beam_sampling
   e o fator `(1 - window_light(world_position))`.
3. Em TerrainLightOverlay.gdshader, remover o include, varying/vertex do feixe
   e a linha `light=max(light,vec3(window_light(beam_world_position)))`.
4. Os arquivos WindowBeamCache.cs e window_beam*.gdshader* ficam sem consumidores.

Isso preserva a luz ambiente pelos buracos de fundo e não exige editar o .tres.

## Verificação

Executar da raiz do repositório:

```powershell
dotnet build Game --no-restore
godot_console --headless --editor --path Game --import --quit
godot_console --path Game res://Testing/DepthSkylightRegression.tscn
godot_console --path Game res://Testing/WindowBeamRegression.tscn
```

Usar Godot com renderização GPU nos testes visuais. DepthSkylightRegression
compara CPU/GPU e verifica teto com fundo aberto, fechamento e janela.
WindowBeamRegression verifica sala fechada, janela, direção, rotação solar,
invalidação ao fechar a janela e bloqueio por terreno. Salva o campo em
`.images/window-beam-regression.png`.

Para revisar a integração, LightPortWorldProbe captura o cenário completo;
a posição da câmera desse teste pode precisar ser ajustada para enquadrar a
janela. Os testes existentes podem emitir avisos de recursos no encerramento.

Depois de editar compute shaders, importar novamente. Se includes permanecerem
em cache no editor, fechar e reabrir o editor; o cache pode ser renomeado para
backup com o editor fechado. Não apagar arquivos do projeto para forçar refresh.

## Limitações e cuidados

- Paredes muito finas recebem luz ambiente das bordas próximas.
- O scanner runtime consulta as layers disponíveis; alterações no streaming
  precisam continuar invalidando a região de luz.
- O campo direcional usa a ocupação lógica dos tiles, não cada pixel do sprite.
- A textura de luz e a máscara visual têm papéis diferentes: a máscara recorta
  onde desenhar, e não deve substituir Base/Compose no cálculo de ocupação.
- Preservar parâmetros ajustados pelo usuário e conferir o diff antes de reverter.

## Controles solares e atualização no editor

Em `LightMap → Settings`, `Sun Angle Degrees` controla a direção existente;
`Sun Color` controla a cor do feixe (branco preserva o resultado anterior).
`Sun Penumbra` controla a abertura. As curvas Shadow/Ambient moldam as duas
metades da transição, sem alterar a direção ou abertura geométrica.

LightMapData emite Changed ao alterar seus valores. O cache inclui ambas as
curvas na chave de atualização e solicita redesenho do campo. A cor é um
uniform dos materiais, portanto muda sem reconstruir a geometria. A cor do
feixe não tinge as fontes emissivas nem toda a luz ambiente do mapa.

Para testar as mudanças também dentro do editor:

```powershell
$env:WINDOW_BEAM_EDITOR_TEST='1'
godot_console --path Game --editor res://Testing/WindowBeamRegression.tscn
```

Esse teste altera o ângulo e as curvas, verifica pixels novos, notificações do
recurso e publicação da cor, além dos testes de janela e obstáculos.

## Node único da cena

As cenas Upsidedown e Overworld agora usam somente `LightMap` como node
configurável. O antigo LightingPreview foi removido. LightMap cria um helper
interno TerrainPreview no editor e concentra IncludeSkylight, caminhos Base/
Compose, PreviewPadding e PreviewRefreshIntervalSeconds, além de Settings.
PreviewInEditor liga/desliga a visualização; LightMapEnabled controla também
a visibilidade do overlay runtime. O manager continua responsável pelos chunks
em execução, sem exigir um segundo node de configuração em cada cenário.

O helper e sua textura são transitórios e não recebem Owner da cena.
As coordenadas do preview permanecem relativas ao nível mesmo quando o node
LightMap é deslocado. O teste LightPerformanceEditorCheck cobre atualização
de tiles, ausência de rebuild ocioso e desligar/religar o preview unificado.

## Composição: influência da luz sobre a sombra

Esta seção substitui as fórmulas de composição descritas anteriormente.
EnvironmentDarkness foi removido: ele alterava o brilho ambiental e não
representava o controle solicitado. O controle atual é AmbientLightInfluence
(0 a 1, padrão 0.75) no Settings do LightMap.

Para cada receptor wall-only:

- available = max(luz propagada, feixe * cor solar), limitado a 0..1;
- opacity = ShadowStrength * oclusão geométrica;
- fill = available * AmbientLightInfluence;
- resultado = available * (1 - opacity * (1 - fill)).

ShadowStrength continua sendo opacidade, inclusive onde não existe feixe de
janela. AmbientLightInfluence determina quanto a luz disponível preenche a
sombra. Não modifica pontos sem sombra e não inventa luz em lugares fechados.
Não há peso difuso fixo de 35%. A projeção analítica permanece no overlay;
a wall não multiplica sombra previamente. O terreno primário permanece no
caminho anterior de iluminação.

EnvironmentCompositionRegression usa o shader real com oclusão total
injetada para testar a composição independentemente da geometria. Verifica
opacidade 0/1, preenchimento numa área clara, área fechada, janela e invariância
da luz sem sombra ao variar a influência. Exemplo medido: luz disponível 0.8,
sombra máxima e influência máxima resultam em ~0.64; com luz disponível 0.05,
o resultado fica em ~0.004 (quantização da captura), sem clarear o ambiente
fechado como se fosse aberto.

## Correções de profundidade, cor e vedação dos ambientes

O caminho ativo agora separa fontes locais (mapa propagado existente) da luz
solar (`SkyAccessField`). O scanner dos consumidores editor/runtime coleta
somente emissores locais; os testes antigos do scanner continuam exercitando
sua API de semeadura, mas não representam mais o caminho solar ativo.

SkyAccessField calcula acesso ao céu em oito direções, sem cruzar sólidos nem
atravessar cantos fechados na diagonal. Uma célula sem sólido e sem wall é
uma entrada de céu. Depois, a luz entra nos sólidos a partir do ar iluminado,
com profundidade controlada por TerrainLightDepthTiles; não pode sair desses
sólidos para iluminar outro compartimento de ar. Assim uma parede fina recebe
luz exterior sem transportá-la para uma sala vedada.

O campo é calculado na CPU nas atualizações do cache, enviado no canal B de
window_cells/sky_access e amostrado no shader. A propagação de emissores locais
e o campo de feixe continuam nos caminhos GPU existentes. Não há trabalho
contínuo de propagação quando nada muda. Alterar TerrainLightDepthTiles faz
parte da chave de invalidação do cache.

SunColor multiplica a contribuição solar ambiente e o feixe, preservando as
cores das fontes locais. O brilho mínimo de exibição é aplicado depois da
composição; ele não é contado como luz disponível para AmbientLightInfluence.
A amostragem também impede que um pixel de ar completamente vedado tome luz
emprestada de um sólido vizinho por interpolação bilinear.

EnvironmentCompositionRegression verifica: vedação mesmo com grande
profundidade, abertura admitindo luz, profundidades 2/12 com resultados
diferentes no terreno, cor solar no ambiente sem feixe e invariância de uma
sala sem fontes quando AmbientLightInfluence varia. O teste de editor inclui
agora tanto a textura de fontes locais quanto o campo de céu nas comparações.

## Restauração do perfil visual do terreno

A transição linear com smoothstep introduzida em SkyAccessField foi removida,
assim como o meio passo na superfície. O terreno voltou ao perfil exponencial
original: SOLID_FALLOFF elevado à distância do passo. TerrainLightDepthTiles
escala essa distância por 3 / depth; no padrão 3, a sequência é exatamente
0.5, 0.25, 0.125 etc., como no propagador anterior. Aumentar depth alonga essa
mesma curva; diminuir encurta. O teste compara numericamente os quatro
primeiros tiles com o perfil original, dentro da quantização da textura.
As regras de vedação do ar e a separação de cor solar/fontes locais permanecem.

## Janela com influência ambiente zerada

O feixe transmitido pela abertura agora é composto depois da sombra ambiente:
`resultado = max(ambiente sombreado, feixe * SunColor)`. O feixe já verifica
obstáculos até a abertura e não deve ser bloqueado novamente pela projeção 2D
que enxerga o teto além dela. AmbientLightInfluence continua atuando somente
no preenchimento da sombra ambiente. O gradiente contínuo do feixe é preservado.

O teste de composição verifica feixe inteiro e de intensidade 0.5 com influência
zero e sombra máxima. WindowBeamRegression continua verificando bloqueio e
fechamento. LightPortWorldProbe captura `janela-sem-influencia-ambiente.png`
nessa configuração, restaurando os valores em memória depois da captura.

## Limpeza no início procedural

As alterações de performance desta etapa foram revertidas a pedido do usuário.
O campo voltou ao cálculo síncrono, com a região e publicação anteriores.
Permanece apenas a correção de limpeza descrita abaixo.

Na inicialização procedural, ClearLayers(discardBackground:true) descarta o
cache lógico das walls além da representação visual. ClearEntities remove o
container antigo da árvore imediatamente antes de agendar sua liberação.
O mesmo reset acontece no cliente ao receber ClearLayersReceive. O caminho
de mundo desenhado à mão conserva o conteúdo da cena. Mutações do save são
carregadas depois da limpeza do mundo procedural.

LightPortWorldProbe verifica que restaurar chunks após o descarte não traz
walls antigas de volta e que Entities fica vazio após a limpeza.

## Campfire e fontes de cena no mapa ativo

LightSource2D agora registra célula, cor e energia em SceneLightSources, isolado
por nível/dimensão. LightSourceScanner inclui essas fontes no mesmo lote dos
blocos emissores. Alterações invalidam os chunks antigos e novos no runtime;
o preview acompanha a revisão do registro. Fontes paradas não invalidam o
mapa a cada frame. Desligar, ocultar, remover ou mudar de nível retira o registro.
A sombra solar não multiplica a emissão local já propagada.

Removidos o caminho antigo de overlay de LightMap2D, LightOverlayEnabled,
LightMapComputer, layered_light.gdshader e os testes LayeredLightingRegression/
LayeredWorldIntegration exclusivos desse caminho substituído. O overlay
ProjectedShadow agora serve exclusivamente ao DebugShadow. Componentes
lógicos ainda referenciados pelo mundo e por testes de geometria foram mantidos.
As instruções históricas deste documento sobre ativar LightOverlayEnabled
não se aplicam mais.

SceneLightRegression instancia Campfire.tscn e verifica coleta, saída GPU,
propagação, estabilidade ociosa, enabled, energia/cor, movimento e remoção.
Executar com `godot_console --path Game res://Testing/SceneLightRegression.tscn`.
No editor, definir SCENE_LIGHT_TEST=1 e adicionar --editor ao comando.

## Cor global no background e contribuição local em superfícies claras

SunColor também modula o node Background (céu e parallax), preservando sua
modulação original e restaurando-a ao desligar/desanexar a iluminação.
A emissão local é somada depois da iluminação solar/sombra, em vez de usar
max entre as contribuições. Não há limite artificial de ganho 1 antes da
multiplicação pela cor da superfície.

TerrainLightOverlay lê a cor da cena via hint_screen_texture e compõe
explicitamente com blend_disabled; o blend_mul fixo limitava ganhos acima
de 1 e fazia a fogueira desaparecer sobre regiões já iluminadas. A máscara
continua limitando o efeito aos receptores. Isso exige uma cópia da cor da
cena pelo renderer por viewport; não foi feita outra otimização de desempenho.

O teste GPU de composição verifica ganho de brilho e tonalidade quente numa
superfície já iluminada. LightPortWorldProbe verifica a mudança de SunColor
no Background e salva background-cor-solar.png.
