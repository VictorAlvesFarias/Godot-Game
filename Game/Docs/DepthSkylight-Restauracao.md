# Entrada de luz pelo fundo: implementação e restauração

Este guia descreve o caminho ativo. Ele substitui as instruções antigas sobre
`LightingField`, `LightMapComputer`, `layered_light.gdshader` e semeadura solar no
mapa de emissores. Esses caminhos não participam mais da apresentação.

## Reimplementar o comportamento atual

1. Manter a ocupação de Base/Compose e walls no `LogicalLightWorld`, incluindo
   edições, replay de saves e sincronização da dimensão.
2. Em `SkyAccessField.Build`, gerar uma textura RGBA8: R indica sólido primário,
   G indica wall, B armazena acesso à luz de céu e A vale 255. Semear B=1 onde
   não há sólido nem wall. Propagar primeiro pelo ar, sem atravessar sólidos
   ou diagonais fechadas; depois propagar para dentro dos sólidos. Não usar
   sólidos iluminados como novas entradas de luz para compartimentos de ar.
3. Preservar a perda no ar `AIR_LIGHT_LOSS * distância` e o perfil do terreno
   `luz * pow(SOLID_FALLOFF, distância * 3 / max(0.25, depth))`. Com depth=3,
   os primeiros tiles recebem 0.5, 0.25, 0.125, antes da quantização.
4. Em `WindowBeamCache`, reconstruir o campo quando mudam mundo, região, walls,
   direção, penumbra, curvas, profundidade ou revisão das fontes. Renderizar os
   campos de feixe em SubViewports com quatro texels por tile e atualização Once.
5. Em `window_beam_cache.gdshader`, transmitir luz solar da primeira abertura
   atingida antes de um sólido. Transmitir fontes locais apenas quando sua célula
   está sem wall e sem sólido; verificar o caminho completo até o emissor.
   Uma fonte interna permanece no registro, mas não gera esse feixe.
6. Em `window_beam_sampling.gdshaderinc`, transformar posição de mundo para o
   campo e impedir que interpolação de sólidos claros injete luz no ar vedado.
7. Em `TerrainLightOverlay`, colorir o campo de céu com SunColor. Nas walls,
   preencher apenas a sombra com a luz disponível e AmbientLightInfluence.
   Combinar por máximo com o feixe solar **depois** da projeção, pois o feixe já
   verificou obstáculos. Somar a contribuição local e só então aplicar o piso visual.
8. Ligar os campos aos materiais de preview e runtime no `LightMap2D`, mantendo
   a chave de cache dos bindings. Atualizar o Background com a cor solar e
   restaurar a modulação original ao desativar/desanexar o mapa.

O mapa RGB de emissão local deve continuar separado do céu. Seus consumidores
ativos chamam `LightSourceScanner` com `includeSkylight: false`. O modo opcional
de semeadura do scanner permanece para comparação CPU/GPU nos diagnósticos.

## Reverter uma parte sem desfazer as demais

- **Somente feixes solares:** remover a contribuição `max(luz, SunColor * beam)`
  no overlay e a geração solar de `WindowBeamCache`. Preservar acesso ao céu e
  feixes locais. Isso elimina intencionalmente a entrada direcional com influência
  ambiente zero; a regressão correspondente precisará refletir essa decisão.
- **Somente feixes locais:** retirar o segundo viewport e seus bindings e usar
  apenas emissão propagada na contribuição local. Manter `SceneLightSources` e
  a coleta dos emissores para a fogueira continuar iluminando.
- **Entrada de céu pelo fundo:** mudar a regra de sementes de `SkyAccessField`,
  não a opacidade da sombra. Uma semeadura somente por coluna voltará a escurecer
  espaços abertos atrás de tetos; essa é uma mudança visual deliberada.

Não substituir a absorção exponencial por smoothstep, não multiplicar o feixe
solar pela oclusão uma segunda vez e não trocar a composição explícita por
blend_mul: essas mudanças alteram o resultado aprovado.

Antes de reverter, salvar o estado funcional em um commit próprio. Para recuperar
essa versão integralmente, usar o commit correspondente, incluindo sistemas,
shaders, cenas e testes. Nenhum hash fixo é indicado enquanto o merge está pendente.
Não é necessário modificar os valores ajustados em `Assets/Data/LightMap.tres`.

## Verificar

Abrir a Upsidedown no editor e conferir a entrada de luz pelo fundo: teto fechado
escurece, janela ilumina a sala, fechar a janela volta ao escuro.
