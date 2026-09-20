# Camera

## Perseguicao suavizada

A camera segue o player com `position_smoothing` do `Camera2D` (`CameraController.SmoothingSpeed`,
padrao **12**), em vez de copiar `GlobalPosition` frame a frame.

O motivo esta no corpo do player, nao na camera: o movimento nao tem aceleracao (0 a 300 px/s em um
frame), o pulo comeca em -750 px/s e o degrau sobe 64 px a 450 px/s. Cada uma dessas quebras de
velocidade chegava inteira na tela.

Medido a 60 fps sobre a trajetoria andar/parar/degrau/pulo do proprio player. "Solavanco" e a
segunda diferenca da posicao vista da camera por frame; "atraso" e a distancia ate o player.

| velocidade | solavanco max | solavanco medio | atraso max | atraso parado |
|-----------:|--------------:|----------------:|-----------:|--------------:|
| colada     | 12,08 px      | 0,263 px        | 0 px       | 0 px          |
| 8          | 1,61 px       | 0,210 px        | 79,7 px    | 0 px          |
| **12**     | **2,42 px**   | **0,232 px**    | **57,8 px**| **0 px**      |
| 16         | 3,22 px       | 0,244 px        | 45,1 px    | 0 px          |
| 20         | 4,03 px       | 0,250 px        | 36,9 px    | 0 px          |
| 25         | 5,03 px       | 0,255 px        | 30,1 px    | 0 px          |

A troca e linear: solavanco e atraso andam em direcoes opostas. Em 12 o solavanco cai 5x e o atraso
maximo (58 px de mundo, no auge do pulo) e 27% da meia-altura visivel no zoom 2,5. Parado, a camera
volta a ficar exatamente no player.

## Corte no teleporte

Deslocamento do alvo maior que `SmoothingCutDistance` (512 px) nao e andar - e troca de dimensao,
load ou respawn. Nesse caso o `_PhysicsProcess` chama `ResetSmoothing()`, senao a camera varreria o
mundo inteiro ate alcancar. Teleporte de 4472 px medido: a camera chega ao destino no frame
seguinte.

## Regulagem em jogo

`cam_smooth off/<velocidade>` no console troca a velocidade a quente; sem argumento, imprime a atual.
