uniform sampler2D light_data : filter_nearest, repeat_disable;
// RGBA32F spatial index and compressed logical rectangles; no ray samples.
uniform sampler2D shadow_geometry : filter_nearest, repeat_disable;
uniform vec2 map_size = vec2(1.0);
uniform float sun_angle = -0.5585;
uniform float penumbra = 0.5;
uniform float penumbra_gradient = 1.4;
uniform float penumbra_shadow_softness = 1.0;
uniform float penumbra_ambient_softness = 1.0;
uniform float terrain_transition_tiles = 3.0;

vec4 geometry(int index) {
    return texelFetch(shadow_geometry, ivec2(index % 1024, index / 1024), 0);
}
float opacity(ivec2 c) {
    ivec2 size = ivec2(map_size / 2.0);
    if (c.x < 0 || c.y < 0 || c.x >= size.x || c.y >= size.y) return 0.0;
    return texelFetch(light_data, c * 2, 0).b;
}

// Angular silhouette of an opaque rectangle as seen by this receiver.
vec2 shadow_interval(vec4 r, vec2 p, float lower, float upper) {
    if (r.y >= p.y) return vec2(upper, lower);
    float far_y = p.y - r.y;
    float near_y = max(0.000001, p.y - r.w);
    float left = min(atan(r.x - p.x, far_y), atan(r.x - p.x, near_y));
    float right = max(atan(r.z - p.x, far_y), atan(r.z - p.x, near_y));
    return vec2(max(lower, left), min(upper, right));
}
float source_integral(float angle, float spread) {
    float u = clamp((angle - sun_angle) / spread, -1.0, 1.0);
    return (u * sqrt(max(0.0, 1.0 - u * u)) + asin(u) + 1.57079632679) / 3.14159265359;
}

float analytic_sun(vec2 p, vec2 pixel_footprint) {
    vec2 ray = vec2(sin(sun_angle), -cos(sun_angle));
    float depth = 1.0;
    // Surface reception only: a short DDA exits the receiving solid. Shadow
    // boundaries themselves are analytic, not ray-marched or angularly sampled.
    if (opacity(ivec2(floor(p))) > 0.99) {
        ivec2 c = ivec2(floor(p));
        vec2 delta = 1.0 / max(abs(ray), vec2(0.000001));
        vec2 next = vec2(ray.x >= 0.0 ? float(c.x + 1) - p.x : p.x - float(c.x), p.y - float(c.y)) * delta;
        float distance = 0.0;
        bool found = false;
        for (int i = 0; i < 48; i++) {
            distance = min(next.x, next.y);
            if (distance >= clamp(terrain_transition_tiles, 0.25, 16.0)) break;
            if (next.x <= distance + 0.000001) { c.x += ray.x >= 0.0 ? 1 : -1; next.x += delta.x; }
            if (next.y <= distance + 0.000001) { c.y -= 1; next.y += delta.y; }
            if (opacity(c) < 0.99) { found = true; break; }
        }
        if (found) {
            p += ray * (distance + 0.001);
            float normalized_depth = distance / clamp(terrain_transition_tiles, 0.25, 16.0);
            depth = 1.0 - smoothstep(0.0, 1.0, normalized_depth);
        } else depth = 0.0;
    }
    ivec2 bins = (ivec2(map_size / 2.0) + ivec2(15)) / 16;
    ivec2 bin = clamp(ivec2(floor(p / 16.0)), ivec2(0), bins - ivec2(1));
    vec4 header = geometry(bin.y * bins.x + bin.x);
    float spread = radians(8.0) * pow(1.0 - clamp(penumbra, 0.0, 1.0), 2.0);
    if (depth == 0.0 || ray.y >= -0.001) return 0.0;
    float covered = 0.0;
    int count = int(header.y);
    if (spread < 0.00001) {
        // Point-source limit with screen-space edge coverage.
        float slope = -tan(sun_angle);
        float aa = max(length(pixel_footprint), 0.0001);
        for (int i = 0; i < count; i++) {
            vec4 r = geometry(int(header.x) + i * 2);
            if (r.y >= p.y) continue;
            float far_y = p.y - r.y, near_y = max(0.0, p.y - r.w);
            float left = r.x + min(slope * far_y, slope * near_y);
            float right = r.z + max(slope * far_y, slope * near_y);
            float distance = min(p.x - left, right - p.x) / sqrt(1.0 + slope * slope);
            covered = max(covered, smoothstep(-aa * 0.5, aa * 0.5, distance));
            if (covered >= 1.0) break;
        }
    } else {
        // Integrate the UNION of blocked source intervals. Rectangle decomposition
        // cannot create additional penumbrae inside one connected silhouette.
        // Exact interval sweep, no fixed angular sample count or interval capacity.
        float lower = sun_angle - spread, upper = sun_angle + spread;
        float cursor = lower;
        for (int segment = 0; segment < count && cursor < upper; segment++) {
            float start = upper, end = lower;
            for (int i = 0; i < count; i++) {
                vec2 v = shadow_interval(geometry(int(header.x) + i * 2), p, lower, upper);
                if (v.y <= cursor || v.x >= v.y) continue;
                float candidate = max(cursor, v.x);
                if (candidate < start) { start = candidate; end = v.y; }
                else if (candidate == start) end = max(end, v.y);
            }
            if (end <= start) break;
            for (int merge = 0; merge < count && end < upper; merge++) {
                float extended = end;
                for (int i = 0; i < count; i++) {
                    vec2 v = shadow_interval(geometry(int(header.x) + i * 2), p, lower, upper);
                    if (v.x <= end && v.y > extended) extended = v.y;
                }
                if (extended <= end) break;
                end = extended;
            }
            covered += source_integral(end, spread) - source_integral(start, spread);
            cursor = end;
        }
        // Tone inside the fixed umbra/penumbra bounds, not a separate cone per tile.
        // Symmetric softness preserves 50% coverage instead of moving the gradient
        // toward its dark end, as a one-sided power curve did.
        covered = clamp(covered, 0.0, 1.0);
        float contrast = 1.0 / max(0.25, penumbra_gradient);
        float dark = pow(covered, contrast);
        float light = pow(1.0 - covered, contrast);
        covered = dark / max(dark + light, 0.000001);
        // Independent endpoint shaping: zero, midpoint and full coverage stay fixed.
        // Each control affects only its half of the transition.
        if (covered < 0.5) {
            covered = 0.5 * pow(covered * 2.0, max(0.25, penumbra_ambient_softness));
        } else {
            covered = 1.0 - 0.5 * pow((1.0 - covered) * 2.0, max(0.25, penumbra_shadow_softness));
        }
    }
    return (1.0 - covered) * depth;
}
