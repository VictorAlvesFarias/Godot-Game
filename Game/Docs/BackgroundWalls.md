# Paredes de fundo

`BackgroundWalls` em Overworld e Upsidedown e uma TileMapLayer independente, atras
 de Base/Compose e dos personagens. Pinte essa camada no editor. Seu TileSet proprio
 fica em `Assets/Textures/Tiles/background_walls_tileset.tres`; reutiliza as texturas
 atuais como ponto de partida, permitindo substituir os tiles de fundo separadamente.
 Colisao, navegacao e oclusao nativa sao desativadas. Self Modulate distingue o plano
 de fundo (0,65 por padrao).

## Durante o jogo

Itens `wall_wood`, `wall_dirt` e `wall_hammer`: paredes de madeira/terra e martelo.
 O comando existente `give_all` disponibiliza esses itens para teste. A colocacao
 consome uma parede; o martelo remove apenas o fundo e devolve paredes dos dois tipos
 iniciais. O alcance e o indicador seguem o sistema de itens. Fundo e terreno podem
 ocupar a mesma coordenada sem substituir um ao outro. Novos blocos de fundo devem
 ter `IsBackground = true` no BlockDB e um item com `Background = true`.

## Luz e profundidade

As paredes recebem a composicao de ceu, emissao RGB e sombras solares. A ausencia
 de fundo em uma celula de ar e uma abertura no eixo de profundidade: injeta ceu
 ambiente no solver incremental, mesmo sob teto e entre paredes frontais.
 Colocar o fundo remove essa fonte; remover um tile abre uma fonte que se propaga
 lateralmente com atenuacao. O fundo nao bloqueia movimentos nem a propagacao no
 plano XY: bloqueia apenas a entrada por profundidade. Uma sala com teto, laterais
 e fundo completos fica escura se nao tiver outra abertura ou emissor.
 Essa contribuicao usa SkyColor e AmbientInfluence; o sol direcional e a penumbra
 continuam no plano XY. Trata-se de uma aproximacao 2,5D com entrada por profundidade,
 nao de transporte fisico completo em 3D. Sem fundo autorado ou gerado, cavidades
 procedurais tambem ficam abertas por profundidade.

O conjunto de paredes existe no LogicalLightWorld, separado dos tiles renderizados.
 Uma edicao altera somente a semente local e a fila de propagacao: nao reconstrui
 o indice de sombras nem percorre a coluna inteira. O snapshot optico em rede inclui
 o fundo tambem fora dos chunks visiveis. A previa do editor aplica diferencas de
 fundo ao mesmo mundo logico. Salvar/carregar reaplica as mutacoes existentes.

## Persistencia e streaming

Mutacoes `wall_place` / `wall_break` viajam no mesmo estado de chunks e save das
 edicoes existentes. A carga de saves tambem as aplica em mapas autorados, onde o
 streaming fica desligado. O estado logico de paredes e separado da TileMapLayer:
 descarregar apaga so os tiles visuais; carregar restaura o estado por chunk.
 Paredes autoradas fazem parte da cena. Chunks sem paredes nao recebem geracao
 procedural automatica nesta versao. A memoria de paredes cresce com os tiles
 autorados e alterados, e nao com os tiles de terreno procedural.
 Edicoes em rede usam autoridade e RPC confiavel; o teste cobre replay de estado,
 mas ainda nao uma partida com dois processos conectados.

## Validacao

`dotnet build Game/Game.csproj`

`godot_console --path Game res://Testing/LightingIntegration.tscn -- --walls`

`godot_console --path Game res://Testing/LightingIntegration.tscn -- --procedural`

A integracao testa colocacao, remocao, exportacao/importacao, coordenadas negativas,
 descarregamento/restauracao e edicao remota sem renderizar chunks ausentes.
 A captura de demonstracao fica em `.images/lighting-background-walls.png` e nao
 modifica as cenas nem os saves do jogador. O teste de editor verifica que pintar
 BackgroundWalls nao cria um oclusor frontal. A regressao de luz testa a sala com
 fundo fechado/aberto/fechado, propagacao lateral, descarte de cache e composicao GPU.

## Feixe direcional das janelas

A composicao inclui uma contribuicao direta das aberturas no fundo sobre os
 receptores de fundo, usando SunAngleDegrees e a abertura angular de Penumbra.
 O feixe perde intensidade exponencialmente (escala de 12 tiles) e termina
 suavemente entre 20 e 24 tiles. Blocos frontais interrompem seu percurso.
 O mapa auxiliar possui margem logica de 24 tiles, inclusive fora da tela; a
 revisao de fundo invalida sua publicacao mesmo quando a luz ambiente nao muda.

Esta versao usa uma travessia de grade por pixel receptor (ate 64 cruzamentos),
 com mascara de abertura filtrada por mipmaps conforme a abertura do cone.
 E uma aproximacao visual de feixe em 2,5D: a oclusao segue o eixo central e nao
 integra fisicamente todos os raios da penumbra. O mapa auxiliar e gerado nas
 atualizacoes da iluminacao, e a avaliacao ocorre na GPU a cada frame. Ainda
 requer benchmark em cenas com grandes paredes ocupando a tela inteira.
 Nao ha espalhamento multiplo nem iluminacao volumetrica 3D completa.

A regressao GPU isola a contribuicao direta com ambiente zero, verificando
 projecao abaixo da janela, ausencia fora do eixo, fechamento da abertura,
 bloqueio por terreno e mudanca de direcao com o sol.
