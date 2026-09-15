# Iluminação por camadas — implementação atual

A sombra projetada usa exclusivamente a geometria lógica da camada principal. O cálculo analítico une os intervalos bloqueados do disco solar, sem excluir o próprio corpo, escadas, corpos conectados ou receptores sólidos. As regras anteriores estão desativadas.

A recepção é uma etapa separada: `wall_projected_shadow.gdshader` aplica essa projeção nos fragmentos da `BackgroundWallLayer`. A própria textura/alpha da wall fornece a máscara. Céu, tiles da frente e personagens não recebem essa projeção. O material anterior da wall é restaurado ao desativar a iluminação.

O mapa lógico considera as duas camadas: os blocos principais bloqueiam a propagação; as walls fecham a entrada de céu pelo fundo. Uma abertura sem wall fornece luz ao ar, que se propaga e perde intensidade. Fechar a abertura remove essa contribuição. Emissores RGB continuam usando o mesmo campo incremental, independente dos chunks renderizados.

`layered_light.gdshader` aplica a escuridão e a emissão do campo lógico. A recepção no interior do terreno mantém o cálculo de profundidade existente de `LightMapComputer`, separado da projeção solar.

A ocupação do terreno autorado e da prévia inclui `Base` e os blocos da `Compose`. `Base` tem prioridade nas células compartilhadas: uma célula presente só na Base não pode ser tratada como ar. A geração procedural já fornece a ocupação lógica antes de repartir a apresentação entre essas duas TileMapLayers.

## Controles

- `DebugShadow`: mostra somente a projeção bruta, sem máscara de wall e sem escuridão lógica. Branco significa iluminado; preto, oclusão total.
- `SunAngleDegrees`: direção solar.
- `SunPenumbra`: abertura existente; 0 abre o disco, 1 produz raios paralelos.
- `SunPenumbraShadowCurve` e `SunPenumbraAmbientCurve`: perfis das duas transições.
- `ShadowStrength`: intensidade da sombra recebida pelas walls.
- `TerrainLightDepthTiles`: profundidade da transição interna do terreno, em tiles.

A prévia do editor importa alterações das duas camadas periodicamente. Durante o jogo, as alterações vêm do mundo lógico. O debug pode ser alternado sem reconstruir a geometria. Não há feixe volumétrico adicional nesta composição.

## Verificação

`ProjectedShadowRegression.tscn` valida a projeção bruta e um bloqueador remoto. `LayeredLightingRegression.tscn` verifica abertura/fechamento de walls, remoção de emissão, reconstrução de chunks e recepção exclusiva nas walls. `LayeredWorldIntegration.tscn` verifica a composição no mundo autorado, alternância de debug e limpeza ao desativar.

---

## Histórico anterior (não descreve a composição ativa)

﻿# Iluminação lógica e sombras 2D

Implementação de setembro de 2026. Substitui a oclusão angular dependente da janela renderizada.

## Comportamento

- O mundo começa sem luz. Ar em uma sala opaca, com fundo fechado e sem entradas nem emissores, converge para zero.
- Sem parede de fundo, o ar recebe ceu pelo eixo de profundidade. Essa semente e atualizada incrementalmente; veja `BackgroundWalls.md`.
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
a fração angular ocultada da fonte. Isso produz umbra, penumbra e luz exterior sem degradês
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

Em `Assets/Data/LightMap.tres`. Um grupo por tipo de iluminação, cada um com o próprio interruptor
e os próprios parâmetros. O interruptor é **mudo**: desliga a contribuição sem destruir o valor
ajustado, e por isso não é redundante com colocar o slider em zero.

