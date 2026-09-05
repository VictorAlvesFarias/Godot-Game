# Mapa de luz — referência

O nó `LightMap2D` vive na cena de cada dimensão, ao lado das camadas de tile. Ele desenha, ao vivo
no editor e em jogo, quanta luz do céu chega a cada célula.

**Não é uma luz.** Não há `Light2D`, oclusor nem atlas de sombra envolvido. O resultado é uma
imagem em tons de cinza, **um texel por célula**, esticada por cima da cena com blend de
multiplicação — uma camada "multiply", recalculada quando algo muda. Onde o valor é `1.0` a cena
passa intacta; onde é `0.12` ela fica quase preta. Nada é iluminado: as coisas apenas deixam de
ser escurecidas.

> **Sombra projetada foi removida em 2026-09-04, para ser refeita do zero.** A versão anterior —
> varredura do sol por linha, sombra no terreno, sombra no ar, penumbra, dureza de material e
> sub-amostragem — está guardada em `.backup/sombra-projetada/`, junto com o histórico de
> defeitos que cada peça existia para resolver.

| Arquivo | Papel |
|---|---|
| `Features/World/Light/Systems/LightMapComputer.cs` | O algoritmo. Sem `Node`, sem cena, sem singleton |
| `Features/World/Light/Entities/LightMap2D.cs` | O nó: acha as camadas, define a janela, desenha |
| `Features/World/Light/Managers/LightMapManager.cs` | Roteador: traduz `dimensionId` no nó certo para o `Invalidate` |
| `Features/World/Light/Resources/LightMapData.cs` | Os ajustes, em `Assets/Data/LightMap.tres` |
| `Constants/LightMapConstants.cs` | Os valores padrão |

---

## No nó

### `LightMapEnabled` — padrão `true`
Liga e desliga o nó por inteiro.

### `PreviewInEditor` — padrão `true`
Se o mapa é desenhado dentro do editor. Ligado, ele é **refeito a cada quadro** — de propósito:
nada avisa quando você pinta um tile, e um preview parado não serve para calibrar. Em jogo o
recálculo é sob demanda.

### `PreviewSize` — padrão `120 × 80` células
Tamanho da janela de cálculo **dentro do editor**. Em jogo é ignorado: a janela sai da área
visível da câmera mais a margem.

### `Layers` — lista, padrão vazia
As camadas de tile que formam o terreno, **na ordem de prioridade**: a primeira que tiver algo na
célula decide. Em branco, o nó usa todas as `TileMapLayer` irmãs, na ordem da cena — que nas cenas
de dimensão dá `Base` e depois `Compose`. A primeira também é a referência de grid.

### `Camera` — padrão vazio
A câmera que a janela segue. Dá o **centro** e, em jogo, o **tamanho** (área visível ÷ zoom, mais
a margem). Em branco, usa o `Camera2D` irmão. O centro nunca é a posição do próprio nó.

### `Settings`
Aponta para um `LightMapData`. Todo o ajuste fino mora nele. As duas dimensões apontando para o
mesmo arquivo se calibram juntas; para uma ter ajuste próprio, duplique o `.tres`.

É tipado como `Resource` com dica de tipo, e não como `LightMapData` direto, de propósito: o editor
carrega a cena antes de ligar o assembly C# e entrega o `.tres` como `Resource` cru, sem instância
gerenciada. Com o tipo forte isso vira `InvalidCastException` toda vez que a cena abre.

---

## Nos ajustes (`LightMapData`)

### Luz do céu (difusa) — `DiffuseEnabled`
A pergunta que ela responde é **o quão exposta cada célula está, pela face mais exposta dela**.
Cada célula lança um leque de raios e acumula o custo **só da matéria** que atravessa; o ar é de
graça. Desligado, o terreno fica todo em brilho cheio.

| Propriedade | Padrão | O que é |
|---|---|---|
| `SolidCost` | 2 | Quanto um bloco absorve. Com 2 e nível 24, doze blocos matam o raio |
| `MinBrightness` | 0.12 | Piso de luminosidade; sem ele o fundo fica preto absoluto |
| `MaxLightLevel` | 24 | O orçamento do raio: quanto de absorção ele aguenta antes de morrer |

**O escuro vem de oclusão, não de distância.**

#### Vale o melhor quarto do leque, não a média dele

A média do domo mede **volume**. Ela não distingue *"enterrado no meio do bloco"* de *"pendurado
embaixo dele com céu limpo dos dois lados"* — só muda o grau. E pior: ela põe a **largura** do que
está em cima direto na conta de quem está embaixo. A copa tapa um cone de
`2·atan(metade da copa ÷ altura acima do ponto)`, então dobrar a largura da copa dobrava o cone e
escurecia o tronco — que não tem nada a ver com isso.

