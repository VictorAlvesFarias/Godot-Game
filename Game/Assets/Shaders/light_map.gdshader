shader_type canvas_item;
render_mode blend_mix, unshaded;

uniform sampler2D light_data : filter_nearest, repeat_disable;
uniform sampler2D boundary_data : filter_linear, repeat_disable;
uniform vec2 map_size = vec2(1.0);
uniform float sun_angle = -0.5585;
uniform float penumbra = 0.5;
uniform vec2 source_reference = vec2(0.0);
uniform float source_radius = 1.5;
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

float trace_sun(vec2 p, vec2 e, int direction, float source_distance) {
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
        if (t >= source_distance) return surface ? 0.0 : transmission;
        if (c.x < 0 || c.y < 0 || c.x >= size.x || c.y >= size.y) {
            if (surface) return 0.0;
            int edge = c.y < 0 ? 0 : c.y >= size.y ? 1 : c.x < 0 ? 2 : 3;
            return transmission * outside_light(p + e * t, direction, edge);
        }
        float stop = min(min(next.x, next.y), source_distance);
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
    float inverse_distance = pow(1.0 - clamp(penumbra, 0.0, 1.0), 2.0) / 64.0;
    vec2 axis = vec2(sin(sun_angle), -cos(sun_angle));
    vec2 tangent = vec2(cos(sun_angle), sin(sun_angle));
    float sun = 0.0;
    float weight_sum = 0.0;
    // Every pixel aims at the same world-space source. A nearby source expands
    // the entire projection; the infinity limit has genuinely parallel rays.
    for (int i = 0; i < SUN_SAMPLES; i++) {
        if ((inverse_distance == 0.0 || source_radius == 0.0) && i != 16) continue;
        float u = float(i - 16) / 16.0;
        float weight = sqrt(max(0.0, 1.0 - u * u));
        if (weight == 0.0) continue;
        vec2 ray = axis + (source_reference - p + tangent * u * source_radius) * inverse_distance;
        float ray_length = length(ray);
        float distance_to_source = inverse_distance == 0.0 ? 1e30 : ray_length / inverse_distance;
        sun += trace_sun(p, ray / max(ray_length, 0.000001), i, distance_to_source) * weight;
        weight_sum += weight;
    }
    COLOR = vec4(vec3(sun / weight_sum), 1.0);
}
