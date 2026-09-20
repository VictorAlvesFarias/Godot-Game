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

Walls fecham a entrada de céu pelo fundo. `SkyAccessField` calcula essa entrada
a partir da união dos sólidos de Base/Compose e da presença de walls. O campo
se propaga pelo ar e, em seguida, é absorvido pelo terreno. Walls também recebem
a sombra analítica e a transmissão pelas janelas na composição final.

O estado lógico permanece independente dos chunks renderizados. Mudanças de
fundo incrementam sua revisão, invalidando o campo de acesso e os feixes. O
snapshot em rede inclui walls fora dos chunks visíveis. Veja [LightMap](LightMap.md).

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

`ClearRenderedForStreaming` descarta apenas os tiles desenhados.
`ResetForNewWorld` descarta também o cache autorado; o save é aplicado depois.
