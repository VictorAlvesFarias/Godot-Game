shader_type canvas_item;
render_mode blend_mix, unshaded;

uniform sampler2D light_data : filter_nearest, repeat_disable;
uniform sampler2D boundary_data : filter_linear, repeat_disable;
uniform vec2 map_size = vec2(1.0);
uniform float sun_angle = -0.5585;
uniform float penumbra = 0.5;

float opacity(ivec2 c) {
    ivec2 size = ivec2(map_size / 2.0);
    if (c.x < 0 || c.y < 0 || c.x >= size.x || c.y >= size.y) return 0.0;
    return texelFetch(light_data, c * 2, 0).b;
}

float outside_light(vec2 p, int direction, int edge) {
    float offset = edge < 2 ? p.x : p.y;
    float width = max(map_size.x, map_size.y) + 1.0;
    return texture(boundary_data, vec2((offset * 2.0 + 0.5) / width, (float(direction * 4 + edge) + 0.5) / 36.0)).r;
}

float trace_sun(vec2 p, vec2 e, int direction) {
    if (e.y >= -0.001) return 0.0;
    ivec2 size = ivec2(map_size / 2.0);
    ivec2 c = ivec2(floor(p));
    ivec2 step_dir = ivec2(e.x >= 0.0 ? 1 : -1, e.y >= 0.0 ? 1 : -1);
    vec2 delta = 1.0 / max(abs(e), vec2(0.000001));
    vec2 next = vec2(e.x >= 0.0 ? float(c.x + 1) - p.x : p.x - float(c.x), e.y >= 0.0 ? float(c.y + 1) - p.y : p.y - float(c.y)) * delta;
    float t = 0.0;
    float transmission = 1.0;
    // DDA visits every crossed cell. The boundary continues the SAME ray through
    // logical columns, including geometry outside the rendered/simulated region.
    for (int i = 0; i < size.x + size.y + 4; i++) {
        if (c.x < 0 || c.y < 0 || c.x >= size.x || c.y >= size.y) {
            int edge = c.y < 0 ? 0 : c.y >= size.y ? 1 : c.x < 0 ? 2 : 3;
            return transmission * outside_light(p + e * t, direction, edge);
        }
        float stop = min(next.x, next.y);
        float extinction = opacity(c);
        if (extinction > 0.99) return 0.0;
        transmission *= pow(max(1.0 - extinction, 0.00001), max(0.0, stop - t));
        if (transmission < 0.0039) return 0.0;
        bool cross_x = next.x <= stop + 0.000001;
        bool cross_y = next.y <= stop + 0.000001;
        if (cross_x) { c.x += step_dir.x; next.x += delta.x; }
        if (cross_y) { c.y += step_dir.y; next.y += delta.y; }
        t = stop;
    }
    return 0.0;
}

void fragment() {
    vec2 pixel = UV * map_size - 0.5;
    ivec2 base = ivec2(floor(pixel));
    vec2 f = fract(pixel);
    ivec2 last = ivec2(map_size) - ivec2(1);
    ivec2 center = clamp(ivec2(floor(UV * map_size)), ivec2(0), last);
    float material = texelFetch(light_data, center, 0).b;
    float depth = 0.0;
    float weights = 0.0;
    for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) {
        ivec2 c = clamp(base + ivec2(x, y), ivec2(0), last);
        vec3 v = texelFetch(light_data, c, 0).rgb;
        float w = (x == 0 ? 1.0 - f.x : f.x) * (y == 0 ? 1.0 - f.y : f.y);
        w *= abs(v.b - material) < 0.01 ? 1.0 : 0.0;
        depth += v.g * w;
        weights += w;
    }
    depth /= max(weights, 0.0001);
    vec2 p = UV * map_size / 2.0;
    ivec2 cell = ivec2(floor(p));
    vec2 receiver = p;
    if (material > 0.99) {
        float nearest = 1000.0;
        for (int distance = 1; distance <= 3; distance++) for (int side = 0; side < 4; side++) {
            ivec2 d = side == 0 ? ivec2(-1, 0) : side == 1 ? ivec2(1, 0) : side == 2 ? ivec2(0, -1) : ivec2(0, 1);
            ivec2 n = cell + d * distance;
            if (opacity(n) > 0.99) continue;
            vec2 q = vec2(d.x == 0 ? p.x : float(n.x) + (d.x > 0 ? 0.001 : 0.999), d.y == 0 ? p.y : float(n.y) + (d.y > 0 ? 0.001 : 0.999));
            float metric = abs(q.x - p.x) + abs(q.y - p.y);
            if (metric < nearest) { nearest = metric; receiver = q; }
        }
    }
    float sun = 0.0;
    if (depth > 0.0) {
        float spread = atan(tan(radians(5.0)) * (1.0 - penumbra));
        for (int i = 0; i < 9; i++) {
            float a = sun_angle + float(i - 4) * spread / 4.0;
            sun += trace_sun(receiver, vec2(sin(a), -cos(a)), i);
        }
        sun *= depth / 9.0;
    }
    COLOR = vec4(vec3(sun), 1.0);
}
