# Sombra projetada

A cena agora usa apenas `projected_shadow.gdshader`. O resultado é
`1 - ShadowStrength * oclusão`, aplicado igualmente a todos os pixels.
Não há máscara de receptor, DDA de saída do terreno, luz ambiente, emissão,
profundidade, volumetria ou pesos por camada.

Oclusão é a união analítica dos intervalos dos bloqueadores diante do sol,
avaliada na posição original do pixel. A geometria vem do mundo lógico e
continua existindo fora da câmera. Os blocos são quadrados lógicos completos;
o alfa decorativo das texturas não define o contorno físico.

Configurações: SunAngleDegrees, SunPenumbra (0 aberta, 1 paralela), duas curvas
de intensidade e ShadowStrength. Alterações no editor atualizam a projeção.
O índice geométrico é reconstruído em fatias de 3 ms quando necessário.

Os arquivos substituídos e testes da composição antiga foram retirados do
projeto Godot e preservados em `Archive/LightingBeforeShadowReset`, na raiz.
O mundo lógico permanece para fornecer geometria e compatibilidade com edição
/ streaming; seu campo de iluminação não é processado pela nova apresentação.

Ative `DebugShadow` no recurso Settings para substituir a cena por uma vista
em escala de cinza: branco = livre, preto = oclusão total, cinza = penumbra.
Usa o mesmo cálculo e curvas, ignorando ShadowStrength e a cor dos tiles.
Desativar restaura a composição normal sem reconstruir a geometria.


## Sem regras (estado atual)

A sombra projeta **tudo sobre tudo**. Não há exclusão do trecho sólido inicial, regra de escada,
corpo próprio nem máscara de receptor: o pixel não é classificado, sólido e ar recebem exatamente a
mesma conta na posição original do pixel.

Consequências medidas, e são esperadas:

| Cena | Resultado |
| --- | --- |
| Parede alta ao lado de chão plano | ar sombreado em 8/10 colunas, sólido em 10/10 — o sólido deixou de ficar de fora |
| Escada descendente de 45° com sol quase vertical | rampa 100% auto-sombreada |

O bloco de regras ficou **comentado** em `projected_shadow.gdshaderinc`, e o estágio de regressão
que as cobrava ficou comentado em `ProjectedShadowRegression.cs`. Os dois voltam quando o
comportamento voltar como **máscara de camada** — só a camada de wall recebendo a projeção —, e não
como regra dentro do cálculo da sombra. Aí o que se testa é quem **recebe**, não quem projeta.

Próximos passos combinados: máscara de corte por camada; mapa de luz usando as duas camadas
(wall e primária); sistema de escuridão inalterado.

## Exclusão do trecho sólido inicial

O cálculo não usa corpos conectados. Em cada direção do sol, ignora apenas o
intervalo sólido contínuo que começa no receptor. Depois de atravessar ar, um
novo intervalo sólido bloqueia luz normalmente, mesmo ligado ao receptor por
outro caminho. Materiais distintos em contato não introduzem uma abertura.

A penumbra integra os intervalos angulares delimitados pelos cantos dos
retângulos. Dentro de cada intervalo, a ordem de entrada/saída é constante.
Não desloca o receptor, não adiciona atenuação de profundidade e não altera o ar.
O caso paralelo mantém cobertura de borda em espaço de tela.

A integração dos receptores sólidos custa mais que a união simples usada no ar;
não possui contagem fixa de amostras, mas depende da quantidade de retângulos e
intervalos no bin. A conectividade global e seu cache foram removidos.

Regressão: bloco contínuo, abrir/fechar lacuna, conexão externa que não deve
cancelar sombra, penumbra aberta e paralela, ar e debug preservados.
