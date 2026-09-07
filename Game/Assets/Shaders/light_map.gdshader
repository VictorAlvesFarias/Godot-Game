shader_type canvas_item;
render_mode blend_mix, unshaded;

// Um texel por celula. R guarda profundidade: 0 = ar, 1 = materia exposta, n = materia funda.
uniform sampler2D dados_mapa : filter_nearest, repeat_disable;

uniform vec2 grade = vec2(1.0);
uniform vec2 direcao_sol = vec2(0.0, 1.0);

// Meia abertura angular do sol, em radianos. E ela que da a transicao suave da penumbra.
uniform float abertura_sol = 0.01;

uniform float influencia_ambiente = 0.65;

uniform int alcance_sol = 96;
uniform int alcance_ceu = 24;

uniform bool ar_ligado = true;
uniform bool mostrar_mapa_cru = false;

const int MAX_INTERVALOS = 12;
const int MAX_LARGURA_FEIXE = 24;
const float EPS = 0.0001;

// Profundidade em celulas ate o ar mais proximo. Fora da grade conta como ar.
float profundidade(ivec2 c) {
    if (c.x < 0 || c.y < 0 || c.x >= int(grade.x) || c.y >= int(grade.y)) {
        return 0.0;
    }

    return texelFetch(dados_mapa, c, 0).r * 255.0;
}

// So a casca solida ocluir. Qualquer raio que alcance o miolo ja atravessou a casca, entao
// ignorar o miolo nao muda a uniao dos intervalos bloqueados, e corta muito trabalho.
bool oclusor(ivec2 c) {
    float d = profundidade(c);
    return d > 0.5 && d < 1.5;
}

vec2 canto(ivec2 c, int i) {
    vec2 base = vec2(float(c.x), float(c.y));

    if (i == 0) {
        return base;
    }

    if (i == 1) {
        return base + vec2(1.0, 0.0);
    }

    if (i == 2) {
        return base + vec2(0.0, 1.0);
    }

    return base + vec2(1.0, 1.0);
}

// Angulo de `d` medido a partir de `eixo`, positivo girando sempre para o mesmo lado.
float angulo(vec2 eixo, vec2 d) {
    return atan(eixo.x * d.y - eixo.y * d.x, dot(eixo, d));
}

// Fonte de brilho constante vista por um receptor: peso cosseno em torno do eixo.
float energia(float a, float b) {
    return max(sin(b) - sin(a), 0.0);
}

// Intervalo angular exato que a celula ocupa, ja recortado pela abertura da fonte.
bool intervalo_da_celula(vec2 p, ivec2 c, vec2 eixo, float limite, out float a, out float b) {
    float menor = 4.0;
    float maior = -4.0;

    for (int i = 0; i < 4; i++) {
        float ang = angulo(eixo, canto(c, i) - p);
        menor = min(menor, ang);
        maior = max(maior, ang);
    }

    if (maior <= -limite || menor >= limite) {
        return false;
    }

    a = max(menor, -limite);
    b = min(maior, limite);

    return b - a > EPS;
}

// Remove [a, b] da lista de intervalos ainda visiveis. E aqui que mora a suavidade: o que sobra
// e medida angular continua, nao contagem de raios.
void subtrair(float a, float b, inout float ini[MAX_INTERVALOS], inout float fim[MAX_INTERVALOS], inout int total) {
    float novo_ini[MAX_INTERVALOS];
    float novo_fim[MAX_INTERVALOS];
    int novo_total = 0;

    for (int i = 0; i < MAX_INTERVALOS; i++) {
        if (i >= total) {
            break;
        }

        float x = ini[i];
        float y = fim[i];

        if (b <= x || a >= y) {
            if (novo_total < MAX_INTERVALOS) {
                novo_ini[novo_total] = x;
                novo_fim[novo_total] = y;
                novo_total++;
            }
        } else {
            if (a > x && novo_total < MAX_INTERVALOS) {
                novo_ini[novo_total] = x;
                novo_fim[novo_total] = a;
                novo_total++;
            }

            if (b < y && novo_total < MAX_INTERVALOS) {
                novo_ini[novo_total] = b;
                novo_fim[novo_total] = y;
                novo_total++;
            }
        }
    }

    total = novo_total;

    for (int i = 0; i < MAX_INTERVALOS; i++) {
        if (i >= total) {
            break;
        }

        ini[i] = novo_ini[i];
        fim[i] = novo_fim[i];
    }
}

