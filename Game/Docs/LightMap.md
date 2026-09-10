# Iluminação lógica e sombras 2D

Implementação de setembro de 2026. Substitui a oclusão angular dependente da janela renderizada.

## Comportamento

- O mundo começa sem luz. Ar em uma sala opaca, sem entradas nem emissores, converge para zero.
- Abrir uma passagem permite luz; fechá-la retira a iluminação que dependia dela.
- Céu ambiente e emissão RGB se propagam por quatro vizinhos, com atenuação positiva.
- Sol direto percorre linhas retas. As sombras atingem ar, personagens e superfícies dos tiles.
- Uma superfície pode receber luz sem transmiti-la através do bloco. O interior do terreno
  escurece; a profundidade da recepção visual é configurada em `TerrainTransitionTiles`.
- Todos os blocos, incluindo folhas, terreno e madeira, são opacos. Base é acabamento visual,
  não uma segunda parede: sua presença não fecha uma passagem que foi aberta na geometria Compose.

## Fonte de verdade

`LightMapManager` mantém um `LogicalLightWorld` por dimensão. Este contém:

1. Semente e parâmetros da geração procedural, ou a fotografia inicial do mapa autorado.
2. Alterações finais por coluna/célula, independentes dos nós e dos chunks renderizados.
3. Cache de colunas comprimidas em intervalos de materiais, limitado a 128 faixas de 32 colunas.
4. Um `LightingField`, cache derivado de luz organizado em chunks de 32 × 32.

A geração visual consulta esse mesmo mundo lógico. `ChunkGeneratorSystem.GenerateLogicalStrip`
produz terreno e árvores sem consultar TileMapLayer. A pintura e o descarregamento dos tiles
não criam nem removem obstáculos lógicos.

A fórmula de altura existente, inclusive a seleção por faixa vertical de chunk, foi preservada.
A colocação de árvores agora usa ocupação procedural determinística, sem depender de qual vizinho
foi pintado primeiro ou das decorações da Base. Árvores também são reconstruídas ao carregar a
parte superior de uma estrutura. **Saves procedurais antigos podem apresentar diferenças na
colocação de árvores**, pois armazenam semente e mutações, não a antiga geometria completa.

Mapas não procedurais usam `UseAuthoredWorlds()` antes de aplicar as mutações do save. O editor
faz uma fotografia própria de Compose e a atualiza quando a camada muda. Em runtime, a câmera
não é usada para determinar a existência dos obstáculos.

## Atualização incremental

`LightingField` é C# sem dependência do Godot e mantém quatro bytes de luz por célula: céu e RGB.
Há também opacidade, semente de céu e marca de fila: aproximadamente 7 KiB de arrays por chunk,
sem contar fila, objetos e dicionários.

Cada célula mantém o máximo entre fontes válidas e contribuições dos vizinhos após atenuação.
A perda no ar é 12 em uma escala de 0 a 255; matéria transmissiva acrescenta perda.
RGB é atenuado proporcionalmente ao maior componente para preservar a cor da fonte.

A fila única de relaxação trata aumentos e reduções. Toda mudança de valor agenda os quatro
vizinhos, e valores iguais interrompem esse ramo. Fontes alternativas são reavaliadas sempre.
Não se procura uma caverna, um volume fechado ou uma "região de sombra". O custo estritamente
positivo impede ciclos de luz autossustentada quando uma fonte é retirada.

Uma edição atualiza a opacidade local e reavalia as sementes de céu da coluna residente afetada.
Isso pode alterar uma coluna longa de verdade; incremental não significa custo constante.
`Process()` tem orçamento cooperativo padrão de 2 ms e 12.000 células por chamada.

`SetRegion()` pede uma área de interesse e acrescenta uma margem de 22 células, maior que o
alcance máximo da propagação. Bordas externas do cache são zero; essa condição não influencia
a área solicitada. Chunks podem ser descartados e reconstruídos do estado lógico, sem salvar
valores de borda obsoletos como se fossem fontes. Isso preserva caminhos por chunks invisíveis.

`Get()` é leitura interna do cache, incluindo o halo. Para gameplay, use `TryGetSettled()`:
`false` significa região indisponível ou ainda pendente, e não escuridão confirmada. Hoje o
apresentador pede a região da câmera; outros consumidores podem manter seu próprio LightingField
sobre o mesmo ILightGeometry. O céu normalizado e a emissão são separados da cor/intensidade do sol.