Um ponto é iluminado pela **face dele que está mais exposta**, não pela média de todas as direções.
Os quatro melhores raios dos dezesseis são essa face.

| Caso | O que os melhores raios acham | Resultado |
|---|---|---|
| Miolo de um bloco | todo raio morre na matéria | escuro |
| Boca de caverna | vários saem | claro |
| Tronco sob copa | o quarto lateral sai limpo | **cheio, em qualquer copa** |
| Fresta de caverna | um raio só sai, num quarto de seis | fraco |
| Boca de caverna | vários saem | claro |

Medido — brilho do tronco (0–255) variando **só** a largura da copa, com 11 de altura:

| Largura da copa | 3 | 5 | 9 | 13 | 21 | 31 |
|---|---|---|---|---|---|---|
| Tronco (meio e base) | 255 | 255 | 255 | 255 | 255 | 255 |
| Miolo da copa | 255 | 165 | 76 | 76 | 76 | 76 |
| Grama no sol | 255 | 255 | 255 | 255 | 255 | 255 |

A largura da copa saiu da conta do tronco por construção, sem knob nenhum, e o miolo dela continua
escurecendo — que é o comportamento pedido.

A folhagem tem custo próprio e menor (`COST_FOLIAGE`, 2), e é por isso que copa de árvore filtra a
luz em vez de cortar. Quem tem colisão é parede; o resto é folhagem.

#### Por que deixou de ser uma inundação em fila

Era um **campo de distância**: o nível de uma célula valia `MaxLevel − (passos até o céu)`, com uma
relaxação em fila semeada no topo de cada coluna. E um campo de distância cobra por **todo** passo,
inclusive os que atravessam ar vazio.

O sintoma era o tronco de uma árvore de copa larga ficar **preto**, mesmo enxergando meio horizonte
de céu limpo dos dois lados. Medido numa grade sintética com copa de 21 células e `MaxLevel 9`, a
linha de ar logo abaixo da copa — sem nada, nem folha nem bloco, entre ela e a clareira — decaía
`8, 7, 6, 5, 4, 3, 2, 1, 0`. **Ar vazio estava apagando a luz.**

Não havia termo nenhum no cálculo que representasse *"esta célula vê o céu"*: a única pergunta era
*"a quantos passos você está dele"*. Nenhum custo de ar resolvia isso: um único número governava
duas coisas que querem valores opostos — o quanto a caverna escurece com a profundidade (quer ser
alto) e o quanto o ar vazio deixa a luz passar (quer ser **zero**). Hoje o ar simplesmente não
absorve, e não há o que regular.

**Matéria é iluminada pela face exposta, não pelo próprio nível.** O brilho de uma célula de
matéria vale o maior nível entre ela e os quatro vizinhos, porque a luz que chega na face é a que
chega no ar encostado nela. Sem isso o raio rasante de um bloco de superfície sai pelo lado e viaja
*dentro* do chão, que é sólido: metade do leque morre no próprio terreno e a grama em pleno sol cai
para metade do brilho — medido, 4,5 de 9. Pelo vizinho de cima, que tem o semicírculo inteiro
aberto, ela dá cheio. É o mesmo mecanismo que faz o tronco pegar a luz do ar ao lado dele. Bloco
enterrado não tem vizinho de ar e continua no nível próprio, escuro.

#### Matéria não lança raio: ela é erodida

São duas contas, de propósito:

| | Como | O que mede |
|---|---|---|
| **Ar** | leque de 16 raios para cima, cada raio **sim ou não** | quanto de céu aquele ponto enxerga |
| **Matéria** | semeada pelo ar encostado nela, escurece para dentro | o quão **fundo** dentro da matéria o ponto está |

#### O mesmo leque dá duas medidas

| Medida | Como | Para que |
|---|---|---|
| **Melhor quarto** (4 de 16) | média dos melhores raios | semeia a matéria — a face mais exposta manda |
| **Fração do céu** (16 de 16) | média do leque inteiro | o **ambiente**, canal B, que o shader usa para afrouxar a sombra |

Usar o melhor quarto nas duas quantizava o ambiente: média de quatro raios binários só pode dar
`0 · 0.25 · 0.5 · 0.75 · 1`, cinco níveis, e a sombra ganhava manchas de borda seca. Com a média
inteira são 17, e com o rebote abaixo, 49 medidos numa janela real.

E a fração do céu não serve de semente pelo motivo já dito: ela põe a largura da copa na conta do
tronco.

#### O ambiente vaza pelo ar: o rebote

Só "que fração do céu eu vejo **daqui**" não basta. Debaixo de uma copa isso é quase zero mesmo com
a faixa iluminada a três células de distância — e aí duas sombras vizinhas, com a mesma luz em
volta, ficam com escuridão muito diferente conforme o que cada uma tem por cima. Na sombra real quem
preenche é a luz que bate no que está iluminado em volta e volta, e ela vem **dos lados**, não de
cima.