float fracao_visivel(float ini[MAX_INTERVALOS], float fim[MAX_INTERVALOS], int total, float limite) {
    float visivel = 0.0;

    for (int i = 0; i < MAX_INTERVALOS; i++) {
        if (i >= total) {
            break;
        }

        visivel += energia(ini[i], fim[i]);
    }

    float cheio = energia(-limite, limite);

    return cheio > EPS ? clamp(visivel / cheio, 0.0, 1.0) : 0.0;
}

// O sol e direcional: so o cone que aponta para ele pode ser bloqueado. A varredura anda pelo eixo
// dominante do cone, uma linha de celulas por vez, e em cada linha visita o intervalo contiguo de
// celulas que o cone atravessa. Cobertura exata: caminhar por pontos discretos ao longo do raio
// deixaria buracos, e buraco em oclusor vira ponto claro dentro da sombra.
float luz_do_sol(vec2 p, ivec2 origem) {
    float limite = max(abertura_sol, EPS);

    float ini[MAX_INTERVALOS];
    float fim[MAX_INTERVALOS];
    int total = 1;

    ini[0] = -limite;
    fim[0] = limite;

    vec2 eixo = normalize(-direcao_sol);
    float espalhamento = tan(limite);

    // Espaco (dominante, transversal). Trocar os eixos deixa um loop so cobrir os dois casos.
    bool vertical = abs(eixo.y) >= abs(eixo.x);
    vec2 q = vertical ? p.yx : p.xy;
    vec2 e = vertical ? eixo.yx : eixo.xy;
    ivec2 o = vertical ? origem.yx : origem.xy;

    float sentido = e.x >= 0.0 ? 1.0 : -1.0;
    float inclinacao = e.y / e.x;
    float escala = 1.0 / abs(e.x);

    for (int i = 1; i <= alcance_sol; i++) {
        if (total <= 0) {
            break;
        }

        int dominante = o.x + int(sentido) * i;
        int fim_grade = vertical ? int(grade.y) : int(grade.x);

        if (dominante < 0 || dominante >= fim_grade) {
            break;
        }

        float centro_linha = float(dominante) + 0.5;
        float avanco = centro_linha - q.x;
        float distancia = abs(avanco) * escala;

        if (distancia > float(alcance_sol)) {
            break;
        }

        float transversal = q.y + inclinacao * avanco;
        float meia = min(distancia * espalhamento * escala + 0.75, float(MAX_LARGURA_FEIXE));

        int primeiro = int(floor(transversal - meia));
        int ultimo = int(floor(transversal + meia));

        for (int t = primeiro; t <= ultimo; t++) {
            ivec2 c = vertical ? ivec2(t, dominante) : ivec2(dominante, t);

            if (c == origem || !oclusor(c)) {
                continue;
            }

            float a;
            float b;

            if (intervalo_da_celula(p, c, eixo, limite, a, b)) {
                subtrair(a, b, ini, fim, total);
            }
        }
    }

    return fracao_visivel(ini, fim, total, limite);
}

// O ceu cobre o hemisferio de cima. So celulas na mesma linha ou acima podem tapar algo dele.
float luz_do_ceu(vec2 p, ivec2 origem) {
    float limite = PI * 0.5;

    float ini[MAX_INTERVALOS];
    float fim[MAX_INTERVALOS];
    int total = 1;

    ini[0] = -limite;
    fim[0] = limite;

    vec2 eixo = vec2(0.0, -1.0);
    int r = alcance_ceu;

    for (int i = 0; i <= r; i++) {
        if (total <= 0) {
            break;
        }

        int dy = -i;

        for (int dx = -r; dx <= r; dx++) {
            if (dx * dx + dy * dy > r * r) {
                continue;
            }

            ivec2 c = origem + ivec2(dx, dy);

            if (c == origem || !oclusor(c)) {
                continue;
            }

            float a;
            float b;

            if (intervalo_da_celula(p, c, eixo, limite, a, b)) {
                subtrair(a, b, ini, fim, total);
            }
        }
    }

    return fracao_visivel(ini, fim, total, limite);
}

void fragment() {
    // Posicao continua em celulas. O passe roda com subdivisao por tile, entao o contorno da
    // sombra nao fica preso na escada da grade.
    vec2 p = UV * grade;
    ivec2 c = ivec2(floor(p));
    float prof = profundidade(c);
    bool materia = prof > 0.5;

    if (mostrar_mapa_cru) {
        COLOR = vec4(vec3(clamp(prof / 8.0, 0.0, 1.0)), 1.0);
    } else if (materia || !ar_ligado) {
        COLOR = vec4(1.0);
    } else {
        float sol = luz_do_sol(p, c);
        float ceu = luz_do_ceu(p, c) * influencia_ambiente;
        float luz = clamp(sol + ceu, 0.0, 1.0);

        COLOR = vec4(vec3(luz), 1.0);
    }
}