## Sol e renderização

O céu vertical é consultado nas colunas lógicas. Não se injeta luz no topo de cada chunk.
A luz vertical atravessa ar sem atenuação; qualquer bloco, incluindo folhas,
interrompe a contribuição, mesmo se estiver dez mil tiles acima e nunca tiver sido renderizada.

### Experimento: projeção analítica

`AnalyticShadowGeometry` combina intervalos verticais iguais de colunas adjacentes em
retângulos de material. Inclui as colunas lógicas acima da janela que podem projetar sombra
nela, até o teto lógico do mundo. Não depende dos tiles renderizados. Retângulos são indexados
em regiões de 16 × 16 tiles pela área de sua projeção, reduzindo os candidatos por fragmento.

A textura `shadow_geometry` contém retângulos, opacidade e listas espaciais em RGBA32F.
Não contém amostras angulares nem transmissão amostrada nas bordas. O shader calcula os
intervalos angulares bloqueados pelos retângulos e une todos os intervalos antes de integrar
a fração ocultada de um disco de luz. Isso produz umbra, penumbra e luz exterior sem degradês
individuais nas divisões internas dos blocos. No limite pontual, a cobertura do pixel usa
distância à borda e derivadas da posição.
Não há o laço anterior de 33 raios. Um DDA curto permanece somente para encontrar a face
receptora dentro de um bloco sólido, limitado à profundidade visual configurada (até 16 tiles).

`SunAngleDegrees` continua sendo o único eixo. `Penumbra` controla uma meia abertura de
`8° × (1 - p)²`: 0 abre o cone, 1 mantém a projeção paralela. Não existe fonte posicionada
nem referência de tile. Esta abertura é um controle visual estilizado, não uma fonte física.
As inclinações extremas são limitadas a 89,5 graus para evitar tangentes infinitas junto ao
horizonte; abaixo dele a contribuição solar é zero.

O passe auxiliar permanece com quatro pixels por tile para inspeção e regressões. A apresentação
final não amplia essa textura: usa `analytic_sun.gdshaderinc` para calcular a cobertura diretamente
por pixel da tela, com derivadas antes de alterar a posição do receptor. Assim o zoom não amplia
os degraus da textura intermediária. Não há amostras angulares nem filtro de várias direções.
A geometria e o campo lógico continuam em cache, mas a avaliação solar GPU agora ocorre em cada
frame na resolução da tela: esse é o custo da reconstrução direta, ainda sem benchmark de FPS.
Antes de publicar a geometria, `geometry_ready` mantém a contribuição solar zerada.
A escuridão e a emissão continuam vindo do solver incremental; não são determinadas pelo cone.

A construção dos retângulos e do índice é cooperativa, com orçamento de 3 ms. A lista solar
é reaproveitada quando só a emissão/iluminação lógica muda. Ao editar geometria, a lista da
janela é reconstruída nesta versão experimental; o solver de luz e seus receptores continuam
incrementais. A janela continua alinhada à grade e com margem de movimento. Mudanças de
ângulo agendam a próxima apresentação sem reiniciar indefinidamente a atual.

Todos os blocos usam o mesmo bloqueio de luz e a mesma composição de sombras, sem tratamento
especial para folhas. A união angular evita contar sobreposições mais de uma vez. A varredura
exata de intervalos pode ter custo quadrático no número de retângulos em casos fragmentados;
não usa número fixo de amostras nem descarta intervalos por um limite de capacidade. Muitos retângulos
projetados na mesma região aumentam o custo GPU. Teto muito distante e sol quase horizontal
podem exigir muitas colunas e memória de índice: ainda não há hierarquia de oclusores para
esse caso extremo. Upload, compactação e geração de uma coluna podem exceder a fatia cooperativa.

## Fontes locais

`LightSource2D` fornece emissão RGB para um nó de cena dentro de uma dimensão. Coloque-o em ar.
Ao mover ou remover o nó, suas contribuições são atualizadas. Serve para fontes transitórias.
Para uma fonte que deve sobreviver ao descarregamento de um prop, registre sua identidade e
posição diretamente em `LightingField.SetSource()` e mantenha esse registro no estado do prop;
não vincule sua existência à renderização. Ainda não há um item de tocha persistente novo.

