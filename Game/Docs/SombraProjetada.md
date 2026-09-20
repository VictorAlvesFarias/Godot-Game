# Sombra projetada

`AnalyticShadowGeometry` funde runs verticais iguais em retângulos e os indexa em
bins de 16 tiles. A textura RGBA32F tem largura 1024: cabeçalhos apontam para os
retângulos candidatos de cada bin. A construção é dividida em fatias de 3 ms.

`projected_shadow.gdshaderinc` avalia os intervalos angulares desses retângulos
vistos da posição original do pixel. A fração ocluída é a **união**, não a soma:
bloqueadores sobrepostos não contam duas vezes o mesmo trecho do disco solar.
A abertura é `radians(8) * (1 - penumbra)²`. No limite paralelo, a cobertura da
borda usa o footprint do pixel. As duas curvas alteram a intensidade da transição.

Esse cálculo não classifica receptores nem exclui auto-sombra. A máscara de
recepção pertence ao `TerrainLightOverlay`: somente células com wall e sem sólido
primário recebem a projeção na composição final. O material de wall é unshaded
e não reaplica a sombra; a máscara de pixels é construída separadamente.

`DebugShadow` usa `projected_shadow_debug.gdshader`, desenhando a projeção bruta
em escala de cinza: branco livre, preto ocluído e cinza penumbra. Ignora a força
da sombra e a textura da cena. `projected_shadow.gdshader` é o passe isolado usado
na regressão para validar força, endpoints e penumbra.

As regras antigas de corpo conectado, escada e saída do sólido foram removidas.
Não devem ser reinseridas na união angular para controlar quais camadas recebem luz.

A projeção cobre sólidos, simetria, penumbra crescente, força zero, modo debug e
bloqueadores fora da câmera.
