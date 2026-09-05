# Sombra projetada — plano de ação

Refazer a sombra projetada do zero, por varredura, sobre o mapa de luz difusa que ficou de pé.
A versão anterior está em `.backup/sombra-projetada/`, junto com o registro dos defeitos que cada
peça dela existia para resolver.

## Onde estamos

O `LightMapComputer` faz uma coisa só: a luz do céu entra pelo topo de cada coluna e se espalha
perdendo intensidade por célula. Nenhuma direção, nenhum ângulo, nenhuma sombra.

## Onde queremos chegar

Duas sombras projetadas, cada uma com o seu interruptor: a que cai no terreno e a que fica no ar.

**Etapas 1 a 4: feitas.** A sombra no terreno foi dada como pronta, e a sombra projetada passou
para o shader — por fragmento, sem resolução de cálculo para regular.

A **penumbra** e a **dureza da iluminação do objeto** chegaram a existir e foram removidas por não
serem necessárias agora. As duas estão guardadas em `.backup/penumbra-e-dureza/`, com o código
completo e um README que explica como funcionavam e os defeitos que cada peça passou.

---

## Etapa 1 — A varredura do sol

O passo que dá direção ao mapa. Cada linha herda a de cima, deslocada segundo o ângulo do sol, e
perde energia ao atravessar matéria.

**Absorção, não desconto.** Cada célula de matéria multiplica o que chegou por uma fração. Com
desconto fixo o raio trava em zero e, a partir de umas poucas células, copa grossa e copa fina
projetam a mesma sombra — a densidade existe no cálculo mas satura e some.

**Sub-colunas.** A varredura amostra célula inteira, então com 1 sub-coluna a borda é um degrau
seco e o degrau se rearranja de uma vez quando o ângulo muda: dá para ver a sombra quebrar.
Medido varrendo o sol de −50° a −20°, o maior salto entre quadros cai de 2,17 para 1,38 células
indo de 1 para 4 sub-colunas. Só a varredura paga por isso.

**Ancoragem.** Raios paralelos, sempre. Fonte pontual foi tentada e a direção passou a depender da
posição: ancorada na janela, a sombra da mesma árvore invertia conforme o jogador andava; ancorada
numa célula fixa do mundo, longe dela a luz chega quase deitada e a sombra vira leque deformado.

## Etapa 2 — Sombra no terreno

A sombra que a árvore ou a construção deixa no chão e nos blocos.

**Arrasto.** O raio morre no primeiro bloco, então abaixo da superfície tudo fica em zero e a
coluna sombreada vale o mesmo que a iluminada. Sem diferença não há sombra para ver: medido numa
torre, três linhas abaixo da superfície o contraste era 0,00. Cada coluna precisa carregar para
dentro do terreno o valor com que entrou — com 6 células o contraste vai a 0,19 e a faixa passa de
1 para 5 células.

**O arrasto segue o raio, não a coluna.** Descendo reto, a faixa dentro do chão ficava vertical
enquanto a sombra no ar deitava no ângulo do sol — as duas não batiam. O mesmo vale para a cor,
no modo de dureza: as duas grandezas que formam a sombra têm que atravessar o terreno pelo mesmo
caminho.

**O valor caminha de volta para o sol cheio, não para zero.** Decaindo para zero, qualquer coluna
escurecia com a profundidade, inclusive uma em pleno sol, e a copa da árvore — que também é
matéria — ganhava um degradê escuro só por ser espessa. Isso não é sombra projetada: é a difusa,
que já faz esse trabalho.

## Etapa 3 — Sombra no ar

O volume de sombra: a região de ar que o sol não alcança. É o que faz uma entidade parada debaixo
da árvore escurecer junto — o overlay multiplica tudo que estiver embaixo dele, player incluso.

**Preço conhecido:** o fundo do céu também escurece nessa faixa, porque o overlay não distingue o
que está atrás dele. Por isso é peso regulável, e não liga/desliga.

**A difusa não entra nisso.** Ela não escurece o ar e não deve passar a escurecer: sem direção,
ela só mede distância até o céu, e o resultado seria uma coluna escura reta descendo da árvore que
não é sombra de nada.

## Etapa 4 — Sombra por raio, no shader

A sombra saiu da CPU. O `LightMapComputer` agora entrega uma **textura de dados**, um texel por
célula, e `Assets/Shaders/light_map.gdshader` traça o raio **por fragmento**.

