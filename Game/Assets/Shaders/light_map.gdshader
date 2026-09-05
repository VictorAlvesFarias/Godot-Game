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

// Quanto a luz do ambiente enfraquece a sombra, de 0 a 1. 0 ignora o ambiente e a sombra fica
// cheia; 1 e influencia total - onde ha qualquer luz em contato a sombra fica transparente.
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
// As duas celulas ortogonais de uma quina estao ambas ocupadas?
bool bloqueado_de_lado(ivec2 c, ivec2 avanco) {
    ivec2 a = ivec2(c.x + avanco.x, c.y);
    ivec2 b = ivec2(c.x, c.y + avanco.y);

    bool fecha_a = dentro_da_janela(a) && tem_materia(a);
    bool fecha_b = dentro_da_janela(b) && tem_materia(b);

    return fecha_a && fecha_b;
}

float transmissao(vec2 p, vec2 rumo, bool comeca_na_materia, out float profundidade_percorrida, out vec2 ultimo_ar) {
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

    // Onde o raio estava antes de bater. E dali que se pergunta se ha luz encostando.
    ultimo_ar = p;

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
        // Onde o raio esta agora, em celulas. Lido ANTES do avanco, que sobrescreve proximo.
        float ate = min(proximo.x, proximo.y);

        if (abs(proximo.x - proximo.y) < 1e-5) {
            // Passar pela quina so vale se houver FRESTA. Um raio um fio ao lado dela cruza uma
            // das duas celulas ortogonais, entao ele passa se aquela for ar; o raio da quina tem
            // que responder igual, ou ele fica diferente dos vizinhos e a diferenca sai como risco
            // reto na direcao da quina. Antes ele passava sempre, inclusive entre dois blocos
            // encostados pelo vertice - luz vazando por onde nao ha vao.
            if (bloqueado_de_lado(c, avanco)) {
                ultimo_ar = p + rumo * max(ate - 1e-3, 0.0);

                return 0.0;
            }

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

        // Materia e OPACA, sempre, e todo tile e igual perante ela.
        // O ponto EXATO onde o raio encostou na materia, e nao o centro da celula anterior.
        //
        // Centro de celula e valor discreto: andando um pixel, o ponto de contato saltava uma
        // celula inteira, a resposta do teste de sol virava de uma vez e saia um corte reto e duro
        // no meio da sombra. Com o ponto exato ele anda junto com o pixel.
        ultimo_ar = p + rumo * max(ate - 1e-3, 0.0);

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
// A inundacao de ambiente do texel, suavizada.
//
// dados.b e UM valor por celula, lido nearest, entao ele entra quadriculado - degrau de tile
// dentro da sombra. Aqui ele vira bilinear entre os quatro texels em volta.
//
// So que a media conta apenas os vizinhos de AR, e renormaliza pelo peso que sobrou. Media cheia
// puxaria o valor das celulas de materia, que nao tem inundacao nenhuma, e cada bloco ganharia uma
// borda de meia celula em volta - trocar quadriculado por auréola nao e conserto.
float ambiente_suave(vec2 celula) {
    vec2 p = celula - 0.5;

    // Peso por smoothstep, e nao linear. Bilinear puro e continuo mas a DERIVADA dele salta na
    // fronteira de cada celula - da vinco, e o ganho do ambiente amplifica o vinco ate virar
    // escadinha visivel dentro da sombra. A cubica chega e sai com derivada zero, entao a emenda
    // entre celulas some.
    vec2 f = fract(p);

    f = f * f * (3.0 - 2.0 * f);

    ivec2 canto = ivec2(floor(p));

    float soma = 0.0;
    float peso = 0.0;

    for (int j = 0; j <= 1; j++) {
        for (int i = 0; i <= 1; i++) {
            ivec2 c = canto + ivec2(i, j);

            if (!dentro_da_janela(c)) {
                continue;
            }

            vec4 d = texelFetch(dados_mapa, c, 0);

            if (d.r > 0.5) {
                continue;
            }

            float w = (i == 0 ? 1.0 - f.x : f.x) * (j == 0 ? 1.0 - f.y : f.y);

            soma += d.b * w;
            peso += w;
        }
    }

    return peso > 0.0 ? soma / peso : 0.0;
}

// ---------------------------------------------------------------------------------------------
// Quanto de ceu este ponto enxerga, EXATO, sem amostrar direcao nenhuma.
//
// Leque de raios responde "bateu?" em N direcoes escolhidas, e por isso um vao fino que caiba entre
// duas delas some - e some/aparece conforme o pixel anda, o que sai na tela como risco reto saindo
// da quina do oclusor. Medido: com 40 amostras o risco aparece, com 400 ele some, mas o frame vai
// de 24 para 72 ms. Amostrar mais nunca ia resolver, so empurrar o problema pra mais longe.
//
// Aqui nao ha direcao escolhida. Cada celula solida subtende um intervalo angular EXATO vista deste
// pixel, e o algoritmo caminha pelas celulas subtraindo esses intervalos de uma lista de faixas de
// ceu ainda abertas. O que sobrevive ate o fim e ceu de verdade. Nao existe "entre duas amostras",
// entao nao existe vao pra escapar.
//
// O custo tambem melhora de ordem: O(celulas visitadas) em vez de O(raios x celulas), e celula
// tapada por outra nunca chega a ser visitada, porque a faixa dela ja fechou.
//
// O hemisferio e dividido em quatro octantes, cada um com inclinacao em [0,1]. Dentro do octante a
// celula (r, c) ocupa a faixa [(c - fc)/dist , (c + 1 - fc)/dist], com dist = r - 0.5 + fr. fr e fc
// sao a posicao do pixel DENTRO da celula dele - e o que faz o resultado andar continuo com o
// pixel, em vez de pular de celula em celula.
// ---------------------------------------------------------------------------------------------

const int MAX_FAIXAS = 8;

// Ate onde a geometria ainda tapa ceu. Alem disso conta como aberto: com o peso cosseno, oclusor
// distante encosta no horizonte e ja quase nao pesa.
const int RAIO_DO_CEU = 16;

// O peso cosseno, ja integrado. Num octante "de pe" o angulo e atan(s), e sin(atan(s)) = s/hip.
// Num octante "deitado" o angulo e 90 graus menos aquele, e o seno vira o cosseno: 1/hip.
float peso_do_slope(float s, bool deitado) {
    float inv = inversesqrt(1.0 + s * s);

    return deitado ? inv : s * inv;
}

float octante_de_ceu(vec2 p, ivec2 eixo_linha, ivec2 eixo_coluna, float fr, float fc, bool deitado) {
    ivec2 base = ivec2(floor(p));

    float ini[MAX_FAIXAS];
    float fim[MAX_FAIXAS];
    float novo_ini[MAX_FAIXAS];
    float novo_fim[MAX_FAIXAS];

    int n = 1;

    ini[0] = 0.0;
    fim[0] = 1.0;

    // Comeca em ZERO, e nao em um. A linha 0 e a travessia dentro da PROPRIA coluna de origem.
    //
    // Marchar direto para a coluna vizinha so vale se o raio sair da celula de origem pela face
    // daquele eixo - o que e verdade partindo do CENTRO da celula, e falso partindo do pixel. Pixel
    // colado no topo da celula, num octante deitado, sobe e sai pela face de CIMA antes de alcancar
    // a coluna vizinha; a celula de cima nunca era testada, e a direcao saia contada como ceu mesmo
    // com parede ali. Era o residuo dentro da caixa: borda de um pixel, so nos deitados, um pico
    // por celula.
    //
    // A distancia da linha 0 e `fr`, que e exatamente quanto falta ate a fronteira da coluna.
    for (int r = 0; r <= RAIO_DO_CEU; r++) {
        float dist = r == 0 ? max(fr, 1e-3) : float(r) - 0.5 + fr;

        int m = 0;

        for (int i = 0; i < MAX_FAIXAS; i++) {
            if (i >= n) {
                break;
            }

            float a = ini[i];
            float b = fim[i];

            int c_ini = max(int(floor(a * dist + fc - 1.0)), 0);
            int c_fim = int(ceil(b * dist + fc));

            float corrente = a;
            bool correndo = true;

            for (int c = c_ini; c <= c_fim; c++) {
                float s_lo = max(a, (float(c) - fc) / dist);
                float s_hi = min(b, (float(c) + 1.0 - fc) / dist);

                if (s_hi <= s_lo) {
                    continue;
                }

                ivec2 cel = base + eixo_linha * r + eixo_coluna * c;

                // Na linha 0 a coluna 0 e a celula do proprio pixel, que e ar por definicao.
                bool solido = !(r == 0 && c == 0) && dentro_da_janela(cel) && tem_materia(cel);

                if (solido) {
                    if (correndo) {
                        if (s_lo > corrente && m < MAX_FAIXAS) {
                            novo_ini[m] = corrente;
                            novo_fim[m] = s_lo;
                            m++;
                        }

                        correndo = false;
                    }
                } else if (!correndo) {
                    corrente = s_lo;
                    correndo = true;
                }
            }

            if (correndo && b > corrente && m < MAX_FAIXAS) {
                novo_ini[m] = corrente;
                novo_fim[m] = b;
                m++;
            }
        }

        n = m;

        if (n == 0) {
            break;
        }

        for (int i = 0; i < MAX_FAIXAS; i++) {
            if (i >= n) {
                break;
            }

            ini[i] = novo_ini[i];
            fim[i] = novo_fim[i];
        }
    }

    float soma = 0.0;

    for (int i = 0; i < MAX_FAIXAS; i++) {
        if (i >= n) {
            break;
        }

        soma += abs(peso_do_slope(fim[i], deitado) - peso_do_slope(ini[i], deitado));
    }

    return soma;
}

// O hemisferio de cima inteiro, de 0 a 1. Ceu aberto da 1, lugar fechado da 0.
float ceu_visivel(vec2 p) {
    vec2 f = fract(p);

    float total = octante_de_ceu(p, ivec2(0, -1), ivec2(1, 0), f.y, f.x, false)
                + octante_de_ceu(p, ivec2(0, -1), ivec2(-1, 0), f.y, 1.0 - f.x, false)
                + octante_de_ceu(p, ivec2(1, 0), ivec2(0, -1), 1.0 - f.x, 1.0 - f.y, true)
                + octante_de_ceu(p, ivec2(-1, 0), ivec2(0, -1), f.x, 1.0 - f.y, true);

    // O hemisferio inteiro livre soma 2 - o seno vai de -1 a 1.
    float ceu = total * 0.5;

    return clamp(ceu, 0.0, 1.0);
}

float cobertura(vec2 celula, vec2 rumo, float abertura) {
    float prof;
    vec2 lixo;

    if (abertura <= 0.0) {
        return transmissao(celula, rumo, false, prof, lixo);
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
    // Teto de amostras do bracket. Sem teto, o leque largo do ambiente pediria ~165 raios por
    // pixel pela formula do sol - medido, 45 ms so nisso.
    //
    // Mas o teto nao pode ser baixo: com 12 o passo angular vira 0.075 rad, que a 30 celulas nao
    // resolve fresta de duas celulas, e a luz que passa entre duas arvores sai em feixes finos.
    // Com 40 os feixes somem e o custo fica em 9 ms.
    int amostras = clamp(int(2.0 * abertura * length(grade)) + 1, 3, 40);

    float passo = 2.0 * abertura / float(amostras - 1);

    float angulo_anterior = -abertura;

    bool livre_anterior = transmissao(celula, rodar(rumo, angulo_anterior), false, prof, lixo) > 0.5;

    float livre_total = 0.0;

    for (int k = 1; k < amostras; k++) {
        float angulo = -abertura + float(k) * passo;

        bool livre = transmissao(celula, rodar(rumo, angulo), false, prof, lixo) > 0.5;

        if (livre == livre_anterior) {
            if (livre) {
                livre_total += sin(angulo) - sin(angulo_anterior);
            }
        } else {
            float baixo = angulo_anterior;
            float alto = angulo;

            for (int i = 0; i < PASSOS_DE_BISSECCAO; i++) {
                float meio = 0.5 * (baixo + alto);

                if ((transmissao(celula, rodar(rumo, meio), false, prof, lixo) > 0.5) == livre_anterior) {
                    baixo = meio;
                } else {
                    alto = meio;
                }
            }

            float fronteira = 0.5 * (baixo + alto);

            livre_total += livre_anterior ? sin(fronteira) - sin(angulo_anterior)
                                          : sin(angulo) - sin(fronteira);
        }

        angulo_anterior = angulo;
        livre_anterior = livre;
    }

    // Peso COSSENO, nao extensao angular crua: o que se soma e sin(b) - sin(a), que e a integral
    // de cos no trecho livre. As fronteiras ja saem exatas da bisseccao, entao a integral e fechada
    // e nao custa amostra nenhuma.
    //
    // Nao e enfeite. Extensao crua trata todas as direcoes igual e corta seco na borda do leque, e
    // um oclusor que cruza exatamente essa borda faz a derivada saltar. O lugar geometrico dos
    // pixels onde uma dada quina cai na borda do leque e uma RETA saindo daquela quina - eram
    // exatamente os riscos em leque que apareciam sob as copas. Com cosseno o peso ja chega a zero
    // no horizonte, entao nao ha borda para cruzar.
    //
    // E e o peso certo de qualquer forma: direcao rasante traz menos luz que direcao a pino. Para o
    // cone do sol, que tem 20 graus, cos varia 1,5% dentro dele - nao muda a sombra projetada.
    return livre_total / (2.0 * sin(abertura));
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
        float t = cobertura(celula, -direcao, penumbra);

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

        // O piso da sombra deixa de ser fixo: ele sobe com a luz que encosta nela.
        //
        // A medida e a MESMA do sol - fracao de um leque que alcanca a luz -, so que com abertura
        // larga em vez da do disco solar. Ponto encostado na borda tem quase todo o leque livre e
        // clareia; ponto no miolo de uma sombra grossa tem o leque quase todo tapado e nao clareia.
        // O "proporcional ao tamanho" cai daqui sozinho.
        //
        // Foi marcha LATERAL antes, medindo a largura ate a borda em duas direcoes. Aquilo dava
        // faixa dura: entre dois troncos os dois lados batem em materia, nenhum acha luz, e o pixel
        // cai no piso enquanto o vizinho clareia. Leque nao tem esse degrau - direcao tapada apenas
        // deixa de contribuir, e o valor anda continuo.
        //
        // So paga o custo quem esta na sombra: em pleno sol a cobertura ja e 1.
        float perto = 0.0;

        if (influencia_ambiente > 0.0 && visivel < 1.0) {
            // A MESMA medida do sol - fracao do leque que alcanca luz -, so que com abertura
            // larga em vez da do disco solar.
            //
            // Ja teve corte de alcance aqui, para o leque nao afirmar o que nao consegue resolver.
            // Saiu: "bloqueado depois de N celulas" e um teste de iso-distancia, e iso-distancia em
            // volta de uma quina e um ARCO - a sombra ficava com bolhas redondas. Sem o corte a
            // borda volta a ser reta, que e a forma que a geometria tem.
            //
            // Direcao bloqueada vale ZERO, e nao "quase nada porque a parede esta longe". Foi essa
            // a conta torta que clareou a escuridao inteira: dando meio ponto para o raio que morre
            // na parede, ponto fundo de sombra - e ate caixa fechada - recebia luz que nao existe.
            // Parede nao emite. O miolo escuro volta a ser escuro.
            //
            // A extensao angular sai por bisseccao, com fronteira exata, e nao por contagem de
            // raios livres. Contagem so consegue AMOSTRAS+1 valores, e num degrade suave esses
            // valores aparecem como escada.
            // Volta INTEIRA: luz encosta por qualquer lado, inclusive por baixo.
            //
            // Com meia volta virada pro sol isto nao funciona - a saida do vao embaixo da copa fica
            // pro lado oposto ao sol, fora do leque.
            // Duas medidas de contato, e vale a maior.
            //
            // O leque so enxerga em LINHA RETA, e por isso ele falha em beco: o raio bate na parede
            // ali do lado e o ar onde ele parou tambem esta na sombra, entao da zero mesmo tendo luz
            // logo depois da quina. Contato por raio nao dobra esquina.
            //
            // dados.b dobra: e a inundacao que a CPU faz pelo AR conectado, celula a celula. Beco
            // recebe pelo vizinho, e o vizinho pelo dele, ate o ceu. Dentro de caixa fechada nao ha
            // caminho de ar nenhum ate fora, entao ela continua zero - a regra da caixa nao muda.
            // O leque manda; a inundacao so PREENCHE O QUE FALTA nele.
            //
            // Sozinha a inundacao nao serve: ela e distancia ate o ceu, entao decai no ar aberto -
            // medido, 1,00 no alto e ~0,5 perto do chao - e esse decaimento vira sombra em cima do
            // mapa inteiro. Sozinho o leque tambem nao serve: ele zera em beco, porque raio reto
            // nao dobra esquina.
            //
            // Juntos cada um faz o que sabe: o leque diz quanto ceu se ve daqui, e o (1 - leque) da
            // a inundacao exatamente o buraco que sobrou.
            // Meia volta, virada pro sol: o HEMISFERIO DE CIMA. E a mesma convencao da CPU, e
            // pela mesma razao - so o ceu emite, raio pra baixo nao mede ceu, mede terra.
            //
            // Volta inteira punha o leque em ~0,5 no ar aberto so porque ha chao embaixo, e esse
            // meio termo virava sombra no mapa inteiro. Eu tinha tapado isso com um teste de
            // contato - segundo raio perguntando "essa parede esta no sol?" -, mas ele responde SIM
            // ou NAO, entao a sombra desenhada nas superficies distantes entrava no leque como
            // fronteira angular, e cada uma dessas virava um risco fino. Meia volta resolve a mesma
            // coisa sem contato, sem risco, e com metade das marchas.
            float leque = ceu_visivel(celula);

            float aberto = leque + ambiente_suave(celula) * (1.0 - leque);

            // Linear -> perceptual. `aberto` e uma fracao de oclusao, grandeza linear, mas ela
            // dirige um piso que MULTIPLICA um framebuffer sRGB. Sem converter, diferenca pequena
            // de oclusao perto do escuro vira diferenca grande de tom: a arvore grande esta alta e
            // ve muito ceu, as duas do meio estao baixas e o chao tapa parte do ceu delas, e essa
            // diferenca real e modesta saia na tela como sombra visivelmente mais escura nas duas.
            //
            // O expoente e a gama do sRGB, nao numero escolhido a olho.
            // O max nao e paranoia: pow(0, y) e INDEFINIDO em GLSL, e o driver aqui devolve NaN.
            // NaN atravessa os dois mix e sai como pixel sem escurecimento nenhum - dentro da caixa,
            // onde `aberto` e exatamente zero, isso desenhava cunhas azul-ceu de 45 graus, o
            // serrilhado. Nao aparecia com influencia baixa porque o ganho la ja deixava tudo
            // escuro o bastante para o NaN se confundir com o piso.
            // O zero tem que sobreviver EXATO. pow(0, y) e indefinido em GLSL e devolve NaN
            // aqui, mas tapar isso com max(aberto, 1e-5) e pior: a gama devolve 0.0053, o ganho
            // pega esses 0.5% e, com a influencia no maximo, levanta o piso de 0.05 para 0.19 -
            // lugar completamente fechado clareando so por subir a influencia, que e justamente o
            // que ela nao pode fazer. Sem contato de luz, sem clareamento, em qualquer valor.
            aberto = aberto > 0.0 ? pow(aberto, 1.0 / 2.2) : 0.0;

            // E a configuracao e a INFLUENCIA, que e coisa diferente da abertura. Enquanto ela era
            // a abertura, o maximo nao podia apagar a sombra: `aberto` e oclusao de verdade, e
            // ponto embaixo de uma copa larga tem meio hemisferio tapado por ela - o maximo dava
            // meia sombra, nao sombra nenhuma. Nao havia como regular ate o transparente.
            //
            // A curva e um ganho: com k = (1-i)/i,
            //
            //     preenchimento = aberto / (aberto + (1 - aberto) * k)
            //
            //   i = 0.5  ->  k = 1  ->  preenchimento = aberto, que e o comportamento de hoje
            //   i -> 1   ->  k -> 0 ->  preenchimento -> 1, sombra transparente onde ha contato
            //   i -> 0   ->  k -> inf -> preenchimento -> 0, sombra intacta
            //
            // Nos dois extremos e exatamente o que a palavra influencia quer dizer, e no meio ela
            // passa pelo valor geometrico puro. Sem constante escolhida a mao.
            //
            // Fechado continua fechado: numa caixa nenhuma direcao escapa, aberto e ZERO, e zero
            // dividido por qualquer coisa e zero. Influencia total nao inventa luz onde nao ha.
            // O ganho tem um teto de inclinacao, e ele nao e escolhido: e a resolucao do
            // proprio `aberto`. A menor parcela de ceu que o algoritmo consegue distinguir e uma
            // celula la no limite do alcance, que pesa ~1/(2*RAIO_DO_CEU) do hemisferio. Abaixo
            // disso o valor nao significa nada, e amplificar diferenca ali e amplificar ruido.
            //
            // Sem o teto, influencia 1 zera k e o ganho vira DEGRAU: `perto` salta de 0 para 1 no
            // instante em que `aberto` sai de zero. Dentro da caixa o miolo da zero e as quinas dao
            // um fiapo, e o degrau entre os dois sai como serrilhado seguindo a grade.
            // Teto de inclinacao do ganho: influencia 1 zera k e o ganho vira DEGRAU, saltando
            // de 0 para 1 no instante em que `aberto` sai de zero. O teto e a resolucao da propria
            // medida - a menor parcela de ceu distinguivel e uma celula no limite do alcance, que
            // pesa 1/(2*RAIO_DO_CEU). Ganho mais ingreme que isso amplifica o que ela nao resolve.
            // A propriedade e um PESO, e nada mais: quanto do ceu medido vira clareamento.
            //
            // Era um ganho com k = (1-i)/i, uma hiperbole, e ela existia so para forcar um
            // requisito - influencia 1 apagar a sombra mesmo onde a geometria nao da para tanto.
            // Esse forcamento custava caro: a hiperbole despenca perto de 0 e nao anda perto de 1,
            // entao o curso do controle ficava todo amontoado embaixo; e como ela vira degrau em
            // i = 1, precisava de teto de inclinacao, e da gama na entrada para desamontoar.
            //
            // Multiplicacao nao tem nada disso. O curso e parelho por construcao, nao ha degrau em
            // ponta nenhuma, e cada uma das tres pecas some junto com a hiperbole. O preco e que no
            // maximo a sombra e preenchida pelo que a geometria permite, e nao apagada a forca -
            // que e mais honesto de qualquer forma.
            perto = aberto * influencia_ambiente;
        }

        brilho = mix(mix(piso_ar, 1.0, perto), 1.0, visivel);
    }

    COLOR = vec4(vec3(brilho), 1.0);
}