| Grupo | Interruptor | Parâmetros |
|---|---|---|
| Luz global | `GlobalLightEnabled` | `GlobalLightIntensity` (0,32), `GlobalLightColor` |
| Luz direcionada | `SunEnabled` | `SunAngleDegrees`, `SunIntensity` (0,85), `SunColor`, `SunPenumbra`, `SunPenumbraShadowCurve`, `SunPenumbraAmbientCurve` |
| Emissão de outras fontes | `EmissionEnabled` | `EmissionIntensity` (1) |
| Cálculo de profundidade | `DepthEnabled` | — |
| Feixe de abertura | `BeamEnabled` | `BeamReachTiles` (24) — ver pré-requisito abaixo |
| Luz volumétrica (poeira) | `DustEnabled` | `DustDensity` (0,12) |
| Sombra projetada no ar | `AirShadowEnabled` | `AirShadowStrength` (0,35) |
| Sombra projetada no terreno | `TerrainShadowEnabled` | `TerrainShadowStrength` (1) |
| Sombra projetada no background | `BackgroundShadowEnabled` | `BackgroundShadowStrength` (1) |
| Sombra projetada em entidades | `EntityShadowEnabled` | `EntityShadowStrength` (1) |
| Iluminação do terreno | `TerrainLightEnabled` | `TerrainLightDepthTiles` (3) |
| Dissolução da borda | `EdgeFadeEnabled` | `EdgeFadeTiles` (2) |

Detalhes que não cabem na tabela:

- `SunAngleDegrees`: zero é sol acima; positivo desloca o sol para a direita.
- `SunPenumbra`: 0 = cone mais aberto; 1 = projeção paralela. Controla a abertura dos dois lados do
  eixo, sem alterar o ângulo.
- `SunPenumbraShadowCurve` / `SunPenumbraAmbientCurve`: curva de intensidade em cada metade da
  penumbra, de 0 (sem degradê, apenas antialiasing) a 1 (degradê completo). Sempre usam toda a faixa
  existente; não alteram início, fim, largura nem ângulo. Uma única curva `pow(q*q*(2-q), controle)`
  altera a dureza, sem mistura de perfis. Não há controle Gradient global nem recorte por porcentagem.
- `TerrainLightDepthTiles`: profundidade visual até preto completo, de 0,25 a 16 tiles. Aceita
  frações. Aplica-se a todos os blocos, à luz solar, ao ambiente e à emissão recebida, e **não**
  transmite luz para o ar do outro lado da parede. A distância solar é medida em direção à luz;
  a recepção ambiente usa as faces expostas. A intensidade é `1 - smoothstep(0, 1, distância /
  profundidade)`, sem patamar inicial iluminado. Isto é recepção da superfície para dentro do bloco,
  e não se confunde com `TerrainShadowStrength`, que é a sombra projetada caindo sobre o terreno.
- `EdgeFadeTiles`: largura, em tiles, da rampa com que a escuridão dissolve a divisa com o bloco
  iluminado que a toca. Padrão 0,5. Existe porque um interior preto encostado numa face acesa
  fecha em linha reta e o escuro passa a ler como recorte sólido.
  O cálculo é **geométrico**, não vem da textura: para um pixel sólido mede-se a distância, dentro
  do próprio tile, até cada uma das 8 faces/quinas cujo vizinho é ar, e o bloco assume a luz desse
  vizinho na divisa, subindo até o próprio valor ao completar `EdgeFadeTiles`. Vale o menor fator
  entre as faces.
  Entra como **razão** sobre a energia já composta, e não como substituição do céu, porque quem
  mantinha a face acesa contra o preto era o termo solar; uma correção só no céu não chega nele.
  Vale apenas onde `material > 0.5`: aplicada ao ar, a escuridão de dentro vaza como halo cinza no
  céu aberto.
  Duas tentativas anteriores foram descartadas e não devem voltar: (a) `edge_blend`, peso do
  vizinho de outro material na leitura bilinear — com 2 texels por tile ela alcança meio texel e a
  linha continua; (b) erosão por amostras (`min` de "céu do vizinho + distância"), que dá manchas
  de vários tiles mas é presa à grade do texel e não resolve meio tile. A leitura interpolada
  também não serve de rampa: seu ponto médio cai **sobre** a divisa, então ela fundeia em meia luz
  e sua largura útil é um quarto de tile, não meio.
