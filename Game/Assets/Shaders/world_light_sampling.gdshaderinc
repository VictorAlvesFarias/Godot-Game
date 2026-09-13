#include "res://Assets/Shaders/analytic_sun.gdshaderinc"
uniform sampler2D emission_data : filter_nearest, repeat_disable;
uniform float ambient_energy = 0.32;
uniform float sun_energy = 0.85;
uniform float emission_energy = 1.0;
uniform vec4 sky_color : source_color = vec4(0.72, 0.83, 1.0, 1.0);
uniform vec4 sun_color : source_color = vec4(1.0, 0.95, 0.84, 1.0);
uniform bool show_raw = false;
uniform bool geometry_ready = false;

// Finite local projection from depth openings. One grid traversal, with a
// mip-filtered aperture footprint for the angular size of the existing sun.
uniform sampler2D depth_beam_data : filter_linear_mipmap, repeat_disable;
uniform bool depth_beam_enabled = false;
// Um peso por alvo da sombra projetada. 1 = sombra cheia, 0 = aquele alvo nao recebe sombra.
// O padrao 1 mantem o comportamento de quem nao alimenta estes uniforms.
uniform float shadow_air = 1.0;
uniform float shadow_terrain = 1.0;
uniform float shadow_background = 1.0;
uniform float volumetric_reach = 24.0;
// Recepcao da superficie solida. Desligada, o terreno para de receber luz por completo.
uniform bool terrain_light_enabled = true;
// Ate onde a escuridao avanca sobre o vizinho iluminado, em tiles. A regra e de mao unica: o
// escuro invade, a luz nunca. Por isso a linha de encontro vira rampa sem clarear o interior
// escuro nem inventar halo. 0 devolve o recorte duro.
uniform float edge_fade_tiles = 0.0;
uniform float volume_density = 0.12;
float window_beam(vec2 p) {
    vec2 size = vec2(textureSize(depth_beam_data, 0));
    p += vec2(24.0);
    ivec2 cell = ivec2(floor(p));
    vec3 receiver = texelFetch(depth_beam_data, cell, 0).rgb;
    if (receiver.b < 0.5 || receiver.g > 0.5) return 0.0;
    vec2 ray = vec2(sin(sun_angle), -cos(sun_angle));
    if (ray.y >= -0.001) return 0.0;
    vec2 stride = 1.0 / max(abs(ray), vec2(0.000001));
    ivec2 direction = ivec2(sign(ray));
    vec2 boundary = vec2(ray.x >= 0.0 ? float(cell.x+1)-p.x : p.x-float(cell.x),
                         ray.y >= 0.0 ? float(cell.y+1)-p.y : p.y-float(cell.y)) * stride;
    float distance = 0.0;
    float spread = tan(radians(8.0) * pow(1.0-clamp(penumbra,0.0,1.0),2.0));
    float beam = 0.0;
    float reach = clamp(volumetric_reach, 0.25, 24.0);
    for (int step_index = 0; step_index < 64; step_index++) {
        distance = min(boundary.x,boundary.y);
        if (distance >= reach) break;
        if (boundary.x <= distance + 0.000001) { cell.x += direction.x; boundary.x += stride.x; }
        if (boundary.y <= distance + 0.000001) { cell.y += direction.y; boundary.y += stride.y; }
        if (any(lessThan(cell,ivec2(0))) || any(greaterThanEqual(cell,ivec2(size)))) break;
        if (texelFetch(depth_beam_data,cell,0).g > 0.5) break;
        vec2 point = p + ray * (distance + 0.01);
        float footprint = max(1.0, 2.0 * distance * spread);
        float aperture = textureLod(depth_beam_data, point / size, log2(footprint)).r;
        float fade = exp(-distance / (reach * 0.5)) * (1.0-smoothstep(reach * 0.833333,reach,distance));
        beam = max(beam,aperture * fade);
        if (beam > 0.99) break;
    }
    return beam;
}

