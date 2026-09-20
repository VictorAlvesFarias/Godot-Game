
uniform sampler2D shadow_geometry : filter_nearest, repeat_disable;
uniform vec2 map_size = vec2(1.0);
uniform float sun_angle = -0.5585;
uniform float penumbra = 0.5;
uniform float penumbra_shadow_transition = 1.0;
uniform float penumbra_ambient_transition = 1.0;

vec4 geometry(int index)
{
    return texelFetch(shadow_geometry, ivec2(index % 1024, index / 1024), 0);
}

vec2 shadow_interval(vec4 r, vec2 p, float lower, float upper)
{
    if (r.y >= p.y)
        return vec2(upper, lower);
    float far_y = p.y - r.y;
    float near_y = max(0.000001, p.y - r.w);
    float left = min(atan(r.x - p.x, far_y), atan(r.x - p.x, near_y));
    float right = max(atan(r.z - p.x, far_y), atan(r.z - p.x, near_y));
    return vec2(max(lower, left), min(upper, right));
}

float penumbra_edge_profile(float coordinate, float softness, float pixel_coverage)
{
    float q = clamp(coordinate, 0.0, 1.0);
    float hardness_exponent = clamp(softness, 0.0, 1.0);
    float aa_coverage = clamp(q / max(pixel_coverage, 0.000001), 0.0, 1.0);
    if (hardness_exponent == 0.0)
        return aa_coverage;
    float profile = pow(q * q * (2.0 - q), hardness_exponent);
    return min(profile, aa_coverage);
}

float sun_occlusion(vec2 p, vec2 pixel_footprint)
{
    ivec2 bins = (ivec2(map_size / 2.0) + ivec2(15)) / 16;
    ivec2 bin = clamp(ivec2(floor(p / 16.0)), ivec2(0), bins - ivec2(1));
    vec4 header = geometry(bin.y * bins.x + bin.x);
    float spread = radians(8.0) * pow(1.0 - clamp(penumbra, 0.0, 1.0), 2.0);
    float covered = 0.0;
    int count = int(header.y);

    if (spread < 0.00001)
    {
        float slope = -tan(sun_angle);
        float aa = max(length(pixel_footprint), 0.0001);
        for (int i = 0; i < count; i++)
        {
            vec4 r = geometry(int(header.x) + i * 2);
            if (r.y >= p.y)
                continue;
            float far_y = p.y - r.y, near_y = max(0.0, p.y - r.w);
            float left = r.x + min(slope * far_y, slope * near_y);
            float right = r.z + max(slope * far_y, slope * near_y);
            float distance = min(p.x - left, right - p.x) / sqrt(1.0 + slope * slope);
            covered = max(covered, smoothstep(-aa * 0.5, aa * 0.5, distance));
            if (covered >= 1.0)
                break;
        }
    }
    else
    {
        float lower = sun_angle - spread, upper = sun_angle + spread;
        float cursor = lower;
        for (int segment = 0; segment < count && cursor < upper; segment++)
        {
            float start = upper, end = lower;
            for (int i = 0; i < count; i++)
            {
                vec2 v = shadow_interval(geometry(int(header.x) + i * 2), p, lower, upper);
                if (v.y <= cursor || v.x >= v.y)
                    continue;
                float candidate = max(cursor, v.x);
                if (candidate < start)
                {
                    start = candidate;
                    end = v.y;
                }
                else if (candidate == start)
                    end = max(end, v.y);
            }
            if (end <= start)
                break;
            for (int merge = 0; merge < count && end < upper; merge++)
            {
                float extended = end;
                for (int i = 0; i < count; i++)
                {
                    vec2 v = shadow_interval(geometry(int(header.x) + i * 2), p, lower, upper);
                    if (v.x <= end && v.y > extended)
                        extended = v.y;
                }
                if (extended <= end)
                    break;
                end = extended;
            }
            covered += (end - start) / (2.0 * spread);
            cursor = end;
        }

        covered = clamp(covered, 0.0, 1.0);
        float coverage_pixel = max(2.0 * fwidth(covered), 0.000001);
        covered =
            covered < 0.5
                ? 0.5 * penumbra_edge_profile(2.0 * covered, penumbra_ambient_transition, coverage_pixel)
                : 1.0 - 0.5 * penumbra_edge_profile(2.0 * (1.0 - covered), penumbra_shadow_transition,
                                                    coverage_pixel);
    }
    return covered;
}

float projected_shadow(vec2 p, vec2 footprint)
{
    if (cos(sun_angle) <= 0.001)
        return 0.0;
    return sun_occlusion(p, footprint);
}
