// Sombra projetada do mapa de luz, por pixel.
//
// A CPU nao calcula mais sombra nenhuma: ela entrega uma textura de DADOS, um texel por celula,
// e este shader tracaa o raio por fragmento. O ganho e duplo - a borda passa a ter a resolucao da
// tela em vez da celula, e o custo sai da CPU, onde ele era proporcional a area vezes o numero de
// sub-celulas.
//
// A textura traz, por celula:
//   R = 1 se ha materia ali, 0 se e ar
//   G = o brilho que a luz difusa deu aquela celula (so a materia usa)
//   B = quanta luz do ceu chega ali, de 0 a 1 - inclusive no ar
//
// A difusa continua na CPU de proposito: ela e uma inundacao em fila, que nao cabe bem na GPU, e
// nao precisa de detalhe menor que um bloco - ela mede distancia ate o ceu.

shader_type canvas_item;
render_mode blend_mul, unshaded;

// A textura de dados. Uniforme proprio, e nao o built-in do sprite, porque aquele so existe
// dentro de fragment() - nas funcoes auxiliares ele nao e reconhecido. filter_nearest garante o
// valor cru da celula, que e o que o texelFetch precisa.
uniform sampler2D dados_mapa : filter_nearest, repeat_disable;

// Tamanho da janela em celulas. A textura tem exatamente este tamanho.
uniform vec2 grade;

// Direcao em que a luz VIAJA, normalizada. O raio anda contra ela, rumo ao sol.
uniform vec2 direcao;

// A cor que a celula assume onde a sombra e total.
uniform float piso_terreno;
uniform float piso_ar;

// Quantas celulas a sombra penetra no terreno antes de desvanecer.
uniform float profundidade;

uniform bool terreno_ligado;
uniform bool ar_ligado;

// Abertura do cone de raios, em radianos: a LARGURA da faixa de penumbra. 0 e um raio so, com
// borda dura de um pixel.
uniform float penumbra;

// Quanto a luz do ambiente enfraquece a sombra. Lugar aberto tem luz vindo de todo lado, que
// preenche a sombra; lugar fechado nao tem, e a sombra fica cheia. 0 ignora o ambiente.
uniform float influencia_ambiente;

const int PASSOS_DE_BISSECCAO = 10;

vec2 rodar(vec2 v, float a) {
    float s = sin(a);
    float c = cos(a);

    return vec2(v.x * c - v.y * s, v.x * s + v.y * c);
}

bool dentro_da_janela(ivec2 c) {
    return c.x >= 0 && c.y >= 0 && c.x < int(grade.x) && c.y < int(grade.y);
}

bool tem_materia(ivec2 c) {
    return texelFetch(dados_mapa, c, 0).r > 0.5;
}