### Onde a oclusão é avaliada para receptor sólido

O tile sólido recebe luz na face exposta, então um DDA curto anda pelo raio até sair do sólido e a
oclusão é medida lá. O ponto usado é o **centro da célula de saída**, não o ponto exato em que o
raio cruzou a face (`p = vec2(c) + 0.5` em `analytic_sun_parts`).

O ponto exato é função do **trajeto**: dois pixels vizinhos do mesmo bloco saem em lugares
ligeiramente diferentes e medem a oclusão rasante à própria superfície, onde um deslocamento
mínimo troca quem bloqueia. Era daí que vinham os feixes de sombra finos riscando copas e terreno.
O centro da célula é função da **célula**: todo pixel que sai na mesma célula concorda.

Limitação conhecida e aceita: com uma amostra por célula, a penumbra sobre o sólido não tem onde
existir abaixo da escala do tile, então a sombra projetada em objeto tem borda mais dura que no ar.

Tentativas descartadas para suavizar essa borda, nenhuma aprovada, todas revertidas:
duas amostras por célula interpoladas; manter o ponto exato ancorando só a distância por célula
(devolve os feixes inteiros); recortar o bloqueador N linhas acima do receptor com recuo relativo
ao ponto avaliado (**racha uma sombra única em dois feixes**, porque pixels vizinhos cortam o mesmo
bloqueador em alturas diferentes); e a mesma ideia com linha de corte vinda da célula mais ponto
exato. O canal do sol do `ShowRawMap` **não** serve para atribuir culpa em nada disso: é
`(1 - oclusão) * profundidade` e ainda é composto por cima da textura do tile, então variação da
rocha aparece nele como se fosse material ou sombra.

- `ShowRawMap`: vermelho = ambiente, verde = sol, azul = opacidade.

### Feixe e poeira: por que são dois grupos

São dois efeitos do mesmo percurso, separados porque respondem a perguntas diferentes:

- **Feixe** (`window_beam`, entra no termo do sol em `sample_world_light`): **onde a luz chega**.
  Multiplicativo. Acende a parede, o chão e o personagem onde o facho bate.
- **Poeira** (`window_volume.gdshader`, sprite `blend_add` por cima): **ver a luz atravessando**.
  Não ilumina nada; soma brilho no ar para desenhar o cone.

A analogia é literal: na vida real não se enxerga o facho, enxerga-se a poeira dentro dele. Um
quarto de ar limpo mostra só o quadrado claro no chão; sacudir um tapete faz o cone aparecer.

**A dependência é assimétrica e não dá para remover.** Poeira sem luz não espalha nada: desligar
`BeamEnabled` apaga os dois. Desligar `DustEnabled` tira só o cone, e a superfície continua acesa
pelo facho. Medido num fixture com janela: com o feixe desligado, a poeira muda **0 pixels**; o
feixe sozinho dá 113/255 em 240 pixels, e a poeira acrescenta 14/255 em 146 pixels sobre os mesmos.

`DustDensity` não muda o tamanho do cone, só a densidade dele — o tamanho vem da abertura, do
ângulo do sol e de `BeamReachTiles`.

### Pré-requisito do feixe

`window_beam` sai de zero na primeira linha se o pixel **não tiver parede de fundo atrás**
(`receiver.b < 0.5`). O facho é luz entrando por um buraco na parede de fundo e precisa dessa parede
para existir. A geração procedural **não pinta parede de fundo nenhuma**: só o jogador colocando
`wall_wood`/`wall_dirt`, a reaplicação de mutações do save, ou o autor pintando a camada
`BackgroundWalls` no editor. Num mundo procedural recém-gerado o facho é zero em toda a tela, e
então `DustDensity` não tem o que escalar — não é fiação morta, é ausência de entrada.
A regressão do editor confirma a fiação: `volume_density` chega ao material do volume.

