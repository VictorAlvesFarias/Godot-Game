#[compute]
#version 450

// Mesma conta do LightPropagationSystem (CPU), so que em forma de "gather": no BFS o valor que
// entra numa celula e sempre multiplicado pelo falloff DA CELULA DE DESTINO, entao ler os 8
// vizinhos e pegar o max da propagacao para ca da exatamente o mesmo resultado. Cada dispatch
// avanca 1 tile; 24 passadas cobrem o alcance maximo (0.85^24 < 0.02).
layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(r8, set = 0, binding = 0) uniform restrict readonly image2D terrain;
layout(rgba32f, set = 0, binding = 1) uniform restrict readonly image2D seed;
layout(rgba32f, set = 0, binding = 2) uniform restrict readonly image2D src;
layout(rgba32f, set = 0, binding = 3) uniform restrict writeonly image2D dst;

layout(push_constant, std430) uniform Params {
    ivec2 size;
    float air_falloff;
    float solid_falloff;
    float threshold;
    float pad0;
    float pad1;
    float pad2;
} params;

const ivec2 OFFSETS[8] = ivec2[8](
    ivec2(1, 0), ivec2(-1, 0), ivec2(0, 1), ivec2(0, -1),
    ivec2(1, 1), ivec2(1, -1), ivec2(-1, 1), ivec2(-1, -1));

void main() {
    ivec2 cell = ivec2(gl_GlobalInvocationID.xy);
    if (cell.x >= params.size.x || cell.y >= params.size.y) return;

    vec3 value = imageLoad(seed, cell).rgb;
    float falloff = imageLoad(terrain, cell).r > 0.5 ? params.solid_falloff : params.air_falloff;
    float straight = falloff;
    float diagonal = pow(falloff, 1.41421356);

    for (int i = 0; i < 8; i++) {
        ivec2 neighbor = cell + OFFSETS[i];
        if (neighbor.x < 0 || neighbor.y < 0 || neighbor.x >= params.size.x || neighbor.y >= params.size.y) continue;
        vec3 propagated = imageLoad(src, neighbor).rgb * (i < 4 ? straight : diagonal);
        // Mesmo corte do BFS: so descarta quando os tres canais ficam abaixo do limiar.
        if (propagated.r < params.threshold && propagated.g < params.threshold && propagated.b < params.threshold) continue;
        value = max(value, propagated);
    }

    imageStore(dst, cell, vec4(value, 1.0));
}