```
R = 1 se há matéria ali, 0 se é ar
G = o brilho que a difusa deu àquela célula
```

**Por que sair da CPU.** A varredura encadeia — cada sub-linha copia a de cima deslocada por um
número inteiro de sub-colunas — e a borda só anda de sub-linha em sub-linha. Para afinar a borda
era preciso subir a resolução do cálculo *e* da textura, e o custo sobe ao quadrado: com 4
sub-células, 16× mais pontos. Foi o que ficou pesado.

No shader não há nada disso: a borda tem a resolução da **tela**, em qualquer zoom, e o custo é do
fragmento — não da área vezes sub-células.

**O que o shader faz**, por pixel:

1. Descobre a célula pelo UV e lê matéria e difusa da textura.
2. Traça o raio rumo ao sol por **DDA**, saltando de fronteira em fronteira: cada célula cruzada é
   visitada exatamente uma vez. Com passo fixo, um passo curto conta a mesma célula duas vezes e
   um passo longo pula bloco fino na diagonal.
3. **Acumula absorção** em vez de responder sim ou não — atravessar duas células de copa deixa
   passar menos que uma, e é isso que faz a sombra acompanhar a densidade.
4. Se o pixel está dentro de matéria, o raio **sobe até a superfície sem absorver**, contando a
   profundidade: a sombra é avaliada na entrada e desvanece com `ShadowDepth`. É o arrasto, agora
   por pixel.
5. Aplica o piso — `ShadowFloor` no terreno, `1 − AirShadowOpacity` no ar —, afrouxa esse piso
   conforme a **luz do ambiente** naquele ponto, e multiplica.

**A borda é um cone, não um raio.** Com um raio só a borda fica binária: o pixel bate ou não bate
no oclusor, e a transição acontece em um pixel, seguindo o degrau do bloco. Com o cone — cinco
raios abertos em torno da direção do sol, com a média — eles concordam perto do oclusor e divergem
longe dele, então a penumbra **abre com a distância sozinha**, sem precisar medir nada. Quem regula
é `ShadowSoftness`, em graus.

Vale notar o contraste com a penumbra da versão em CPU, que precisava de três regras e de um array
de distância desde o oclusor para não se dissolver. Na geometria do cone isso é de graça.

**A sombra é a razão entre o sol direto e a luz que chega de todo lado.** Num lugar aberto há muita
luz do céu preenchendo a sombra, e ela fica fraca; num lugar fechado sobra pouca luz para
preencher, e ela fica cheia. Quem regula é `AmbientInfluence`.

O ambiente é lido com **filtro linear**, e não célula a célula. Ele entra pelo topo de cada coluna
e não tem direção: lido cru, um buraco na copa vira uma coluna de ambiente alto descendo reta, e a
sombra afrouxa numa listra vertical de borda seca. Filtrado, o valor se mistura com os vizinhos e a
listra vira degradê — que é o que um buraco na copa faz de verdade. De quebra, o tronco para de
ficar preto: ele passa a dividir ambiente com o céu aberto dos lados.

Para isso a textura leva o nível difuso de **toda** célula, ar incluído, num canal próprio. Sem
isso o shader não teria como saber se um ponto de ar está em lugar aberto ou fechado — o ar não é
escurecido pela difusa, então ele não carregava esse número, e a sombra no ar tinha a mesma força
dentro de uma caverna e a céu aberto.

**Sem alcance máximo.** Sombra não tem comprimento arbitrário: ela vai até encontrar o que a
projeta ou até o céu. O limite é a própria janela.

**A difusa continua na CPU** de propósito: é uma inundação em fila, que não cabe bem na GPU e não
precisa de detalhe menor que um bloco — ela mede distância até o céu.

---

## Fora do escopo agora

- **Penumbra** e **dureza da iluminação do objeto.** Já existiram e funcionavam; ver
  `.backup/penumbra-e-dureza/`.
- **Passar a difusa também para a GPU.** Hoje ela é uma inundação em fila na CPU. Só vale a pena
  se ela virar gargalo — o recálculo dela é sob demanda, não por quadro.

## Como verificar de verdade

`--headless` **não compila shader**: erro de shader não aparece ali, e o Godot desenha a textura
crua no lugar. Para conferir, rode o jogo com renderizador e tire print:

```
godot --path Game --resolution 900x600
```

