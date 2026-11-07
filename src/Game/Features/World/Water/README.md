# Água

Este é o ponto de entrada da feature: **como a água funciona hoje**, quem calcula o quê e o que dispara
cada etapa.

---

## Onde cada peça mora

```
Features/World/Water/
├── Systems/      WaterField (níveis e simulação), WaterGenerator (lagos e poças pela seed),
│                 WaterReach (previsão de alcance)                         (C# puro)
└── Entities/     WaterLayer (desenho)

Features/World/Dimensions/Entities/Dimension.Water.cs   (ciclo de vida, rede, chunks só de dados,
                                                          água adiada)
Assets/Textures/Tiles/water/                              (tileset de 8 alturas × superfície)
```

---

## 1. Dados — `WaterField`

- Um `byte[1024]` por chunk carregado: nível de 0 (seca) a 255 (cheia) por célula. A exibição usa 8
  alturas (`WaterField.Height`).
- A célula que colide (pelo `TerrainCache.Collides`) não guarda água.
- Só o servidor simula (`Simulated`); o cliente aplica os níveis que chegam e desenha.

## 2. Simulação (servidor, 10 passos/s)

- Processa só as **células ativas** (as que mudaram e os vizinhos), de baixo para cima, alternando o
  lado a cada passo. Uma célula sai do conjunto depois de 3 passos parada.
- **Cair:** passa para a célula de baixo o quanto couber.
- **Espalhar:** com a de baixo cheia ou bloqueada, a célula e os lados livres com 2 ou mais a menos ficam
  com a média.
- Até `MaxWaterCellsPerStep` (4096) células por passo. Acima disso, cada passo pega uma janela que gira,
  para nenhuma frente ficar parada.
- Edições de terreno acordam a água em volta, e colocar um bloco que colide tira a água da célula. A
  água que chega numa tocha a quebra, com drop.

## 3. Geração — `WaterGenerator`

- **Lagos:** um ruído por coluna marca regiões da superfície. Em cada região, a água enche o ar entre a
  superfície gerada e a menor das duas bordas.
- **Poças:** no fundo de cavernas abaixo de uma profundidade, só onde a linha fecha em paredes dos dois
  lados (bacia).
- Só pela geração (sem edições), então a mesma seed dá a mesma água. Só em mundos procedurais, e só para
  chunks sem água salva (tabela `water`).

## 4. Residência: chunks com tiles e chunks só de dados

- Um chunk de jogador (com tiles) carrega a água junto (`LoadWater`) e a grava ao sair (`UnloadWater`).
- **A água posta em movimento por um jogador segue além dos chunks dos jogadores.** Uma edição de terreno
  marca a célula e os vizinhos (`ActivateAround(cell, driven: true)`), e a marca passa para as células que
  recebem água delas (`IsDriven`). Ela fica enquanto a célula tem água. A água sem marca (lago gerado se
  acomodando) trata a borda como parede. Quando a água marcada quer entrar num chunk não
  carregado, a célula de origem fica esperando, ativa, e o chunk vai para a fila. O servidor trata até
  `MaxWaterChunkLoadsPerStep` (2) pedidos por passo:
  - **pode chegar a um chunk de jogador:** o chunk entra **só de dados** (`LoadWaterOnly`): edições dele e
    da margem, e a água salva ou gerada. Sem tiles e sem envio a ninguém;
  - **não chega:** a água é **adiada** (seção 6).
- Um chunk de jogador que sai com água em movimento vira só de dados, e a água continua até assentar.
- Um chunk só de dados parado por `WaterOnlyIdleSteps` (30) passos é gravado e sai da memória. As
  edições dele ficam enquanto a água usa o chunk (`ReleaseFarChunkData` pula esses chunks e a margem).

## 5. Previsão de alcance — `WaterReach`

- Uma busca sobre o terreno lógico, sem simular, a partir das células para onde a água quer ir:
  - desce livre;
  - anda para os lados só com apoio (sólido embaixo, ou célula já alcançada);
  - nunca sobe;
  - a mais funda primeiro.
- **Orçamento de volume:** cada célula andada para o lado custa 1 unidade do volume da água conectada
  (`WaterReach.Body`). É um limite superior: nunca diz "não chega" para uma água que chegaria.
- Carrega do banco as edições dos chunks por onde passa (vê túneis cavados longe).
- Chega quando entra numa célula seca de chunk com tiles de algum jogador. Acima de 200 mil células
  visitadas, responde "pode chegar".
- **"Não chega" por geometria:** o chunk vai para `_deferredChunks`, e novos pedidos dele só param a
  água, sem refazer a busca, até o terreno mudar ou um jogador carregar um chunk.
- **"Não chega" por volume:** não entra na memória. A água que ainda vem refaz a previsão com volume
  maior.

## 6. Água adiada

- **No banco:** a frente (células onde a água parou) vai para `water_pending`, e a região alcançável para
  `water_pending_chunks` (um chunk por linha, para achar por chunk).
- **Retomada** (`ResumePendingWater`): quando um jogador carrega um chunk da região, ou edita o terreno
  dentro dela, as células da frente voltam a ser simuladas de onde pararam. A próxima saída do residente
  refaz a previsão.
- **Save:** as células ativas dos chunks só de dados viram água adiada do tipo "save"
  (`SnapshotWaterFronts`, `kind` 1). Ao abrir o mundo, elas viram adiadas comuns, para o save seguinte não
  apagá-las.

## 7. Rede

- Os bytes de água vão no payload do chunk (`LoadChunkReceive`), aplicados mesmo num chunk que o cliente
  já pintou.
- A cada passo, um RPC por chunk com as mudanças (`WaterChangesReceive`, 3 bytes por célula) só para os
  peers que têm o chunk. Chunks só de dados não vão para ninguém.

## 8. Exibição, luz e minimapa

- **`WaterLayer`:** `TileMapLayer` "Water" entre `Entities` e `Base`, com `z_index` 1 como o terreno e o
  jogador (ordem: jogador → água → terreno). Redesenha só as células que mudaram e a de baixo (a linha de
  superfície).
- **Luz:** cada `LightChunkOverlay` guarda a máscara do terreno e compõe a água por cima a partir dos
  níveis (`PatchWater`), sem reler as camadas. A água recebe a luz do mundo e não a bloqueia.
- **Minimapa:** pinta a água em azul onde o terreno não bloqueia luz.

## 9. Nado — `Player`

- `WaterImmersion()`: fração das células do corpo com água de altura ≥ 4.
- Com 0,5 ou mais:
  - gravidade × 0,25 e queda limitada;
  - velocidade × 0,6;
  - segurar o pulo sobe.

  Na superfície, o pulo normal tira o jogador da água.
