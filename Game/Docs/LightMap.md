# Iluminação lógica e sombras 2D

Implementação de setembro de 2026. Substitui a oclusão angular dependente da janela renderizada.

## Comportamento

- O mundo começa sem luz. Ar em uma sala opaca, sem entradas nem emissores, converge para zero.
- Abrir uma passagem permite luz; fechá-la retira a iluminação que dependia dela.
- Céu ambiente e emissão RGB se propagam por quatro vizinhos, com atenuação positiva.
- Sol direto percorre linhas retas. As sombras atingem ar, personagens e superfícies dos tiles.
- Uma superfície pode receber luz sem transmiti-la através do bloco. O interior do terreno
  escurece; a recepção visual se estende por até três células, com atenuação de 0,43 por camada.
- Folhas (terrain 7) têm extinção 72/255. Terreno e madeira são opacos. Base é acabamento visual,
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
A luz vertical atravessa ar sem atenuação e folhas com transmissão parcial; uma parede opaca
interrompe a contribuição, mesmo se estiver dez mil tiles acima e nunca tiver sido renderizada.

Para o sol direcional:

1. `LightMapComputer` prepara uma textura RGBA de ambiente e material (canal G reservado),
   uma textura de emissão RGB e um atlas com a transmissão solar nas bordas da janela.
2. Cada amostra da borda consulta intervalos das colunas lógicas até atingir o céu ou um bloqueio.
   A distância não tem um corte arbitrário de 96 tiles. Espaços verticais vazios são saltados.
3. `light_map.gdshader` faz DDA local em 33 direções em torno de SunAngle. Quando um raio sai da
   janela, continua pela transmissão lógica da borda correspondente. O passe gera somente sol.
4. `light_map_present.gdshader` combina ambiente, emissão e esse sol, multiplicando as cores do
   mundo. Oclusão solar não apaga a contribuição de uma fonte local.

O passe solar roda com seis subdivisões por tile, só quando geometria, janela ou direção mudam.
O ambiente tem duas subdivisões para distinguir recepção nas faces dos blocos. A reconstrução
bilinear de ambiente/emissão não mistura materiais diferentes, evitando halos nas paredes.
O atlas de borda tem amostras a cada meio tile; sombras distantes são uma aproximação filtrada.
`SunAngleDegrees` é o único eixo da projeção, igual em todos os tiles. `Penumbra` controla
um cone simétrico em torno desse eixo: meia abertura de `8° × (1 - p)²`, de aberto em 0 a
paralelo em 1. Não há fonte posicionada, referência de tile nem dependência da câmera.

O passe combina o contorno externo das oclusões do cone, preservando a transmissão parcial
dos materiais e suavizando sua franja. Assim a sombra inteira se abre com a distância ao
obstáculo, sem apenas desfocar uma projeção paralela. Este é um controle visual estilizado,
não uma simulação física de uma única fonte próxima. A resolução do passe permanece fixa.
São 33 amostras, ou uma no caso paralelo. O custo adicional ocorre na reconstrução do cache.

No material opaco, o sol recebe luz pela face voltada à fonte, com atenuação contínua e alcance
visual de três tiles. Ambiente e emissão consideram as quatro faces expostas, sem escolher
abruptamente uma única face mais próxima. Isso evita recortes internos nos cantos. A recepção
na superfície não altera a propagação lógica e não transmite luz através de paredes.

A janela é alinhada à grade e tem margem para movimento, evitando recalcular ao andar um pixel.
Uma mudança local reaproveita os receptores fora de uma vizinhança de três tiles. Bordas solares
são reaproveitadas se a edição ocorreu dentro da janela. Emissão móvel não refaz os raios solares.
A preparação das texturas é dividida em fatias de 3 ms. Uma região inicial fica preta até estar
pronta; depois as texturas são publicadas completas. Mudanças do ângulo durante uma preparação
agendam o próximo resultado, em vez de reiniciar indefinidamente a preparação atual.

Os orçamentos são cooperativos: geração inicial de colunas, uploads e GPU têm custos adicionais.
Um raio quase horizontal pode consultar muitas colunas. Não há garantia de custo constante para
sombras extremamente longas; um índice hierárquico adicional seria necessário nesse caso extremo.

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
- `AmbientInfluence`: energia do céu, padrão 0,32.
- `SunIntensity`: energia direta, padrão 0,85.
- `SkyColor` e `SunColor`: cor das contribuições.
- `ShowRawMap`: vermelho = ambiente, verde = sol, azul = opacidade.

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
- Regressão da projeção na GPU: cone aberto cresceu de 62 para 88 pixels; paralelo manteve
  48 pixels. A simetria em torno do eixo e o bloqueador fora da janela também passaram.
- Regressão do editor: penumbra preserva o ângulo; apagar e recolocar um tile atualiza a geometria
  e o cache sem substituir o mundo lógico.
- Preparação da textura do fixture de 64 × 36 tiles: aproximadamente 37 ms no total,
  distribuível em fatias; não é um custo cobrado em todo frame.

Essas medições são cenários de teste, não garantias de FPS para qualquer mundo ou GPU.
Há avisos de UID e de liberação de texturas no encerramento do jogo. Os avisos de texturas
foram reproduzidos iniciando e encerrando somente a tela inicial, sem instanciar iluminação.
