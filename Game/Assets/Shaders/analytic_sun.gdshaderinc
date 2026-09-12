uniform sampler2D light_data : filter_nearest, repeat_disable;
// RGBA32F spatial index and compressed logical rectangles; no ray samples.
uniform sampler2D shadow_geometry : filter_nearest, repeat_disable;
uniform vec2 map_size = vec2(1.0);
uniform float sun_angle = -0.5585;
uniform float penumbra = 0.5;
uniform float penumbra_shadow_transition = 1.0;
uniform float penumbra_ambient_transition = 1.0;
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
// One intensity curve with variable hardness, not a blend of two images.
// Its domain and endpoints stay fixed. As the exponent approaches zero the
// curve approaches a step at the seam; the pixel footprint limits aliasing.
float penumbra_edge_profile(float coordinate, float softness, float pixel_coverage) {
    float q = clamp(coordinate, 0.0, 1.0);
    float hardness_exponent = clamp(softness, 0.0, 1.0);
    float aa_coverage = clamp(q / max(pixel_coverage, 0.000001), 0.0, 1.0);
    if (hardness_exponent == 0.0) return aa_coverage;
    float profile = pow(q * q * (2.0 - q), hardness_exponent);
    return min(profile, aa_coverage);
}

// Devolve (oclusao, recepcao). Separados porque quem chama precisa poder escalar SO a oclusao:
// enfraquecer a sombra projetada nao pode significar tirar a iluminacao do alvo.
vec2 analytic_sun_parts(vec2 p, vec2 pixel_footprint) {
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
    if (depth == 0.0 || ray.y >= -0.001) return vec2(0.0, 0.0);
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
            covered += (end - start) / (2.0 * spread);
            cursor = end;
        }
        // Only Penumbra determines the angular extent. These controls change
        // intensity, never coordinate scale, clipping thresholds or ramp width.
        covered = clamp(covered, 0.0, 1.0);
        float coverage_pixel = max(2.0 * fwidth(covered), 0.000001);
        covered = covered < 0.5
            ? 0.5 * penumbra_edge_profile(2.0 * covered, penumbra_ambient_transition, coverage_pixel)
            : 1.0 - 0.5 * penumbra_edge_profile(2.0 * (1.0 - covered), penumbra_shadow_transition, coverage_pixel);
    }
    return vec2(covered, depth);
}

float analytic_sun(vec2 p, vec2 pixel_footprint) {
    vec2 parts = analytic_sun_parts(p, pixel_footprint);
    return (1.0 - parts.x) * parts.y;
}