O modelo RGB usa máximo entre contribuições, não soma física de energia. A propagação ambiente
aproxima iluminação indireta; não simula rebotes de materiais ou conservação de energia.

## Saves e rede

A iluminação é derivada e não precisa ser gravada como textura. Saves continuam guardando as
mutações do mundo, que são reaplicadas à geometria lógica antes de consultá-la.
Clientes recebem, ao entrar, a semente e um snapshot compacto das alterações ópticas finais,
separado dos chunks visuais e enviado em lotes. Destruições recebidas fora dos tiles carregados
também atualizam esse estado. O protocolo foi integrado, mas os testes incluídos não substituem
um ensaio com dois processos de rede.

A geometria procedural residente e os campos de luz são limitados pelo cache. As alterações
crescem com o mundo editado, e o histórico de mutações já existente no save continua crescendo.
O snapshot de entrada em rede é proporcional às células alteradas. O projeto ainda usa posições
Vector2 em parte da persistência: isso limita a precisão de coordenadas muito distantes.

## Ajustes

Em `Assets/Data/LightMap.tres`:

- `SunAngleDegrees`: direção solar; zero é sol acima, positivo desloca o sol para a direita.
- `Penumbra`: 0 = cone mais aberto; 1 = projeção paralela; padrão 0,5.
  Controla a abertura dos dois lados do eixo, sem alterar `SunAngleDegrees`.
- `PenumbraGradient`: suavidade do contraste da fração ocultada, padrão 1,4. 1 usa a integração
  do disco; aumentar suaviza simetricamente os dois lados da transição, preservando o ponto
  de 50% e os limites geométricos. Não cria transições separadas para cada retângulo.
  A curva é `c^a / (c^a + (1-c)^a)`, com `a = 1 / PenumbraGradient`.
- `PenumbraShadowSoftness` / `PenumbraAmbientSoftness`: controles independentes da borda
  junto a sombra / ao ambiente (0,25 a 4). 1 preserva a curva; menor marca o corte,
  maior suaviza a chegada. Cada controle atua somente em sua metade, preservando extremos,
  ponto central, abertura e angulo. No limite paralelo permanece apenas o antialiasing.
- `TerrainTransitionTiles`: profundidade visual até preto completo, de 0,25 a 16 tiles,
  padrão 3. Aceita frações (0,5 é meio tile). Aplica-se igualmente a todos os blocos, à luz solar,
  ao ambiente e à emissão recebida. Não transmite luz para o ar do outro lado da parede.
  A distância solar é medida em direção à luz; a recepção ambiente usa as faces expostas.
  A intensidade é `1 - smoothstep(0, 1, distância / profundidade)`, desde a superfície até
  preto completo, sem patamar inicial iluminado nem faixa de transição só no final.
  Valores maiores aumentam o trabalho de procura dessas faces, sem mudar o solver lógico.
- `AmbientInfluence`: energia do céu, padrão 0,32.
- `SunIntensity`: energia direta, padrão 0,85.
- `SkyColor` e `SunColor`: cor das contribuições.
- `ShowRawMap`: vermelho = ambiente, verde = sol, azul = opacidade.

A soma máxima das cores ambiente e solar é normalizada antes da atenuação local quando
ultrapassa 1. Isso evita que o corte final de cor apague o início do degradê: por exemplo,
ambiente 0,5 e sol 1,03 somavam 1,53 e mantinham uma faixa saturada. A normalização também
reduz a contribuição ambiente nas regiões sem sol. A emissão local é adicionada separadamente.

O recurso de configurações executa no editor (`Tool`), e as alterações do Inspector são lidas
pela prévia. O tamanho da prévia respeita `PreviewSize`. Durante a recarga de scripts, o iluminador
também lê as propriedades de recursos genéricos para não substituir os ajustes pelos padrões.

Alterações de tiles são comparadas com o estado anterior e aplicadas ao mesmo mundo lógico.
Além do sinal `Changed`, há uma conferência a cada 250 ms somente no editor: no teste real,
apagar/recolocar tiles não acionou o callback C#. A conferência percorre os tiles autorados da
prévia, mas só as diferenças invalidam iluminação. Esse trabalho não existe durante o jogo.

Não há piso artificial de brilho. O overlay ainda afeta o fundo atrás do mundo, como no sistema
anterior; sombras no ar são um efeito visual estilizado, não simulação volumétrica de névoa.