Atenção à camada `Base`: ela é o que se vê como fundo dentro de uma construção, mas é acabamento
visual e **não** conta como parede de fundo para a luz, por decisão explícita deste documento.
Uma janela aberta no **terreno** também não aciona o feixe: ele procura buraco na parede de fundo.

### Os quatro alvos da sombra projetada

`receiver_shadow_weight` escolhe um peso por pixel conforme o que está ali, com o sólido tendo
prioridade sobre a parede de fundo — quem aparece na tela é o tile, não a parede atrás dele:

    solido ? TerrainShadowStrength : (parede_de_fundo ? BackgroundShadowStrength : AirShadowStrength)

**O peso multiplica a oclusão, não a composição.** `analytic_sun_parts` devolve `(oclusão, recepção)`
separadas, e o sol vira `(1 - oclusão × peso) × recepção`. Peso 0 significa "nada de sombra
projetada aqui", com a iluminação do alvo inteira. A versão anterior aplicava o peso sobre a
composição final, e por isso `TerrainShadowStrength = 0` apagava a **iluminação** do terreno junto —
eram coisas diferentes tratadas pelo mesmo número.

Entidades (Sprite2D/AnimatedSprite2D sem material próprio) não usam esse peso posicional: elas têm
`EntityShadowStrength`, aplicado em `world_light_receiver.gdshader`.

**Limite conhecido do alvo de entidades.** O overlay vai multiplicar o sprite pela composição da
célula, então o material do sprite compõe duas vezes — com o peso da célula e com o seu — e divide
uma pela outra. Quando o peso da entidade deixa ela **mais clara** que a célula, essa divisão
exigiria escrever acima de 1, e o framebuffer LDR corta. Na prática: dá para deixar a entidade mais
sombreada que o entorno, não menos. O padrão 1 é o comportamento anterior, então nada regrediu.
A correção seria desenhar o overlay abaixo das entidades, e não é um ajuste de valor.

### Custo de alternar

`DepthEnabled` é o único que mexe no solver lógico e não só na apresentação: alterná-lo descarta e
reconstrói o cache de luz. Medido na cena procedural de teste, a apresentação volta a estabilizar
em **20 frames**. Os outros nove são uniformes e valem no frame seguinte.

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
- Regressão da projeção na GPU: contorno com pelo menos 5% de sombra cresceu de 38 para 52 pixels;
  paralelo manteve 34 pixels. A simetria em torno do eixo e o bloqueador fora da janela passaram.
  A composição filtrada foi verificada junto à borda interna de um teto fechado, sem vazamento.
- Regressão do editor: penumbra preserva o ângulo; apagar e recolocar um tile atualiza a geometria
  e o cache sem substituir o mundo lógico.
- Regressão de antialiasing a 8× de zoom: as 320 linhas verificadas têm cobertura subpixel,
  com transição de no máximo dois pixels na tela, sem ampliação da textura intermediária.
- A penumbra aberta tem degradê além do antialiasing: a faixa entre 5% e 95% de luz cresceu
  de 63 para 150 pixels com a distância ao bloqueador no teste a 8×, com gradiente 1,4.
  A transição integra a união das silhuetas, sem raios adicionais.
- Um retângulo sólido e a mesma silhueta subdividida em xadrez produzem a mesma sombra na GPU
  com gradiente 3, verificando a ausência de emendas internas geradas pela decomposição.
- Transição do terreno: 1 e 6 tiles validados na CPU e GPU, com preto além da profundidade
  escolhida. Os dois novos parâmetros também foram alterados e verificados no editor.
- Composição final: ambiente 0,5 + sol 1,03 testados na GPU, sem patamar branco na transição
  do terreno. Gradientes de penumbra 0,5 e 3 preservam o ponto de 50%; o maior reforca o contraste.
