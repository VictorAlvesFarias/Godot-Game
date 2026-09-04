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

// A MESMA textura, lida com filtro linear. Serve so para o canal de ambiente.
//
// O ambiente e o nivel difuso, que entra pelo topo de cada coluna e nao tem direcao. Lido celula a
// celula, um buraco na copa vira uma coluna de ambiente alto descendo reta, e a sombra afrouxa
// numa listra vertical de borda seca. Com filtro linear o valor se mistura com os vizinhos, e a
// listra vira degrade - que e o que um buraco na copa faz de verdade.
uniform sampler2D dados_suaves : filter_linear, repeat_disable;

// Tamanho da janela em celulas. A textura tem exatamente este tamanho.
uniform vec2 grade;

// Direcao em que a luz VIAJA, normalizada. O raio anda contra ela, rumo ao sol.
uniform vec2 direcao;

// Quanto de luz uma celula de materia deixa passar (1 - dureza).
uniform float passa;

// A cor que a celula assume onde a sombra e total.
uniform float piso_terreno;
uniform float piso_ar;

// Quantas celulas a sombra penetra no terreno antes de desvanecer.
uniform float profundidade;

uniform bool terreno_ligado;
uniform bool ar_ligado;

// Abertura do cone de raios, em radianos. 0 e um raio so, com borda dura.
uniform float suavidade;

// Quanto a luz do ambiente enfraquece a sombra. Lugar aberto tem luz vindo de todo lado, que
// preenche a sombra; lugar fechado nao tem, e a sombra fica cheia. 0 ignora o ambiente.
uniform float influencia_ambiente;

// Abaixo desta transmissao o raio para: o que sobra nao muda mais o pixel.
uniform float corte;

// Teto de passos do DDA. Shader precisa de laco limitado; a janela inteira cabe nisto.
const int MAX_PASSOS = 512;

// Quantos raios formam o cone. Impar de proposito: o do meio e a direcao exata do sol.
const int AMOSTRAS = 5;

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

    for (int i = 0; i < MAX_PASSOS; i++) {
        if (proximo.x < proximo.y) {
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

        t *= passa;

        if (t <= corte) {
            return 0.0;
        }
    }

    return t;
}

void fragment() {
    vec2 celula = UV * grade;

    ivec2 c = ivec2(floor(celula));

    vec4 dados = texelFetch(dados_mapa, c, 0);

    bool materia = dados.r > 0.5;

    // A difusa ja vem pronta da CPU; o ar nunca e escurecido por ela.
    float brilho = materia ? dados.g : 1.0;

    bool ligada = materia ? terreno_ligado : ar_ligado;

    if (ligada) {
        float profundidade_percorrida;

        float t;

        if (suavidade <= 0.0) {
            t = transmissao(celula, -direcao, materia, profundidade_percorrida);
        } else {
            // O cone: varios raios abertos em torno da direcao do sol, com a media. Perto do
            // oclusor eles concordam e a borda fica nitida; longe divergem e ela abre sozinha.
            // A penumbra sai da geometria, sem precisar medir distancia ate o oclusor.
            float soma = 0.0;
            float soma_profundidade = 0.0;

            for (int k = 0; k < AMOSTRAS; k++) {
                float fatia = float(k) / float(AMOSTRAS - 1) * 2.0 - 1.0;

                float prof_amostra;

                soma += transmissao(celula, rodar(-direcao, fatia * suavidade), materia, prof_amostra);
                soma_profundidade += prof_amostra;
            }

            t = soma / float(AMOSTRAS);
            profundidade_percorrida = soma_profundidade / float(AMOSTRAS);
        }

        float piso = materia ? piso_terreno : piso_ar;

        // A sombra e a razao entre o sol direto e a luz que chega de todo lado. Onde ha muito
        // ambiente - ceu aberto -, ele preenche a sombra e ela fica fraca; onde ha pouco - dentro
        // de uma caverna, sob a copa -, sobra pouca luz para preencher e ela fica cheia.
        //
        // O ambiente vem no canal B, e o AR tambem o carrega. Sem isso o shader nao teria como
        // saber se um ponto de ar esta em lugar aberto ou fechado, e a sombra no ar teria a mesma
        // forca nos dois. Lido com filtro linear: ver o comentario do uniforme.
        float ambiente = texture(dados_suaves, UV).b;

        piso = mix(piso, 1.0, ambiente * influencia_ambiente);

        float sombra = mix(piso, 1.0, t);

        // Dentro do terreno a sombra desvanece com a profundidade, ate sumir. Sem isso ela teria
        // uma celula de espessura: a coluna sombreada e a iluminada valem o mesmo logo abaixo.
        if (materia && profundidade > 0.0) {
            sombra = mix(sombra, 1.0, clamp(profundidade_percorrida / profundidade, 0.0, 1.0));
        }

        brilho *= sombra;
    }

    COLOR = vec4(vec3(brilho), 1.0);
}