`.backup/probe/ProbeShader.cs` entra num mundo sozinho, espera, salva o print em `.images/` e sai.
Basta copiar para `Game/Testing/` e pendurar o nó em `Main.tscn`.

## Armadilhas registradas

Cada uma custou uma rodada de depuração na tentativa anterior:

| Sintoma | Causa |
|---|---|
| Sombra some no meio do dia | Ângulo perto de 0 dá inclinação ~0.03 e a sombra cai reta, dentro do próprio bloco |
| Leques de sombra saindo do chão para cima | Média da penumbra amostrando célula sólida, cujo solar é 0 por ter batido no bloco |
| Feixes de sombra separados | Borrão amostrando três pontos; cada raio maior virava cópia deslocada da borda |
| Penumbra descolada da sombra | Distância acumulando em pleno sol, então ar iluminado chegava ao raio máximo |
| Sombra muda enquanto o jogador anda | Oclusor fora da janela não existe para a varredura; margem curta demais |
| Sombra uniforme, sem variação | Dupla remapeagem no desenho comprimindo 0,22–1,00 em 0,65–0,89 |
| Sombra serrilhada mesmo com o raio | A textura tinha um texel por célula: nada menor que um bloco existia |
| Cálculo pesado ao afinar a borda | Resolução em sub-células custa ao quadrado; no shader é por fragmento |
| Textura crua aparecendo na tela (verde/laranja) | Shader não compilava: `TEXTURE` só existe dentro de `fragment()` |
| Sombra no ar com a mesma força em qualquer lugar | O ar não carregava o nível difuso na textura |
| Listras verticais de luz descendo da copa | Ambiente lido célula a célula; ele entra por coluna e não tem direção |
| Superfície em pleno sol com a cor de quem está na sombra | A luz direta só criava sombra; ela nunca acendia nada, e o brilho vinha só da difusa |
| Copa larga escurecendo o tronco | A difusa era um campo de distância e cobrava por todo passo, inclusive por ar vazio; nenhum termo do cálculo perguntava se a célula via o céu |
| Manchas de escuridão sem regra, em múltiplos de 1/16 | Aliasing do leque: ângulos fixos e iguais em toda célula, então o erro fica coerente no espaço |
| Sombras vizinhas com escuridão muito diferente, com a mesma luz em volta | O ambiente só media o céu visível dali; faltava o rebote, que vem dos lados |
| Ambiente em cinco níveis, com manchas de borda seca | Ele usava a média do melhor quarto, e média de quatro raios binários só dá cinco valores |
| Bordinha acesa em volta de todo sólido ao subir o AmbientInfluence | O ambiente era lido com filtro linear e borrava meia célula na fronteira ar/matéria |
| Quarto lacrado por parede fina enxergando 71% do céu | O raio acumulava absorção, então `MaxLightLevel ÷ SolidCost` valia como espessura de parede — 3,5 células |
| A mesma forma sombreando diferente conforme o que existe longe dela | A matéria lançava raio, então a face de baixo de um bloco ia bater no chão doze células abaixo |
| Núcleo escuro de um bloco encostado na borda de baixo, não centrado | O leque cobria só o semicírculo de cima; a face de baixo de uma laje não via nada e a de cima via tudo |
| Largura da copa escurecendo o tronco | A média do leque mede volume: o cone que a copa tapa cresce com a largura dela e entrava direto na conta de quem está embaixo |
| Grama em pleno sol pela metade do brilho | Raio rasante de um bloco de superfície sai pelo lado e viaja dentro do próprio chão |
| Céu aberto custando o máximo no cálculo | Raio caminhava a janela inteira por ar vazio só para descobrir que era céu |
| Tronco preto mesmo recebendo luz dos lados | Matéria usava o nível dela, que já pagou o custo de atravessar o bloco, em vez do da face exposta |
| Terreno escurecendo por ser grosso | Arrasto decaindo para zero em vez de decair para "sem sombra" |
| Copa escurecendo a si mesma no modo dureza | O modo trocava a conta inteira em vez de trocar só o piso da sombra |
| Faixa de sombra sumindo no chão, só no modo dureza | A cor era carregada na diagonal, mas o arrasto no terreno era vertical |
| Sombra no chão descendo reta, sem seguir o ângulo | O arrasto no terreno andava pela coluna em vez de seguir o raio do sol |
