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