Então o ambiente escorre de um ponto de ar para os vizinhos de ar, perdendo `1 / AlcanceDoAmbiente`
por célula (20 hoje).

Mexer nesse alcance **não é a mesma regulagem** que `AirShadowOpacity`: como ele não atravessa
parede, ele clareia bolsão aberto longe da luz e **não toca em lugar vedado** — quarto lacrado
medido em 0 com alcance 10, 20, 40 e 80.

Já foi uma **difusão**, com parede refletindo, para tentar separar "bolsão aberto longe da luz" de
"fundo de túnel" pela largura da abertura em vez da distância. Medido, não pagou:

| | sob copa larga | túnel a 20 células | custo |
|---|---|---|---|
| Difusão, reflexão 0.8 | 37 | 13 | 41 ms |
| Propagação, alcance 20 | 32 | **6** | **12 ms** |

Mesmo ponto sob a copa, túnel mais escuro, um terço do custo. Medido, entrando por baixo de uma copa de 25 de largura, do sol para o centro:

```
sem rebote   175 159 143 127  63  31  15  15  15   0   0   0
com rebote   175 159 143 127 102  76  51  25  15   0   0   0
```

Penhasco de três células vira rampa de nove.

#### E leva uma média no fim

O leque tem um número fixo de raios, e **os mesmos ângulos em toda célula**. Andar uma célula faz a
borda de uma copa cruzar a fronteira de um raio, e o ambiente pula `1 / Amostras` de uma vez — às
vezes para cima, às vezes para baixo. Medido subindo ao lado de um tronco:

```
63 · 63 · 63 · 47 · 63 · 47      todos múltiplos exatos de 1/16
```

Como os ângulos são os mesmos em todo lugar, esse erro fica **coerente no espaço e vira mancha**,
não ruído. Triplicar os raios reduz o degrau mas não o elimina, e custa o triplo — medido, 48 raios
ainda deixam resto. A média resolve porque o erro é de alta frequência e o campo de ambiente não é:

```
16 raios, sem média    63 · 63 · 63 · 47 · 63 · 47 · 19
16 raios, 4 médias      58 · 57 · 57 · 56 · 52 · 45 · 30
```

Duas árvores idênticas em posições diferentes passam a dar perfis **idênticos byte a byte**.

Passa **só por ar**: parede corta. É o que mantém o quarto lacrado em zero — do contrário isto
desfaria o conserto do raio binário, vazando luz por dentro da parede.

#### O raio é binário: matéria é opaca

Já foi absorção acumulada, e aí o alcance do raio — `MaxLightLevel ÷ SolidCost` — valia como
**espessura de parede**. Com 14 e custo 4 o raio atravessava 3,5 células, e um quarto lacrado por
parede de **uma** célula media 182 de 255 de céu visível. Luz passando por parede.

| Espessura da parede | 1 | 2 | 3 |
|---|---|---|---|
| Antes | **182** | 36 | 0 |
| Agora | 0 | 0 | 0 |

Aquele mesmo número é o que diz quantos blocos a luz penetra no terreno. São duas perguntas
opostas — o degradê quer que ela vá fundo, a vedação quer que pare no primeiro bloco — e não cabem
no mesmo valor. Por isso o raio é binário, e o degradê fica por conta da erosão, que é outra
passada e tem o custo por célula só dela.

O degradê espacial não se perde: o que varia de ponto para ponto é **quantos** raios do leque
escapam, e isso muda suave. Medido num túnel entrando pela boca: `255 · 255 · 191 · 127 · 63 · 0`.

De quebra o cálculo caiu de 17 ms para **10 ms**: o raio morre na primeira célula de matéria.

A matéria já lançou leque também, e aí **a mesma forma sombreava diferente conforme o que existia
longe dela**. Medido em dois blocos 15×14 idênticos, um com tronco pendurado até o chão e outro sem:
o com tronco ficava com o núcleo escuro empurrado para baixo, porque os raios da face de baixo iam
bater no chão doze células abaixo.

Profundidade dentro da matéria não pode depender de nada que está fora dela. Por isso virou uma
**erosão da própria forma**. Medido agora, os dois blocos, linha por linha:

```
255 255 255 255 255 255 255 255 255 255 255 255 255 255 255
255 195 195 195 195 195 195 195 195 195 195 195 195 195 255
255 195 136 136 136 136 136 136 136 136 136 136 136 195 255
255 195 136  76  76  76  76  76  76  76  76  76 136 195 255
255 195 136  76  76  76  76  76  76  76  76  76 136 195 255
```

Idênticos byte a byte, com tronco e sem. Topo e laterais dão a mesma escada — `255 · 195 · 136 · 76`
—, que é o custo do bloco somando por célula de profundidade.