// Quanto do sol chega a este ponto, e - quando o ponto esta dentro de materia - quantas celulas
// ele esta abaixo da superficie por onde o raio entrou.
//
// O raio anda por DDA, saltando de fronteira em fronteira de celula: cada celula cruzada e
// visitada exatamente uma vez. Com passo fixo, um passo curto conta a mesma celula duas vezes e um
// passo longo pula bloco fino na diagonal.
//
// Nao ha alcance maximo. Sombra nao tem comprimento arbitrario: ela vai ate encontrar o que a
// projeta ou ate o ceu. O limite e a propria janela.
float transmissao(vec2 p, vec2 rumo, bool comeca_na_materia, out float profundidade_percorrida) {
    vec2 d = rumo;

    ivec2 c = ivec2(floor(p));

    ivec2 avanco = ivec2(d.x > 0.0 ? 1 : -1, d.y > 0.0 ? 1 : -1);

    vec2 absd = abs(d);
    vec2 delta = vec2(absd.x < 1e-6 ? 1e9 : 1.0 / absd.x,
                      absd.y < 1e-6 ? 1e9 : 1.0 / absd.y);

    vec2 proximo;
    proximo.x = absd.x < 1e-6 ? 1e9 : (d.x > 0.0 ? float(c.x) + 1.0 - p.x : p.x - float(c.x)) / absd.x;
    proximo.y = absd.y < 1e-6 ? 1e9 : (d.y > 0.0 ? float(c.y) + 1.0 - p.y : p.y - float(c.y)) / absd.y;

    float t = 1.0;

    // Enquanto o raio ainda esta dentro da materia em que nasceu, ele nao absorve: essa parte e a
    // profundidade abaixo da superficie, e a sombra e avaliada la em cima, na entrada. Sem isto o
    // raio morre no proprio bloco e toda celula enterrada fica igual, sem sombra para ver.
    bool subindo = comeca_na_materia;

    profundidade_percorrida = 0.0;

    // O teto de passos e a diagonal Manhattan da janela: nenhum raio pode dar mais passos que
    // isso sem sair dela, e sair dela ja retorna. Era um 512 fixo, folgado em 4x.
    int limite = int(grade.x + grade.y);

    for (int i = 0; i < limite; i++) {
        // Quina: quando as duas fronteiras caem no mesmo ponto, o raio passa exatamente pelo
        // canto entre quatro celulas. Avancando um eixo so, ele ENTRA numa das duas diagonais que
        // nao chegou a cruzar - e se aquela for solida, e bloqueado sem ter tocado em nada. Na tela
        // isso vira um risco fino e reto, na direcao exata que passa pela quina, saindo do canto de
        // um tronco ou de uma copa.
        //
        // Cruzando a quina de fato, ele so visita as duas celulas que a diagonal realmente atinge.
        if (abs(proximo.x - proximo.y) < 1e-5) {
            proximo += delta;
            c += avanco;
        } else if (proximo.x < proximo.y) {
            proximo.x += delta.x;
            c.x += avanco.x;
        } else {
            proximo.y += delta.y;
            c.y += avanco.y;
        }

        // Saiu da janela: dali para cima e ceu aberto, nada mais tapa.
        if (!dentro_da_janela(c)) {
            return t;
        }

        bool materia = tem_materia(c);

        if (subindo) {
            if (materia) {
                profundidade_percorrida += 1.0;
                continue;
            }

            subindo = false;
        }

        if (!materia) {
            continue;
        }

        // Materia e OPACA, sempre. Oclusao e caracteristica do TILE, e nao do mapa inteiro: um
        // numero global so faria sentido se todo tile do mundo fosse igualmente translucido, que e
        // o mesmo que dizer que ele nao existe. Se um dia houver folhagem que deixa passar luz,
        // isso e dado do tileset e entra pela grade de custo, nao por uma propriedade do recurso.
        return 0.0;
    }

    return t;
}

