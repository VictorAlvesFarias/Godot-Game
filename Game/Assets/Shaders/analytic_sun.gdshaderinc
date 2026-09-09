uniform sampler2D light_data : filter_nearest, repeat_disable;
// RGBA32F spatial index and compressed logical rectangles; no ray samples.
uniform sampler2D shadow_geometry : filter_nearest, repeat_disable;
uniform vec2 map_size = vec2(1.0);
uniform float sun_angle = -0.5585;
uniform float penumbra = 0.5;

vec4 geometry(int index) {
    return texelFetch(shadow_geometry, ivec2(index % 1024, index / 1024), 0);
}
float opacity(ivec2 c) {
    ivec2 size = ivec2(map_size / 2.0);
    if (c.x < 0 || c.y < 0 || c.x >= size.x || c.y >= size.y) return 0.0;
    return texelFetch(light_data, c * 2, 0).b;
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
        for (int i = 0; i < 8; i++) {
            distance = min(next.x, next.y);
            if (distance >= 3.0) break;
            if (next.x <= distance + 0.000001) { c.x += ray.x >= 0.0 ? 1 : -1; next.x += delta.x; }
            if (next.y <= distance + 0.000001) { c.y -= 1; next.y += delta.y; }
            if (opacity(c) < 0.99) { found = true; break; }
        }
        if (found) {
            p += ray * (distance + 0.001);
            depth = pow(0.43, max(0.0, distance - 0.5)) * (1.0 - smoothstep(2.0, 3.0, distance));
        } else depth = 0.0;
    }
    ivec2 bins = (ivec2(map_size / 2.0) + ivec2(15)) / 16;
    ivec2 bin = clamp(ivec2(floor(p / 16.0)), ivec2(0), bins - ivec2(1));
    vec4 header = geometry(bin.y * bins.x + bin.x);
    float spread = radians(8.0) * pow(1.0 - clamp(penumbra, 0.0, 1.0), 2.0);
    float left_slope = -tan(clamp(sun_angle + spread, radians(-89.5), radians(89.5)));
    float right_slope = -tan(clamp(sun_angle - spread, radians(-89.5), radians(89.5)));
    float transmission = 1.0;
    float opaque_coverage = 0.0;
    float center_slope = -tan(clamp(sun_angle, radians(-89.5), radians(89.5)));
    // Pixel footprint is constant regardless of angle, aperture or rectangle count.
    float aa = max(length(pixel_footprint), 0.0001);
    for (int i = 0; i < int(header.y) && depth > 0.0; i++) {
        vec4 r = geometry(int(header.x) + i * 2);
        float extinction = geometry(int(header.x) + i * 2 + 1).r;
        float far_distance = p.y - r.y;
        float near_distance = max(0.0, p.y - r.w);
        float left = r.x + min(left_slope * far_distance, left_slope * near_distance);
        float right = r.z + max(right_slope * far_distance, right_slope * near_distance);
        float left_normal = sqrt(1.0 + left_slope * left_slope);
        float right_normal = sqrt(1.0 + right_slope * right_slope);
        float center_left = r.x + min(center_slope * far_distance, center_slope * near_distance);
        float center_right = r.z + max(center_slope * far_distance, center_slope * near_distance);
        // The cone has a finite transition, distinct from pixel antialiasing.
        // Its width grows continuously with blocker distance and vanishes at contact.
        float left_softness = max(aa * 0.5, (center_left - left) * 0.85 / left_normal);
        float right_softness = max(aa * 0.5, (right - center_right) * 0.85 / right_normal);
        float coverage = min(smoothstep(-aa * 0.5, left_softness, (p.x - left) / left_normal),
            smoothstep(-aa * 0.5, right_softness, (right - p.x) / right_normal));
        coverage *= clamp(far_distance / aa + 0.5, 0.0, 1.0);
        // Suppress self-shadowing of the exact surface exit, without blurring into air rooms.
        if (far_distance < 0.0) coverage = 0.0;
        if (extinction > 0.99) {
            // Union opaque silhouettes; overlapping rectangle fringes must not
            // repeatedly darken the same shadow into visible solid bands.
            opaque_coverage = max(opaque_coverage, coverage);
            if (opaque_coverage >= 1.0) break;
            continue;
        }
        float optical_length = 1.0;
        if (extinction < 0.99) {
            vec2 inverse = 1.0 / vec2(abs(ray.x) < 0.000001 ? 0.000001 : ray.x, ray.y);
            vec2 a = (r.xy - p) * inverse, b = (r.zw - p) * inverse;
            vec2 lo = min(a, b), hi = max(a, b);
            optical_length = max(1.0, min(hi.x, hi.y) - max(0.0, max(lo.x, lo.y)));
        }
        float blocked = 1.0 - pow(max(1.0 - extinction, 0.0), optical_length);
        transmission *= 1.0 - coverage * blocked;
        if (transmission < 0.0039) break;
    }
    return ray.y >= -0.001 ? 0.0 : transmission * (1.0 - opaque_coverage) * depth;
}