// Qual alvo esta neste pixel, e com que peso ele recebe a sombra projetada. Solido tem prioridade
// sobre parede de fundo: quem aparece na tela e o tile, nao a parede atras dele.
float receiver_shadow_weight(vec2 uv) {
    ivec2 cell = ivec2(floor(uv*map_size*0.5))+ivec2(24);
    ivec2 dimensions = textureSize(depth_beam_data,0);
    cell = clamp(cell,ivec2(0),dimensions-ivec2(1));
    vec3 data = texelFetch(depth_beam_data,cell,0).rgb;
    float weight = data.g > 0.5 ? shadow_terrain : (data.b > 0.5 ? shadow_background : shadow_air);
    return clamp(weight,0.0,1.0);
}
// `shadow_weight` escala a oclusao solar deste pixel. 1 = sombra projetada cheia, 0 = nenhuma
// sombra projetada, com a iluminacao do alvo intacta nos dois casos.
vec3 compose_world_light(vec2 uv, vec2 footprint, float shadow_weight) {
    vec2 p = uv * map_size - 0.5;
    ivec2 base = ivec2(floor(p));
    vec2 f = fract(p);
    ivec2 last = ivec2(map_size) - ivec2(1);
    ivec2 center = clamp(ivec2(floor(uv * map_size)), ivec2(0), last);
    float material = texelFetch(light_data, center, 0).b;
    float sky = 0.0;
    vec3 emission = vec3(0.0);
    float weight_sum = 0.0;
    for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) {
        ivec2 c = clamp(base + ivec2(x, y), ivec2(0), last);
        vec3 v = texelFetch(light_data, c, 0).rgb;
        float w = (x == 0 ? 1.0 - f.x : f.x) * (y == 0 ? 1.0 - f.y : f.y);
        w *= abs(v.b - material) < 0.01 ? 1.0 : 0.0;
        sky += v.r * w;
        emission += texelFetch(emission_data, c, 0).rgb * w;
        weight_sum += w;
    }
    sky /= max(weight_sum, 0.0001);
    emission /= max(weight_sum, 0.0001);
    // Evaluate coverage at the actual display pixel, not a magnified cached texel.
    vec2 world_pixel = uv * map_size / 2.0;
    float sun = 0.0;
    if (geometry_ready) {
        // Todo bloqueador projeta em todo lugar; a diferenca entre os alvos e de peso. E o peso
        // multiplica a oclusao, nao a composicao: peso 0 tira a sombra e deixa a luz do alvo.
        vec2 parts = analytic_sun_parts(world_pixel, footprint);
        sun = (1.0 - parts.x * clamp(shadow_weight, 0.0, 1.0)) * parts.y;
        if (depth_beam_enabled) sun = max(sun,window_beam(world_pixel));
    }
    // "Iluminacao do terreno" desligada: a face solida deixa de receber qualquer contribuicao.
    if (!terrain_light_enabled && material > 0.5) { sky = 0.0; sun = 0.0; emission = vec3(0.0); }
    // Reserve the display range for unoccluded daylight BEFORE applying attenuation.
    // Clipping ambient + sun afterwards erases the first portion of every gradient.
    vec3 daylight = sky_color.rgb * ambient_energy + sun_color.rgb * sun_energy;
    float daylight_scale = max(1.0, max(daylight.r, max(daylight.g, daylight.b)));
    vec3 energy = (sky_color.rgb * sky * ambient_energy + sun_color.rgb * sun * sun_energy) / daylight_scale + emission * emission_energy;
    // Dilatacao do escuro sobre o solido. So o bloco escurece - o ar aberto fica intacto, senao a
    // escuridao de dentro vazaria como halo no ceu. Compara a media do ceu num raio de N tiles com
    // o ceu local: longe de escuridao elas empatam e nada muda; encostado nela a razao cai e leva
    // junto o sol, que e quem mantinha a face do bloco acesa contra o preto. A linha vira rampa.
    float fade = clamp(edge_fade_tiles, 0.0, 4.0);
    if (fade > 0.01 && material > 0.5) {
        // Distancia geometrica ate a face escura, medida em tiles dentro do proprio tile. Sai da
        // geometria e nao da interpolacao da textura, entao a largura da rampa e exatamente a
        // pedida - a leitura bilinear ficava presa a meio texel, um quarto de tile.
        vec2 tile_position = uv * map_size * 0.5;
        vec2 inside = fract(tile_position);
        float factor = 1.0;
        for (int k = 0; k < 8; k++) {
            vec2 towards = vec2(k == 0 ? 1.0 : k == 1 ? -1.0 : k == 2 ? 0.0 : k == 3 ? 0.0
                              : k == 4 ? 1.0 : k == 5 ? 1.0 : k == 6 ? -1.0 : -1.0,
                                k == 0 ? 0.0 : k == 1 ? 0.0 : k == 2 ? 1.0 : k == 3 ? -1.0
                              : k == 4 ? 1.0 : k == 5 ? -1.0 : k == 6 ? 1.0 : -1.0);
            ivec2 c = clamp(ivec2(floor((tile_position + towards * 0.6) * 2.0)), ivec2(0), last);
            vec2 neighbour = texelFetch(light_data, c, 0).rb;  // ceu, material
            if (neighbour.y > 0.5) continue;                   // vizinho solido nao dissolve nada
            vec2 gap = vec2(towards.x > 0.0 ? 1.0 - inside.x : towards.x < 0.0 ? inside.x : 0.0,
                            towards.y > 0.0 ? 1.0 - inside.y : towards.y < 0.0 ? inside.y : 0.0);
            // Na divisa a distancia e zero e o bloco assume a luz do vizinho: encostado no preto,
            // preto. Dali para dentro sobe ate o proprio valor ao completar `fade` tiles.
            float reached = smoothstep(0.0, 1.0, clamp(length(gap) / fade, 0.0, 1.0));
            factor = min(factor, mix(clamp((neighbour.x + 0.02) / (sky + 0.02), 0.0, 1.0), 1.0, reached));
        }
        // Razao, e nao substituicao, para a rampa levar junto o sol - que e quem mantinha a face
        // do bloco acesa encostada no preto.
        energy *= factor;
    }
    return show_raw ? vec3(sky, sun, material) : clamp(energy, vec3(0.0), vec3(1.0));
}

vec4 sample_world_light(vec2 uv, vec2 footprint) {
    // Alpha 1: o overlay agora aplica a iluminacao em todo pixel. O que muda por alvo e a sombra.
    return vec4(compose_world_light(uv, footprint, receiver_shadow_weight(uv)), 1.0);
}