// Quanto do cone chega ao sol, de 0 a 1.
//
// Materia e opaca, entao cada raio responde SIM ou NAO e a fronteira entre os dois e um angulo
// exato. Contar raios livres daria a cobertura em AMOSTRAS+1 degraus, e esses degraus aparecem na
// borda como faixas - a escadinha e da amostragem, nao da conta.
//
// Entao ele mede a EXTENSAO ANGULAR livre, trecho por trecho: cinco raios uniformes dividem o cone
// em quatro trechos, e onde as pontas de um trecho discordam a bisseccao acha a fronteira dentro
// dele. Cada passo dobra a precisao, entao dez dao ~1024 niveis por fronteira.
//
// Cada trecho e resolvido por conta propria, e nao so o primeiro. E o que trata FRESTA - oclusor
// fino no meio do cone, que poe duas fronteiras em trechos diferentes. Com uma bisseccao so, a
// fresta caia numa fracao contada de cinco niveis e virava cunha em escada dentro da sombra.
//
// Custo: 5 raios sempre, mais 10 por fronteira. Pixel em plena luz ou em plena sombra nao tem
// fronteira nenhuma e sai com 5. Borda comum tem uma. Fresta tem duas.
float cobertura(vec2 celula, vec2 rumo) {
    float prof;

    if (penumbra <= 0.0) {
        return transmissao(celula, rumo, false, prof);
    }

    // Quantas amostras o bracket precisa: um vao de UMA celula a distancia d ocupa ~1/d radianos,
    // entao o passo angular tem que ser menor que isso, ou a fresta cabe inteira dentro de um
    // trecho, as duas pontas dele concordam, nenhuma virada e detectada e o vao SOME - vira sombra
    // onde devia ter luz, num risco fino e paralelo a sombra.
    //
    // A distancia maxima em que um oclusor ainda pode existir e a DIAGONAL da janela: alem dela o
    // raio ja saiu. Entao a contagem sai da propria janela e da abertura do cone, sem numero
    // escolhido a mao - cone estreito, o caso comum, resolve com 3 e sai barato; cone aberto paga
    // o que precisa. Era um alcance fixo de 40 celulas, MENOR que a diagonal, entao vao de uma
    // celula entre 40 e a borda ainda sumia.
    int amostras = max(3, int(2.0 * penumbra * length(grade)) + 1);

    float passo = 2.0 * penumbra / float(amostras - 1);

    float angulo_anterior = -penumbra;

    bool livre_anterior = transmissao(celula, rodar(rumo, angulo_anterior), false, prof) > 0.5;

    float livre_total = 0.0;

    for (int k = 1; k < amostras; k++) {
        float angulo = -penumbra + float(k) * passo;

        bool livre = transmissao(celula, rodar(rumo, angulo), false, prof) > 0.5;

        if (livre == livre_anterior) {
            if (livre) {
                livre_total += passo;
            }
        } else {
            float baixo = angulo_anterior;
            float alto = angulo;

            for (int i = 0; i < PASSOS_DE_BISSECCAO; i++) {
                float meio = 0.5 * (baixo + alto);

                if ((transmissao(celula, rodar(rumo, meio), false, prof) > 0.5) == livre_anterior) {
                    baixo = meio;
                } else {
                    alto = meio;
                }
            }

            float fronteira = 0.5 * (baixo + alto);

            livre_total += livre_anterior ? fronteira - angulo_anterior : angulo - fronteira;
        }

        angulo_anterior = angulo;
        livre_anterior = livre;
    }

    return livre_total / (2.0 * penumbra);
}

void fragment() {
    vec2 celula = UV * grade;

    ivec2 c = ivec2(floor(celula));

    vec4 dados = texelFetch(dados_mapa, c, 0);

    bool materia = dados.r > 0.5;

    // ISOLADO: so a sombra projetada NO AR.
    //
    // Tudo o mais esta desligado de proposito, para trabalhar uma peca por vez:
    //   - a difusa (dados.g) nao entra: materia sai intacta, brilho 1
    //   - a sombra no terreno nao entra
    //   - o ambiente (dados.b) nao entra: o piso e constante
    //
    // O backup do shader inteiro esta em .backup/antes-do-isolamento/.
    float brilho = 1.0;

    if (ar_ligado && !materia) {
        float t = cobertura(celula, -direcao);

        // O degrade da penumbra e a FRACAO DO DISCO DO SOL que ainda esta visivel dali:
        //
        //     I = I0 * A_visivel / A_sol
        //
        // Sol inteiro visivel da luz cheia; metade tapado da metade da luz; nada visivel da sombra
        // cheia. Nao e uma rampa escolhida - e a area que sobra.
        //
        // Como o oclusor e sempre uma aresta de tile, o corte no disco e uma RETA, e a area do
        // pedaco que sobra tem forma fechada - o segmento circular:
        //
        //     visivel = ( acos(x) - x * sqrt(1 - x*x) ) / PI
        //
        // com x indo de -1 (disco todo livre) a +1 (todo tapado). A formula geral de intersecao de
        // dois circulos so seria necessaria se o oclusor tambem fosse redondo.
        //
        // t e a fracao do cone que os raios acharam livre, e a borda corta o cone linearmente,
        // entao x = 1 - 2t. O resultado e uma curva em S: a borda escurece devagar no comeco,
        // rapido no meio e devagar no fim, que e o que o disco faz de verdade.
        float x = clamp(1.0 - 2.0 * t, -1.0, 1.0);

        float visivel = (acos(x) - x * sqrt(max(0.0, 1.0 - x * x))) / PI;

        brilho = mix(piso_ar, 1.0, visivel);
    }

    COLOR = vec4(vec3(brilho), 1.0);
}
