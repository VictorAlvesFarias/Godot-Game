shader_type canvas_item;
render_mode blend_mix, unshaded;

uniform sampler2D light_data : filter_nearest, repeat_disable;
uniform sampler2D boundary_data : filter_linear, repeat_disable;
uniform vec2 map_size = vec2(1.0);
uniform float sun_angle = -0.5585;
uniform float penumbra = 0.5;
const int SUN_SAMPLES = 33;

float opacity(ivec2 c) {
    ivec2 size = ivec2(map_size / 2.0);
    if (c.x < 0 || c.y < 0 || c.x >= size.x || c.y >= size.y) return 0.0;
    return texelFetch(light_data, c * 2, 0).b;
}

float outside_light(vec2 p, int direction, int edge) {
    float offset = edge < 2 ? p.x : p.y;
    float width = max(map_size.x, map_size.y) + 1.0;
    return texture(boundary_data, vec2((offset * 2.0 + 0.5) / width,
        (float(direction * 4 + edge) + 0.5) / float(SUN_SAMPLES * 4))).r;
}

float trace_sun(vec2 p, vec2 e, int direction) {
    if (e.y >= -0.001) return 0.0;
    ivec2 size = ivec2(map_size / 2.0);
    ivec2 c = ivec2(floor(p));
    ivec2 step_dir = ivec2(e.x >= 0.0 ? 1 : -1, -1);
    vec2 delta = 1.0 / max(abs(e), vec2(0.000001));
    vec2 next = vec2(e.x >= 0.0 ? float(c.x + 1) - p.x : p.x - float(c.x), p.y - float(c.y)) * delta;
    float t = 0.0;
    float transmission = 1.0;
    // Only the receiving solid may be traversed, toward the light. Once outside,
    // every subsequent solid blocks the ray, including the opposite room wall.
    bool surface = opacity(c) > 0.99;
    for (int i = 0; i < size.x + size.y + 4; i++) {
        if (c.x < 0 || c.y < 0 || c.x >= size.x || c.y >= size.y) {
            if (surface) return 0.0;
            int edge = c.y < 0 ? 0 : c.y >= size.y ? 1 : c.x < 0 ? 2 : 3;
            return transmission * outside_light(p + e * t, direction, edge);
        }
        float stop = min(next.x, next.y);
        float extinction = opacity(c);
        if (surface && extinction < 0.99) {
            transmission = pow(0.43, max(0.0, t - 0.5)) * (1.0 - smoothstep(2.0, 3.0, t));
            surface = false;
        }
        if (surface) {
            if (stop >= 3.0) return 0.0;
        } else {
            if (extinction > 0.99) return 0.0;
            transmission *= pow(max(1.0 - extinction, 0.00001), max(0.0, stop - t));
            if (transmission < 0.0039) return 0.0;
        }
        bool cross_x = next.x <= stop + 0.000001;
        bool cross_y = next.y <= stop + 0.000001;
        if (cross_x) { c.x += step_dir.x; next.x += delta.x; }
        if (cross_y) { c.y += step_dir.y; next.y += delta.y; }
        t = stop;
    }
    return 0.0;
}

void fragment() {
    vec2 p = UV * map_size / 2.0;
    float spread = radians(8.0) * pow(1.0 - clamp(penumbra, 0.0, 1.0), 2.0);
    float strongest_block = 0.0;
    float blocked_weight = 0.0;
    float weight_sum = 0.0;
    // Artistic cone envelope, symmetric about SunAngle at every world position.
    // Preserve optical transmission of leaves; soften only the envelope fringe.
    // Averaging disk visibility alone would soften an otherwise parallel shadow.
    for (int i = 0; i < SUN_SAMPLES; i++) {
        if (spread == 0.0 && i != 16) continue;
        float u = float(i - 16) / 16.0;
        float weight = i == 0 || i == 32 ? 0.5 : 1.0;
        float a = sun_angle + u * spread;
        float blocked = 1.0 - trace_sun(p, vec2(sin(a), -cos(a)), i);
        strongest_block = max(strongest_block, blocked);
        blocked_weight += blocked * weight;
        weight_sum += weight;
    }
    float coverage = blocked_weight / max(strongest_block * weight_sum, 0.000001);
    float sun = 1.0 - strongest_block * smoothstep(0.0, 0.12, coverage);
    COLOR = vec4(vec3(sun), 1.0);
}