- Preparação da textura do fixture de 64 × 36 tiles: aproximadamente 16–21 ms no total nesta versão experimental,
  distribuível em fatias; não é um custo cobrado em todo frame.

Essas medições são cenários de teste, não garantias de FPS para qualquer mundo ou GPU.
Há avisos de UID e de liberação de texturas no encerramento do jogo. Os avisos de texturas
foram reproduzidos iniciando e encerrando somente a tela inicial, sem instanciar iluminação.


## Composição por camadas (experimento 2.5D)

O multiplicador final tem **peso** por camada, não presença. Terreno e parede de fundo recebem a
composição inteira; ar aberto recebe uma fração dela, definida por `AirShadowStrength`. Todo
bloqueador projeta em todo lugar — a diferença entre as camadas é de intensidade.

Uma versão anterior fazia isso com dois cortes: máscara zero em ar aberto e alcance de projeção
limitado a `AirShadowStrength` fora das paredes. O efeito não era reduzir a força, era
**apagar**: a sombra projetada só existia onde houvesse parede atrás, e bloqueadores distantes
paravam de projetar. Os dois cortes foram removidos.

Sprites sem material próprio recebem o mesmo cálculo por material, sem multiplicação em dobro
quando estão diante de uma parede. Como o overlay multiplica o sprite por `mix(1, luz, peso)`, o
material do sprite aplica `luz / mix(1, luz, peso)` — e não `1 - peso`, que só fecha nos extremos e
escureceria demais em pesos fracionários. Materiais personalizados precisam integrar
`world_light_sampling.gdshaderinc`; não são substituídos automaticamente.

Em `LightMapData`, grupo **Profundidade das camadas**:

- `AirShadowStrength` (padrão 0,35): força da sombra projetada onde não há
  superfície atrás, isto é, contra o céu aberto. 1 iguala à de uma parede de fundo;
  0 devolve o comportamento antigo, em que ar aberto não recebia sombra. Não muda
  onde a sombra existe, nem o ângulo, nem a abertura da penumbra.
- `WindowBeamReachTiles` (padrão 24): alcance do feixe vindo de aberturas do
  fundo, limitado pelo halo lógico de 24 tiles. A atenuação acompanha esse alcance.
- `VolumeDensity` (padrão 0,12): intensidade aditiva da luz visível no ar diante
  de paredes. Zero desliga essa contribuição visual.

Paredes de fundo conservam o teste completo de bloqueadores: um teto distante
continua impedindo luz direta em um recinto fechado. O feixe termina ao encontrar
terreno sólido e desaparece quando a abertura é fechada. O sol e os dois controles
de gradiente permanecem compartilhados entre as apresentações.

É uma aproximação artística por planos, não transporte volumétrico 3D: não há
coordenada Z contínua, espalhamento múltiplo nem integração física da profundidade.
O feixe usa o percurso e a máscara de abertura existentes; sua visibilidade é uma
passagem aditiva separada, sem escurecer o céu para simular contraste.

Validação: regressão GPU verifica que o céu **iluminado** continua intacto, que o céu em sombra
escurece exatamente pelo peso (pixel ≈ 1 − `AirShadowStrength`), recinto fechado com teto
distante, iluminação da parede e volume ao abrir/fechar janela. A regressão do editor verifica
a atualização dos três parâmetros nas duas passagens.

Comparação no mapa procedural real, peso 0 contra 0,35:
`.images/sombra_projetada_no_ar_peso_0_vs_035.png`.

Variações dos interruptores, no mesmo mapa: `.images/luz_toggles_tudo_ligado.png` e
`luz_toggle_sem_ceu`, `_sem_sol`, `_sem_luz_de_profundidade`, `_sem_peso_de_camada`. Emissão, feixe
e volume não têm captura porque essa cena não tem fonte local nem abertura de fundo; a fiação dos
três é verificada na regressão do editor.