## Validação reproduzível

Da raiz do repositório:

```powershell
dotnet build Game/Game.csproj
dotnet run --project Tests/Lighting/Lighting.Tests.csproj -c Release
godot_console --path Game --resolution 1024x576 res://Testing/LightingRegression.tscn
godot_console --path Game --resolution 1280x720 res://Testing/LightingIntegration.tscn
godot_console --path Game --resolution 1280x720 res://Testing/LightingIntegration.tscn -- --procedural
$env:LIGHTING_EDITOR_REGRESSION = '1'
godot_console --headless --editor --path Game res://Testing/LightingEditorRegression.tscn --quit-after 1500
Remove-Item Env:LIGHTING_EDITOR_REGRESSION
```

O teste C# compara atualizações incrementais com um recálculo independente a partir das fontes,
incluindo paredes finas, remoção de fontes, caminhos alternativos, edições aleatórias, coordenadas
negativas e descarte/reconstrução do cache. A regressão Godot verifica céu e sol distantes,
restauração de alterações e pixels GPU dentro de uma sala fechada e sob um teto a 10.000 tiles.
A integração usa as cenas reais, compara ocupação lógica/renderizada, coloca e remove blocos e
verifica que descarregar tiles não altera luz. As cenas de teste não abrem nem salvam mundos do usuário.
A regressão do editor altera ângulo e ambiente em uma cópia do recurso e verifica os uniforms
atualizados e a invalidação do cache solar, sem salvar os ajustes usados no teste.

Capturas ficam em `.images/lighting-regression.png`, `lighting-authored.png` e `lighting-procedural.png`.
O executável headless pode testar a lógica, mas a validação dos shaders exige um renderizador real.

### Resultado da execução local

Em Godot 4.6 / Vulkan / Radeon RX 7600, build de desenvolvimento:

- Build C#: sucesso, com quatro avisos existentes em classes fora da iluminação.
- Solver independente: 468.488 verificações passaram.
- Regressão GPU: passou, incluindo sala fechada e teto distante.
- Regressão no editor: passou, incluindo alterações de ângulo e ambiente pelo recurso.
- Integrações autorada e procedural: passaram, incluindo o sol mudando continuamente.
- Edição pontual na cena procedural: 37 células processadas, apresentação em 3–4 frames.
- Regressão da projeção na GPU: contorno com pelo menos 5% de sombra cresceu de 42 para 60 pixels;
  paralelo manteve 34 pixels. A simetria em torno do eixo e o bloqueador fora da janela passaram.
  A composição filtrada foi verificada junto à borda interna de um teto fechado, sem vazamento.
- Regressão do editor: penumbra preserva o ângulo; apagar e recolocar um tile atualiza a geometria
  e o cache sem substituir o mundo lógico.
- Regressão de antialiasing a 8× de zoom: as 320 linhas verificadas têm cobertura subpixel,
  com transição de no máximo dois pixels na tela, sem ampliação da textura intermediária.
- A penumbra aberta tem degradê além do antialiasing: a faixa entre 5% e 95% de luz cresceu
  de 85 para 205 pixels com a distância ao bloqueador no teste a 8×, com gradiente 1,4.
  A transição integra a união das silhuetas, sem raios adicionais.
- Um retângulo sólido e a mesma silhueta subdividida em xadrez produzem a mesma sombra na GPU
  com gradiente 3, verificando a ausência de emendas internas geradas pela decomposição.
- Transição do terreno: 1 e 6 tiles validados na CPU e GPU, com preto além da profundidade
  escolhida. Os dois novos parâmetros também foram alterados e verificados no editor.
- Composição final: ambiente 0,5 + sol 1,03 testados na GPU, sem patamar branco na transição
  do terreno. Gradientes de penumbra 0,5 e 3 preservam o ponto de 50% e aumentam a faixa suave.
- Preparação da textura do fixture de 64 × 36 tiles: aproximadamente 16–21 ms no total nesta versão experimental,
  distribuível em fatias; não é um custo cobrado em todo frame.

Essas medições são cenários de teste, não garantias de FPS para qualquer mundo ou GPU.
Há avisos de UID e de liberação de texturas no encerramento do jogo. Os avisos de texturas
foram reproduzidos iniciando e encerrando somente a tela inicial, sem instanciar iluminação.
