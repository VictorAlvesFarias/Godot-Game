# LightMap

O `LightMap2D` desenha uma mascara de iluminacao sobre a dimensao. A CPU nao calcula luz: ela so
monta uma textura R8 de geometria, com um texel por celula do tilemap.

Nao existe amostragem de direcao em lugar nenhum. A sombra nao vem de contar raios: vem de medir,
em radianos, quanto da fonte cada celula ainda enxerga. Por isso a transicao claro/escuro e
continua por construcao, e nao um degrade montado a partir de N amostras.

## Pipeline

1. `LightMapComputer` le as `TileMapLayer` da janela visivel e escreve, por celula, a profundidade
   ate o ar mais proximo (Chebyshev, duas varreduras): `0` = ar, `1` = materia exposta, `n` = miolo.
2. `LightMap2D` envia essa textura para um `SubViewport` de `grade * SUBDIVISIONS`.
3. `light_map.gdshader` roda nesse `SubViewport` e calcula a luz na posicao continua do fragmento,
   nao no centro do tile. Com uma amostra por tile o contorno da sombra so podia mudar de valor de
   tile em tile, e virava escada.
4. `light_map_present.gdshader` estica o resultado por cima do mundo com `blend_mul` e aplica o piso
   da sombra.

O shader pesado roda em resolucao de tile subdividida, nao em resolucao de tela.

## Como a luz e calculada

Cada celula comeca com a fonte inteira visivel, representada como um intervalo angular:

- sol: `[-abertura, +abertura]`, onde `abertura` e a meia largura angular do disco solar;
- ceu: `[-90 graus, +90 graus]` em torno do eixo que aponta para cima.

Para cada oclusor, o shader calcula o intervalo angular exato que aquela celula ocupa vista do
ponto (via os quatro cantos) e **subtrai** esse intervalo da lista do que ainda esta visivel. No
fim, integra o que sobrou com peso cosseno em torno do eixo (`sin(b) - sin(a)`) e divide pela
energia da fonte cheia.

Sombra dura, penumbra e sombra suave sao o mesmo calculo: se o oclusor tapa o intervalo todo, sobra
zero; se tapa parte, sobra a fracao angular exata. A penumbra aparece sozinha porque a fonte tem
largura angular.

### Quem pode ocluir

- **Sol**: e direcional, entao so o cone que aponta para ele importa. O shader anda pelo eixo
  dominante do cone, uma linha de celulas por vez, e em cada linha visita o intervalo **contiguo** de
  celulas que o cone atravessa. Isso importa: amostrar pontos ao longo do raio deixa buracos, e
  buraco em oclusor vira ponto claro dentro da sombra. Fonte maior = faixa mais larga = mais
  trabalho, o que e a relacao fisica certa.
- **Ceu**: cobre o hemisferio de cima, entao so celulas na mesma linha ou acima podem tapar algo.
  Varredura em meio disco de raio `SKY_RANGE_CELLS`.
- Em ambos, so a **casca** solida (profundidade `1`) e testada. Qualquer raio que chegue no miolo ja
  atravessou a casca, entao ignorar o miolo nao muda a uniao dos intervalos bloqueados e corta a
  maior parte do custo.
- A busca para assim que a lista de intervalos visiveis esvazia.

### O piso da sombra

`AirShadowOpacity` e `SolidShadowOpacity` sao pisos diferentes: ar em sombra pode ir a zero enquanto
o tile para em, digamos, 0.52. Esse piso **nao** e gravado na textura de luz. Se fosse, a
interpolacao bilinear levaria o piso da materia para dentro do ar vizinho e a juncao da sombra com o
bloco aparecia clara. Ele e aplicado na apresentacao, que le a geometria por `texelFetch` e por isso
troca de piso exatamente na borda do tile.

### Dentro da materia

A celula solida recebe luz pelas faces expostas e perde por absorcao ao longo da profundidade ja
calculada na CPU: `exp(-MaterialAbsorption * (profundidade - 1))`. Todos os tiles usam o mesmo
material, como combinado.

## Ajustes

Os parametros ficam em `Assets/Data/LightMap.tres`:

| Campo | Uso |
|---|---|
| `SunAngleDegrees` | direcao do sol; zero aponta para baixo |
| `SunLightColor` / `SunIntensity` | cor e forca do sol |
| `Penumbra` | distancia aparente da fonte; 1 e sol pontual, 0 abre o disco todo (`PENUMBRA_MAX_DEGREES`) |
| `SkyLightColor` / `AmbientInfluence` | cor e forca da luz do ceu |
| `AirShadowOpacity` | piso da sombra no ar |
| `SolidShadowOpacity` | piso da sombra em tiles |
| `MaterialAbsorption` | perda ao atravessar materia ate uma face exposta |
| `ShowRawMap` | mostra a textura de profundidade usada pelo shader |

`SUN_RANGE_CELLS`, `SKY_RANGE_CELLS` e `SUBDIVISIONS` ficam em `LightMapConstants`. Os dois
primeiros sao o orcamento de busca do shader, em celulas. `SUBDIVISIONS` e quantas amostras de luz
existem por celula em cada eixo: e o que decide se o contorno da sombra pode ser uma reta.

## Serrilhado no contorno

Se a borda da sombra aparecer em degraus, a causa quase nunca e a resolucao do mapa de luz: as
rampas de penumbra sao continuas e da para conferir amostrando uma linha de pixels atravessando a
borda (deve subir sem patamar). O degrau que sobra e a **silhueta do tile**, que e um quadrado.

Ele aparece quando a penumbra e mais estreita que um tile, e a largura da penumbra e
`2 * distancia * tan(abertura)`. Ou seja: perto do oclusor a sombra e dura de proposito, e o unico
controle e o tamanho angular da fonte. Aumentar `SUBDIVISIONS` nao resolve isso, so gasta GPU.

## Limites

O modelo nao e path tracing. Ele nao faz BRDF por material nem rebote acumulativo. Rebote real
precisa de passes ping-pong de textura, nao cabe dentro de um fragment shader unico.

A oclusao so e considerada dentro do alcance acima. Alem dele a fonte conta como visivel.