Cada célula de matéria com ar vizinho nasce com o nível **desse ar**, sem pagar custo: ela é a face
que recebe a luz, e cobrar já nela deixava a grama da superfície escura. Dali para dentro cada
célula custa o próprio custo.

É relaxação em fila, não largura simples: o custo varia por célula (folhagem e bloco têm custos
diferentes), então uma célula pode melhorar depois de já ter saído da fila.

A borda de **baixo** fica um degrau mais escura que a de cima, e isso não é a forma: é a semente. O
ar acima de um bloco vê céu; o ar embaixo dele, não. Sombra com direção é a projetada, que mora no
shader.

#### O custo

Recalcula inteiro quando a janela anda — a cada tile caminhado —, então precisa caber num quadro.
Medido em 140×90 com árvores, Debug, melhor de 8: **17 ms**.

Três atalhos, todos exatos:

1. **Desistência pela silhueta.** `_topoEsq[x]` e `_topoDir[x]` guardam a matéria mais alta olhando
   para cada lado. O raio só sobe, então quando passa acima disso não há mais o que encontrar. Sem
   isso ele caminha a janela inteira por ar vazio só para descobrir que era céu.
2. **Céu aberto por um teste.** Célula acima da silhueta dos dois lados vale `MaxLevel` direto, sem
   leque nenhum. É a maior parte da tela.
3. **Matéria não traça.** A erosão é uma passada em fila sobre as células de matéria — o subsolo,
   que é a maior parte da janela num mapa de caverna, sai praticamente de graça.

**O sol acende a face em que bate.** A difusa diz quanto de *céu* chega a um ponto; ela não sabe
nada do sol. Sem `SunLight`, a luz direta só cria sombra e nunca acende nada.

**Ela nunca escurece o ar — só o que tem matéria.** O ar carrega o nível difuso na textura, para o
shader saber se um ponto está em lugar aberto ou fechado, mas não é escurecido por ele.

#### O custo, e o que o segura

O recálculo roda inteiro quando a janela anda — ou seja, a cada tile que o jogador caminha —, então
ele precisa caber num quadro. Medido em 140×90, build Debug, melhor de 8 execuções:

| Versão | Tempo |
|---|---|
| Leque ingênuo | 90 ms |
| + desistência pela silhueta | 64 ms |
| + tabela de direções e céu aberto por teste | 31 ms |
| + atalho de célula enterrada | **17 ms** |

Três atalhos, todos exatos — nenhum aproxima o resultado:

1. **Desistência pela silhueta.** `_topoEsq[x]` e `_topoDir[x]` guardam a matéria mais alta olhando
   para cada lado. O raio só sobe, então quando ele passa acima disso não há mais o que encontrar e
   ele pode responder na hora. Sem isso ele caminha a janela inteira por ar vazio só para descobrir
   que era céu.
2. **Céu aberto por um teste.** Célula acima da silhueta dos dois lados tem todo raio limpo — vale
   `MaxLevel` direto, sem leque nenhum. É a maior parte da tela.
3. **Célula enterrada.** Um raio morre depois de `MaxLevel ÷ menor custo de matéria` células, então
   só o que está a essa distância pode mudar a resposta dele. Uma soma de prefixo de "quantas
   células de ar há neste retângulo" responde em O(1) se há ar ao alcance; não havendo, o nível é
   zero sem traçar nada. É o subsolo, onde estava quase todo o custo.

### Depuração — `ShowRawMap`
Mostra o mapa cru em cinza por cima da cena, bom para ler valor. Desligado, ele multiplica a cena.

---

## Como o valor de uma célula é calculado

1. **`PreencherGrade`** — lê as camadas e monta o custo de cada célula da janela.
2. **`MontarSilhueta`, `MontarLeque`, `MontarAcumuladoDeAr`** — as três estruturas que sustentam os
   atalhos acima.
3. **`CalcularVisaoDoCeu`** — por célula: céu aberto vale `MaxLevel` direto; célula enterrada vale
   zero; o resto lança o leque. **`Raio`** anda por DDA, saltando de fronteira em fronteira de
   célula, e acumula o custo da matéria que cruza. A célula de **origem** não é cobrada: a conta é
   do que tapa a vista dela, não dela mesma.
4. **`Desenhar`** — `brilho = lerp(MinBrightness, 1, nível/MaxLevel)` para matéria, `1` para ar.

O desenho escreve num buffer de bytes e monta a imagem de uma vez: cada `SetPixel` é uma chamada
de interop, e seriam dezenas de milhares por recálculo.

## Custo

O recálculo só acontece quando a janela anda ou um bloco muda — o `Invalidate` que o `TerrainLayer`
dispara ao colocar ou quebrar bloco. No editor é todo quadro, de propósito.
